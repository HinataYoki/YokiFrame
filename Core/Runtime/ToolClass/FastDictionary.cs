using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 快速字典，基于开放寻址法实现，减少 GC 压力
    /// 适合高频查找、热路径场景（如 Update 中的数据查询）
    /// </summary>
    public class FastDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private struct Entry
        {
            public int HashCode;    // 缓存哈希值，-1 表示空槽
            public TKey Key;
            public TValue Value;
        }

        private Entry[] mEntries;
        private int mCount;
        private int mFreeCount;
        private readonly IEqualityComparer<TKey> mComparer;

        private const int DEFAULT_CAPACITY = 16;
        private const float LOAD_FACTOR = 0.75f;

        public int Count => mCount - mFreeCount;
        public int Capacity => mEntries.Length;

        /// <summary>
        /// 创建快速字典
        /// </summary>
        /// <param name="capacity">初始容量，建议预估大小避免扩容</param>
        /// <param name="comparer">键比较器，默认使用 EqualityComparer</param>
        public FastDictionary(int capacity = DEFAULT_CAPACITY, IEqualityComparer<TKey> comparer = null)
        {
            int size = GetPrime(capacity);
            mEntries = new Entry[size];
            mComparer = comparer ?? EqualityComparer<TKey>.Default;
            InitializeEntries();
        }

        public TValue this[TKey key]
        {
            get
            {
                int index = FindEntry(key);
                if (index < 0)
                    throw new KeyNotFoundException($"键 '{key}' 不存在");
                return mEntries[index].Value;
            }
            set => Insert(key, value, false);
        }

        /// <summary>
        /// 添加键值对，键已存在时抛出异常
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            Insert(key, value, true);
        }

        /// <summary>
        /// 尝试添加，键已存在时返回 false
        /// </summary>
        public bool TryAdd(TKey key, TValue value)
        {
            if (key is null) return false;
            return Insert(key, value, true, throwOnExisting: false);
        }

        /// <summary>
        /// 尝试获取值，避免异常开销
        /// </summary>
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
        /// 获取值，不存在时返回默认值（无异常）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default)
        {
            int index = FindEntry(key);
            return index >= 0 ? mEntries[index].Value : defaultValue;
        }

        /// <summary>
        /// 获取或添加：键存在返回现有值，不存在则添加并返回新值
        /// </summary>
        public TValue GetOrAdd(TKey key, TValue value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            
            int index = FindEntry(key);
            if (index >= 0)
                return mEntries[index].Value;
            
            Insert(key, value, true);
            return value;
        }

        /// <summary>
        /// 获取或添加：键存在返回现有值，不存在则通过工厂创建
        /// </summary>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (valueFactory is null) throw new ArgumentNullException(nameof(valueFactory));
            
            int index = FindEntry(key);
            if (index >= 0)
                return mEntries[index].Value;
            
            var value = valueFactory(key);
            Insert(key, value, true);
            return value;
        }

        public bool ContainsKey(TKey key) => FindEntry(key) >= 0;

        public bool Remove(TKey key)
        {
            if (key is null) return false;
            
            int hashCode = mComparer.GetHashCode(key) & 0x7FFFFFFF;
            int bucket = hashCode % mEntries.Length;
            int probeCount = 0;

            while (probeCount < mEntries.Length)
            {
                ref Entry entry = ref mEntries[bucket];
                
                if (entry.HashCode == -1)
                    return false;
                
                if (entry.HashCode == hashCode && mComparer.Equals(entry.Key, key))
                {
                    entry.HashCode = -2;  // 标记为已删除（墓碑）
                    entry.Key = default;
                    entry.Value = default;
                    mFreeCount++;
                    return true;
                }
                
                bucket = (bucket + 1) % mEntries.Length;
                probeCount++;
            }
            return false;
        }

        public void Clear()
        {
            if (mCount > 0)
            {
                InitializeEntries();
                mCount = 0;
                mFreeCount = 0;
            }
        }

        /// <summary>
        /// 遍历所有键值对（无 GC 分配）
        /// </summary>
        public void ForEach(Action<TKey, TValue> action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));
            
            for (int i = 0; i < mEntries.Length; i++)
            {
                ref Entry entry = ref mEntries[i];
                if (entry.HashCode >= 0)
                {
                    action(entry.Key, entry.Value);
                }
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (int i = 0; i < mEntries.Length; i++)
            {
                if (mEntries[i].HashCode >= 0)
                {
                    yield return new KeyValuePair<TKey, TValue>(mEntries[i].Key, mEntries[i].Value);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #region 内部实现

        private void InitializeEntries()
        {
            for (int i = 0; i < mEntries.Length; i++)
            {
                mEntries[i].HashCode = -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindEntry(TKey key)
        {
            if (key is null) return -1;
            
            int hashCode = mComparer.GetHashCode(key) & 0x7FFFFFFF;
            int bucket = hashCode % mEntries.Length;
            int probeCount = 0;

            while (probeCount < mEntries.Length)
            {
                ref Entry entry = ref mEntries[bucket];
                
                if (entry.HashCode == -1)
                    return -1;
                
                if (entry.HashCode == hashCode && mComparer.Equals(entry.Key, key))
                    return bucket;
                
                bucket = (bucket + 1) % mEntries.Length;
                probeCount++;
            }
            return -1;
        }

        private bool Insert(TKey key, TValue value, bool add, bool throwOnExisting = true)
        {
            int hashCode = mComparer.GetHashCode(key) & 0x7FFFFFFF;
            int bucket = hashCode % mEntries.Length;
            int tombstone = -1;
            int probeCount = 0;

            while (probeCount < mEntries.Length)
            {
                ref Entry entry = ref mEntries[bucket];
                
                // 空槽：可以插入
                if (entry.HashCode == -1)
                {
                    int targetBucket = tombstone >= 0 ? tombstone : bucket;
                    ref Entry target = ref mEntries[targetBucket];
                    target.HashCode = hashCode;
                    target.Key = key;
                    target.Value = value;
                    
                    if (tombstone >= 0)
                        mFreeCount--;
                    else
                        mCount++;
                    
                    // 检查是否需要扩容
                    if (mCount > mEntries.Length * LOAD_FACTOR)
                        Resize();
                    
                    return true;
                }
                
                // 墓碑：记录位置，继续探测
                if (entry.HashCode == -2)
                {
                    if (tombstone < 0)
                        tombstone = bucket;
                }
                // 键已存在
                else if (entry.HashCode == hashCode && mComparer.Equals(entry.Key, key))
                {
                    if (add)
                    {
                        if (throwOnExisting)
                            throw new ArgumentException($"键 '{key}' 已存在");
                        return false;
                    }
                    entry.Value = value;
                    return true;
                }
                
                bucket = (bucket + 1) % mEntries.Length;
                probeCount++;
            }

            // 表满了，强制扩容后重试
            Resize();
            return Insert(key, value, add, throwOnExisting);
        }

        private void Resize()
        {
            int newSize = GetPrime(mEntries.Length * 2);
            var oldEntries = mEntries;
            mEntries = new Entry[newSize];
            InitializeEntries();
            
            mCount = 0;
            mFreeCount = 0;
            
            for (int i = 0; i < oldEntries.Length; i++)
            {
                if (oldEntries[i].HashCode >= 0)
                {
                    Insert(oldEntries[i].Key, oldEntries[i].Value, true);
                }
            }
        }

        /// <summary>
        /// 获取大于等于 min 的最小质数，用于哈希表大小
        /// </summary>
        private static int GetPrime(int min)
        {
            // 预定义质数表，覆盖常见容量
            int[] primes = { 17, 37, 79, 163, 331, 673, 1361, 2729, 5471, 10949, 
                            21911, 43853, 87719, 175447, 350899, 701819, 1403641 };
            
            foreach (int prime in primes)
            {
                if (prime >= min) return prime;
            }
            
            // 超出预定义范围，手动计算
            for (int i = min | 1; i < int.MaxValue; i += 2)
            {
                if (IsPrime(i)) return i;
            }
            return min;
        }

        private static bool IsPrime(int candidate)
        {
            if ((candidate & 1) == 0) return candidate == 2;
            int limit = (int)Math.Sqrt(candidate);
            for (int divisor = 3; divisor <= limit; divisor += 2)
            {
                if (candidate % divisor == 0) return false;
            }
            return true;
        }

        #endregion
    }
}
