using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Adapter bridge 发布 snapshot 前使用的有界内存遥测缓冲。
    /// </summary>
    public sealed class CommandBridgeTelemetryBuffer<T>
    {
        private readonly List<T> mItems;
        private readonly int mCapacity;

        /// <summary>
        /// 创建有界遥测缓冲。
        /// </summary>
        /// <param name="capacity">缓冲容量。</param>
        public CommandBridgeTelemetryBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            mCapacity = capacity;
            mItems = new List<T>(Math.Min(capacity, 16));
        }

        /// <summary>
        /// 获取当前缓冲项数量。
        /// </summary>
        public int Count => mItems.Count;

        /// <summary>
        /// 获取当前缓冲项列表。
        /// </summary>
        public IReadOnlyList<T> Items => mItems;

        /// <summary>
        /// 添加一条遥测项；容量满时丢弃最早项。
        /// </summary>
        /// <param name="item">要添加的遥测项。</param>
        public void Add(T item)
        {
            if (mItems.Count >= mCapacity)
                mItems.RemoveAt(0);

            mItems.Add(item);
        }

        /// <summary>
        /// 清空缓冲。
        /// </summary>
        public void Clear()
        {
            mItems.Clear();
        }
    }
}
