using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 提供 FastDictionary 的开放寻址、扩容和容量计算实现。
    /// </summary>
    public partial class FastDictionary<TKey, TValue>
    {
        /// <summary>
        /// 把全部槽位重置为空槽并释放原有键和值引用。
        /// </summary>
        private void InitializeEntries()
        {
            for (var index = 0; index < mEntries.Length; index++)
            {
                mEntries[index].HashCode = EMPTY_HASH_CODE;
                mEntries[index].Key = default;
                mEntries[index].Value = default;
            }
        }

        /// <summary>
        /// 使用线性探测查找键所在槽位，空键或不存在时返回 -1。
        /// </summary>
        /// <param name="key">需要查找的键。</param>
        /// <returns>键所在槽位或 -1。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindEntry(TKey key)
        {
            if (key is null)
            {
                return -1;
            }

            int hashCode = GetNormalizedHashCode(key);
            int bucket = hashCode % mEntries.Length;
            for (var probeCount = 0; probeCount < mEntries.Length; probeCount++)
            {
                ref Entry entry = ref mEntries[bucket];
                if (entry.HashCode == EMPTY_HASH_CODE)
                {
                    return -1;
                }

                if (entry.HashCode == hashCode && mComparer.Equals(entry.Key, key))
                {
                    return bucket;
                }

                bucket = GetNextBucket(bucket, mEntries.Length);
            }

            return -1;
        }

        /// <summary>
        /// 使用单次线性探测添加、覆盖或读取一个键值对。
        /// </summary>
        /// <param name="key">需要写入的键。</param>
        /// <param name="value">需要写入的值。</param>
        /// <param name="behavior">键已存在时采用的行为。</param>
        /// <param name="storedValue">返回最终保存在字典中的值。</param>
        /// <returns>实际新增或覆盖时返回 true，保留已有值时返回 false。</returns>
        private bool Insert(
            TKey key,
            TValue value,
            InsertionBehavior behavior,
            out TValue storedValue)
        {
            RequireKey(key);
            int hashCode = GetNormalizedHashCode(key);
            int bucket = hashCode % mEntries.Length;
            int tombstone = -1;
            for (var probeCount = 0; probeCount < mEntries.Length; probeCount++)
            {
                ref Entry entry = ref mEntries[bucket];
                if (entry.HashCode == EMPTY_HASH_CODE)
                {
                    WriteNewEntry(tombstone >= 0 ? tombstone : bucket, hashCode, key, value);
                    storedValue = value;
                    return true;
                }

                if (entry.HashCode == TOMBSTONE_HASH_CODE && tombstone < 0)
                {
                    tombstone = bucket;
                }
                else if (entry.HashCode == hashCode && mComparer.Equals(entry.Key, key))
                {
                    return HandleExistingEntry(ref entry, key, value, behavior, out storedValue);
                }

                bucket = GetNextBucket(bucket, mEntries.Length);
            }

            if (tombstone >= 0)
            {
                WriteNewEntry(tombstone, hashCode, key, value);
                storedValue = value;
                return true;
            }

            Resize();
            return Insert(key, value, behavior, out storedValue);
        }

        /// <summary>
        /// 根据插入行为覆盖、保留或拒绝已经存在的键。
        /// </summary>
        /// <param name="entry">已经匹配的槽位。</param>
        /// <param name="key">重复键，用于异常信息。</param>
        /// <param name="value">覆盖模式下写入的新值。</param>
        /// <param name="behavior">键已存在时采用的行为。</param>
        /// <param name="storedValue">返回最终保存在槽位中的值。</param>
        /// <returns>覆盖值时返回 true，保留已有值时返回 false。</returns>
        private bool HandleExistingEntry(
            ref Entry entry,
            TKey key,
            TValue value,
            InsertionBehavior behavior,
            out TValue storedValue)
        {
            if (behavior == InsertionBehavior.Overwrite)
            {
                entry.Value = value;
                storedValue = value;
                mVersion++;
                return true;
            }

            if (behavior == InsertionBehavior.ThrowOnExisting)
            {
                throw new ArgumentException("Key '" + key + "' already exists.", nameof(key));
            }

            storedValue = entry.Value;
            return false;
        }

        /// <summary>
        /// 写入空槽或墓碑槽，并在已用槽超过阈值时扩容。
        /// </summary>
        /// <param name="index">目标槽位。</param>
        /// <param name="hashCode">规范化键哈希值。</param>
        /// <param name="key">需要写入的键。</param>
        /// <param name="value">需要写入的值。</param>
        private void WriteNewEntry(int index, int hashCode, TKey key, TValue value)
        {
            bool reusedTombstone = mEntries[index].HashCode == TOMBSTONE_HASH_CODE;
            mEntries[index].HashCode = hashCode;
            mEntries[index].Key = key;
            mEntries[index].Value = value;
            if (reusedTombstone)
            {
                mTombstoneCount--;
            }
            else
            {
                mOccupiedCount++;
            }

            mVersion++;
            if (mOccupiedCount > mResizeThreshold)
            {
                Resize();
            }
        }

        /// <summary>
        /// 扩大底层数组并使用已缓存哈希重新放置有效键值对。
        /// </summary>
        private void Resize()
        {
            if (mEntries.Length > int.MaxValue / 2)
            {
                throw new InvalidOperationException("FastDictionary cannot grow beyond the supported array size.");
            }

            Rehash(GetPrime(mEntries.Length * 2));
        }

        /// <summary>
        /// 使用指定容量重建开放寻址数组并清除全部墓碑。
        /// </summary>
        /// <param name="newSize">新的底层槽位数量。</param>
        private void Rehash(int newSize)
        {
            Entry[] oldEntries = mEntries;
            mEntries = new Entry[newSize];
            InitializeEntries();
            mOccupiedCount = 0;
            mTombstoneCount = 0;
            UpdateResizeThreshold();
            for (var index = 0; index < oldEntries.Length; index++)
            {
                if (oldEntries[index].HashCode >= 0)
                {
                    WriteRehashedEntry(oldEntries[index]);
                }
            }
        }

        /// <summary>
        /// 把带缓存哈希的有效槽位写入重建后的数组，不重复计算 comparer 哈希。
        /// </summary>
        /// <param name="source">旧数组中的有效槽位。</param>
        private void WriteRehashedEntry(Entry source)
        {
            int bucket = source.HashCode % mEntries.Length;
            while (mEntries[bucket].HashCode >= 0)
            {
                bucket = GetNextBucket(bucket, mEntries.Length);
            }

            mEntries[bucket] = source;
            mOccupiedCount++;
        }

        /// <summary>
        /// 当大量删除使字典变为空时原地清除墓碑，缩短后续失败查询链。
        /// </summary>
        private void ResetTombstonesWhenEmpty()
        {
            if (Count != 0 || mTombstoneCount < mEntries.Length / LOAD_FACTOR_DENOMINATOR)
            {
                return;
            }

            InitializeEntries();
            mOccupiedCount = 0;
            mTombstoneCount = 0;
        }

        /// <summary>
        /// 更新当前槽位容量对应的扩容阈值。
        /// </summary>
        private void UpdateResizeThreshold()
        {
            mResizeThreshold = mEntries.Length * LOAD_FACTOR_NUMERATOR / LOAD_FACTOR_DENOMINATOR;
        }

        /// <summary>
        /// 把预计元素数量换算为满足负载因子的最小槽位数量。
        /// </summary>
        /// <param name="expectedCount">预计元素峰值。</param>
        /// <returns>满足负载因子的最小槽位数量。</returns>
        private static int GetRequiredSlotCount(int expectedCount)
        {
            long required = ((long)expectedCount * LOAD_FACTOR_DENOMINATOR
                + LOAD_FACTOR_NUMERATOR - 1) / LOAD_FACTOR_NUMERATOR;
            if (required > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedCount));
            }

            return Math.Max(MINIMUM_SLOT_COUNT, (int)required);
        }

        /// <summary>
        /// 把键哈希值规范化为可与空槽、墓碑标记区分的非负数。
        /// </summary>
        /// <param name="key">需要计算哈希值的键。</param>
        /// <returns>非负哈希值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetNormalizedHashCode(TKey key)
        {
            return mComparer.GetHashCode(key) & 0x7FFFFFFF;
        }

        /// <summary>
        /// 返回线性探测的下一槽位，以条件回绕替代循环内整数取模。
        /// </summary>
        /// <param name="bucket">当前槽位。</param>
        /// <param name="length">底层数组长度。</param>
        /// <returns>下一槽位。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetNextBucket(int bucket, int length)
        {
            bucket++;
            return bucket == length ? 0 : bucket;
        }

        /// <summary>
        /// 校验需要写入的键不为空。
        /// </summary>
        /// <param name="key">需要校验的键。</param>
        private static void RequireKey(TKey key)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }
        }

        /// <summary>
        /// 返回不小于指定值的最近质数容量，减少稀疏质数表造成的容量浪费。
        /// </summary>
        /// <param name="minimum">所需最小容量。</param>
        /// <returns>可用质数容量。</returns>
        private static int GetPrime(int minimum)
        {
            int candidate = minimum | 1;
            while (candidate > 0)
            {
                if (IsPrime(candidate))
                {
                    return candidate;
                }

                if (candidate >= int.MaxValue - 2)
                {
                    break;
                }

                candidate += 2;
            }

            throw new InvalidOperationException("No supported prime capacity is available.");
        }

        /// <summary>
        /// 判断候选整数是否为质数。
        /// </summary>
        /// <param name="candidate">需要判断的整数。</param>
        /// <returns>候选值为质数时返回 true。</returns>
        private static bool IsPrime(int candidate)
        {
            if ((candidate & 1) == 0)
            {
                return candidate == 2;
            }

            int limit = (int)Math.Sqrt(candidate);
            for (var divisor = 3; divisor <= limit; divisor += 2)
            {
                if (candidate % divisor == 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 保存开放寻址槽位中的哈希值、键和值。
        /// </summary>
        private struct Entry
        {
            internal int HashCode;
            internal TKey Key;
            internal TValue Value;
        }

        /// <summary>
        /// 定义键已存在时插入操作采用的行为。
        /// </summary>
        private enum InsertionBehavior
        {
            Overwrite,
            ThrowOnExisting,
            KeepExisting
        }
    }
}
