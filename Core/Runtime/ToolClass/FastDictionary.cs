using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// Lightweight dictionary based on open addressing, optimized for hot-path lookups with reduced GC pressure.
    /// </summary>
    public class FastDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private struct Entry
        {
            /// <summary>
            /// Cached hash code. <c>-1</c> means empty, <c>-2</c> means tombstone.
            /// </summary>
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

        /// <summary>
        /// Number of valid key/value pairs currently stored.
        /// </summary>
        public int Count => mCount - mFreeCount;

        /// <summary>
        /// Current backing array capacity.
        /// </summary>
        public int Capacity => mEntries.Length;

        /// <summary>
        /// Creates a fast dictionary.
        /// </summary>
        /// <param name="capacity">Initial capacity estimate.</param>
        /// <param name="comparer">Optional key comparer.</param>
        public FastDictionary(int capacity = DEFAULT_CAPACITY, IEqualityComparer<TKey> comparer = null)
        {
            int size = GetPrime(capacity);
            mEntries = new Entry[size];
            mComparer = comparer ?? EqualityComparer<TKey>.Default;
            InitializeEntries();
        }

        /// <summary>
        /// Gets or sets a value by key.
        /// </summary>
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

        /// <summary>
        /// Adds a key/value pair. Throws when the key already exists.
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            Insert(key, value, true);
        }

        /// <summary>
        /// Attempts to add a key/value pair. Returns <see langword="false"/> when the key already exists.
        /// </summary>
        public bool TryAdd(TKey key, TValue value)
        {
            if (key is null) return false;
            return Insert(key, value, true, throwOnExisting: false);
        }

        /// <summary>
        /// Attempts to get a value without throwing.
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
        /// Gets a value or returns the supplied fallback when the key does not exist.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default)
        {
            int index = FindEntry(key);
            return index >= 0 ? mEntries[index].Value : defaultValue;
        }

        /// <summary>
        /// Gets an existing value or inserts the supplied value.
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
        /// Gets an existing value or creates one through the supplied factory.
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

        /// <summary>
        /// Returns whether the key exists.
        /// </summary>
        public bool ContainsKey(TKey key) => FindEntry(key) >= 0;

        /// <summary>
        /// Removes one key/value pair.
        /// </summary>
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

        /// <summary>
        /// Clears all entries while retaining the current capacity.
        /// </summary>
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
        /// Iterates all valid entries without allocating an enumerator object explicitly.
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

        /// <summary>
        /// Enumerates all valid key/value pairs.
        /// </summary>
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

        #region Internal

        /// <summary>
        /// Marks every slot as empty.
        /// </summary>
        private void InitializeEntries()
        {
            for (int i = 0; i < mEntries.Length; i++)
            {
                mEntries[i].HashCode = -1;
            }
        }

        /// <summary>
        /// Finds the backing index for a key.
        /// </summary>
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

        /// <summary>
        /// Inserts or updates an entry.
        /// </summary>
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

                    if (tombstone >= 0)
                        mFreeCount--;
                    else
                        mCount++;

                    if (mCount > mEntries.Length * LOAD_FACTOR)
                        Resize();

                    return true;
                }

                if (entry.HashCode == -2)
                {
                    if (tombstone < 0)
                        tombstone = bucket;
                }
                else if (entry.HashCode == hashCode && mComparer.Equals(entry.Key, key))
                {
                    if (add)
                    {
                        if (throwOnExisting)
                            throw new ArgumentException($"Key '{key}' already exists.");
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

        /// <summary>
        /// Grows the backing storage and rehashes all entries.
        /// </summary>
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
        /// Returns the smallest prime greater than or equal to <paramref name="min"/>.
        /// </summary>
        private static int GetPrime(int min)
        {
            int[] primes =
            {
                17, 37, 79, 163, 331, 673, 1361, 2729, 5471, 10949,
                21911, 43853, 87719, 175447, 350899, 701819, 1403641
            };

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

        /// <summary>
        /// Returns whether the specified integer is prime.
        /// </summary>
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
