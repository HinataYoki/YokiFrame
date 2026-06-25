using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 基于开放寻址的轻量字典，面向热路径查找并减少 GC 压力。
    /// </summary>
    public class FastDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private struct Entry
        {
            /// <summary>缓存的哈希值。<c>-1</c> 表示空槽，<c>-2</c> 表示墓碑槽。</summary>
            public int HashCode;
            public TKey Key;
            public TValue Value;
        }

        private Entry[] mEntries;
        private int mCount;
        private int mFreeCount;
        private readonly IEqualityComparer<TKey> mComparer;

        private const int DEFAULT_CAPACITY = 16;
        private const float LOAD_FACTOR = 0.75f;

        /// <summary>当前存储的有效键值对数量。</summary>
        public int Count => mCount - mFreeCount;

        /// <summary>当前底层数组容量。</summary>
        public int Capacity => mEntries.Length;

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
                    throw new KeyNotFoundException($"Key '{key}' does not exist.");
                return mEntries[index].Value;
            }
            set => Insert(key, value, false);
        }

        public void Add(TKey key, TValue value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            Insert(key, value, true);
        }

        public bool TryAdd(TKey key, TValue value)
        {
            if (key is null) return false;
            return Insert(key, value, true, throwOnExisting: false);
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default)
        {
            int index = FindEntry(key);
            return index >= 0 ? mEntries[index].Value : defaultValue;
        }

        public TValue GetOrAdd(TKey key, TValue value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            int index = FindEntry(key);
            if (index >= 0) return mEntries[index].Value;
            Insert(key, value, true);
            return value;
        }

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (valueFactory is null) throw new ArgumentNullException(nameof(valueFactory));
            int index = FindEntry(key);
            if (index >= 0) return mEntries[index].Value;
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
                if (entry.HashCode == -1) return false;
                if (entry.HashCode == hashCode && mComparer.Equals(entry.Key, key))
                {
                    entry.HashCode = -2;
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

        public void ForEach(Action<TKey, TValue> action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));
            for (int i = 0; i < mEntries.Length; i++)
            {
                ref Entry entry = ref mEntries[i];
                if (entry.HashCode >= 0)
                    action(entry.Key, entry.Value);
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (int i = 0; i < mEntries.Length; i++)
            {
                if (mEntries[i].HashCode >= 0)
                    yield return new KeyValuePair<TKey, TValue>(mEntries[i].Key, mEntries[i].Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void InitializeEntries()
        {
            for (int i = 0; i < mEntries.Length; i++)
                mEntries[i].HashCode = -1;
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
                if (entry.HashCode == -1) return -1;
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
                if (entry.HashCode == -1)
                {
                    int targetBucket = tombstone >= 0 ? tombstone : bucket;
                    ref Entry target = ref mEntries[targetBucket];
                    target.HashCode = hashCode;
                    target.Key = key;
                    target.Value = value;
                    if (tombstone >= 0) mFreeCount--;
                    else mCount++;
                    if (mCount > mEntries.Length * LOAD_FACTOR) Resize();
                    return true;
                }
                if (entry.HashCode == -2)
                {
                    if (tombstone < 0) tombstone = bucket;
                }
                else if (entry.HashCode == hashCode && mComparer.Equals(entry.Key, key))
                {
                    if (add)
                    {
                        if (throwOnExisting) throw new ArgumentException($"Key '{key}' already exists.");
                        return false;
                    }
                    entry.Value = value;
                    return true;
                }
                bucket = (bucket + 1) % mEntries.Length;
                probeCount++;
            }
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
                    Insert(oldEntries[i].Key, oldEntries[i].Value, true);
            }
        }

        private static int GetPrime(int min)
        {
            int[] primes = { 17, 37, 79, 163, 331, 673, 1361, 2729, 5471, 10949, 21911, 43853, 87719, 175447, 350899, 701819, 1403641 };
            foreach (int prime in primes)
            {
                if (prime >= min) return prime;
            }
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
    }
}
