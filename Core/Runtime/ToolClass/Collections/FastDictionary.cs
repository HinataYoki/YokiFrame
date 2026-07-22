using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 使用线性探测开放寻址的轻量字典，适合已知元素峰值且高频查询的运行时路径。
    /// </summary>
    /// <typeparam name="TKey">键类型。</typeparam>
    /// <typeparam name="TValue">值类型。</typeparam>
    public partial class FastDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private const int DEFAULT_CAPACITY = 16;
        private const int MINIMUM_SLOT_COUNT = 17;
        private const int LOAD_FACTOR_NUMERATOR = 3;
        private const int LOAD_FACTOR_DENOMINATOR = 4;
        private const int EMPTY_HASH_CODE = -1;
        private const int TOMBSTONE_HASH_CODE = -2;

        private Entry[] mEntries;
        private int mOccupiedCount;
        private int mTombstoneCount;
        private int mResizeThreshold;
        private int mVersion;
        private readonly IEqualityComparer<TKey> mComparer;

        /// <summary>
        /// 创建指定预计元素峰值和键比较器的快速字典。
        /// </summary>
        /// <param name="capacity">不触发扩容时预计保存的元素数量。</param>
        /// <param name="comparer">自定义键比较器；为空时使用默认比较器。</param>
        public FastDictionary(int capacity = DEFAULT_CAPACITY, IEqualityComparer<TKey> comparer = null)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            int slotCount = GetPrime(GetRequiredSlotCount(capacity));
            mEntries = new Entry[slotCount];
            mComparer = comparer ?? EqualityComparer<TKey>.Default;
            InitializeEntries();
            UpdateResizeThreshold();
        }

        /// <summary>
        /// 获取当前有效键值对数量，不包含已经删除的墓碑槽。
        /// </summary>
        public int Count
        {
            get { return mOccupiedCount - mTombstoneCount; }
        }

        /// <summary>
        /// 获取当前底层开放寻址数组容量。
        /// </summary>
        public int Capacity
        {
            get { return mEntries.Length; }
        }

        /// <summary>
        /// 按键读取或写入值；读取不存在的键会抛出 KeyNotFoundException。
        /// </summary>
        /// <param name="key">需要读取或写入的键。</param>
        /// <returns>键关联的值。</returns>
        public TValue this[TKey key]
        {
            get
            {
                int index = FindEntry(key);
                if (index < 0)
                {
                    throw new KeyNotFoundException("Key '" + key + "' does not exist.");
                }

                return mEntries[index].Value;
            }
            set { Insert(key, value, InsertionBehavior.Overwrite, out _); }
        }

        /// <summary>
        /// 添加键值对；键为空或已经存在时抛出异常。
        /// </summary>
        /// <param name="key">需要添加的键。</param>
        /// <param name="value">需要添加的值。</param>
        public void Add(TKey key, TValue value)
        {
            Insert(key, value, InsertionBehavior.ThrowOnExisting, out _);
        }

        /// <summary>
        /// 尝试添加键值对；键为空或已经存在时返回 false。
        /// </summary>
        /// <param name="key">需要添加的键。</param>
        /// <param name="value">需要添加的值。</param>
        /// <returns>实际添加成功时返回 true。</returns>
        public bool TryAdd(TKey key, TValue value)
        {
            return key is not null
                && Insert(key, value, InsertionBehavior.KeepExisting, out _);
        }

        /// <summary>
        /// 尝试读取指定键的值，不存在时返回 false 和默认值。
        /// </summary>
        /// <param name="key">需要查询的键。</param>
        /// <param name="value">查询成功时返回关联值。</param>
        /// <returns>键存在时返回 true。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            int index = FindEntry(key);
            if (index >= 0)
            {
                value = mEntries[index].Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 读取指定键的值；不存在时返回调用方提供的默认值。
        /// </summary>
        /// <param name="key">需要查询的键。</param>
        /// <param name="defaultValue">键不存在时返回的值。</param>
        /// <returns>已保存值或调用方默认值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default)
        {
            int index = FindEntry(key);
            return index >= 0 ? mEntries[index].Value : defaultValue;
        }

        /// <summary>
        /// 通过单次探测返回已有值，或保存并返回指定值。
        /// </summary>
        /// <param name="key">需要读取或添加的键。</param>
        /// <param name="value">键不存在时添加的值。</param>
        /// <returns>已有值或新添加的值。</returns>
        public TValue GetOrAdd(TKey key, TValue value)
        {
            Insert(key, value, InsertionBehavior.KeepExisting, out TValue storedValue);
            return storedValue;
        }

        /// <summary>
        /// 返回已有值；键不存在时调用工厂创建、保存并返回新值。
        /// </summary>
        /// <param name="key">需要读取或添加的键。</param>
        /// <param name="valueFactory">只在键不存在时调用的值工厂；工厂不得并发修改当前字典。</param>
        /// <returns>已有值或工厂创建的新值。</returns>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            RequireKey(key);
            if (valueFactory == null)
            {
                throw new ArgumentNullException(nameof(valueFactory));
            }

            int index = FindEntry(key);
            if (index >= 0)
            {
                return mEntries[index].Value;
            }

            TValue value = valueFactory.Invoke(key);
            Insert(key, value, InsertionBehavior.KeepExisting, out TValue storedValue);
            return storedValue;
        }

        /// <summary>
        /// 判断字典中是否存在指定键；空键返回 false。
        /// </summary>
        /// <param name="key">需要查询的键。</param>
        /// <returns>键存在时返回 true。</returns>
        public bool ContainsKey(TKey key)
        {
            return FindEntry(key) >= 0;
        }

        /// <summary>
        /// 删除指定键并把槽位标记为可复用墓碑。
        /// </summary>
        /// <param name="key">需要删除的键。</param>
        /// <returns>实际删除键值对时返回 true。</returns>
        public bool Remove(TKey key)
        {
            int index = FindEntry(key);
            if (index < 0)
            {
                return false;
            }

            mEntries[index].HashCode = TOMBSTONE_HASH_CODE;
            mEntries[index].Key = default;
            mEntries[index].Value = default;
            mTombstoneCount++;
            mVersion++;
            ResetTombstonesWhenEmpty();
            return true;
        }

        /// <summary>
        /// 清空全部键值对及其引用，同时保留当前底层容量供后续复用。
        /// </summary>
        public void Clear()
        {
            if (mOccupiedCount == 0)
            {
                return;
            }

            InitializeEntries();
            mOccupiedCount = 0;
            mTombstoneCount = 0;
            mVersion++;
        }

        /// <summary>
        /// 依次访问全部有效键值对；回调期间不得修改当前字典。
        /// </summary>
        /// <param name="action">接收键和值的访问回调。</param>
        public void ForEach(Action<TKey, TValue> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            int version = mVersion;
            Entry[] entries = mEntries;
            for (var index = 0; index < entries.Length; index++)
            {
                ref Entry entry = ref entries[index];
                if (entry.HashCode >= 0)
                {
                    action.Invoke(entry.Key, entry.Value);
                    EnsureVersion(version);
                }
            }
        }

        /// <summary>
        /// 返回直接 foreach 不产生迭代器对象分配的结构体枚举器。
        /// </summary>
        /// <returns>当前字典的结构体枚举器。</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <summary>
        /// 通过泛型 IEnumerable 契约返回枚举器；接口调用会装箱结构体枚举器。
        /// </summary>
        /// <returns>泛型键值对枚举器。</returns>
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 通过非泛型 IEnumerable 契约返回枚举器；接口调用会装箱结构体枚举器。
        /// </summary>
        /// <returns>非泛型键值对枚举器。</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 校验枚举期间字典没有发生结构或值修改。
        /// </summary>
        /// <param name="version">枚举开始时保存的版本。</param>
        private void EnsureVersion(int version)
        {
            if (version != mVersion)
            {
                throw new InvalidOperationException("The dictionary was modified during enumeration.");
            }
        }
    }
}
