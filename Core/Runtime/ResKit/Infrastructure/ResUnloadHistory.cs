#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>保存卸载热路径所需的不可变信息，时间仅在诊断读取时格式化。</summary>
    internal readonly struct ResUnloadEvent
    {
        /// <summary>创建一条卸载事件。</summary>
        internal ResUnloadEvent(string path, string typeName, string providerName, DateTime unloadTimeUtc)
        {
            Path = path;
            TypeName = typeName;
            ProviderName = providerName;
            UnloadTimeUtc = unloadTimeUtc;
        }

        internal string Path { get; }
        internal string TypeName { get; }
        internal string ProviderName { get; }
        internal DateTime UnloadTimeUtc { get; }
    }

    /// <summary>使用固定数组保存最新卸载事件，避免历史记录无界增长。</summary>
    internal sealed class ResUnloadHistory
    {
        private readonly ResUnloadEvent[] mItems;
        private int mStart;

        /// <summary>创建指定固定容量的卸载历史环。</summary>
        internal ResUnloadHistory(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            mItems = new ResUnloadEvent[capacity];
        }

        internal int Count { get; private set; }
        internal long DroppedCount { get; private set; }

        /// <summary>追加最新事件，并在容量已满时覆盖最旧事件。</summary>
        internal void Add(ResUnloadEvent item)
        {
            if (Count < mItems.Length)
            {
                mItems[(mStart + Count) % mItems.Length] = item;
                Count++;
                return;
            }

            mItems[mStart] = item;
            mStart = (mStart + 1) % mItems.Length;
            DroppedCount++;
        }

        /// <summary>按最新优先顺序读取指定位置的事件。</summary>
        internal ResUnloadEvent GetNewest(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int physicalIndex = (mStart + Count - 1 - index) % mItems.Length;
            return mItems[physicalIndex];
        }

        /// <summary>清空全部引用和计数，使历史数据可被及时回收。</summary>
        internal void Clear()
        {
            Array.Clear(mItems, 0, mItems.Length);
            mStart = 0;
            Count = 0;
            DroppedCount = 0;
        }
    }
}
#endif
