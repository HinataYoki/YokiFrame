#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 表示一次 EventKit 运行时活动；只保存稳定身份、时间 ticks 和展示字段，不持有监听器或负载对象。
    /// </summary>
    internal readonly struct EventKitActivityRecord
    {
        /// <summary>创建不可变的 EventKit 活动记录。</summary>
        internal EventKitActivityRecord(
            long sequence,
            string kind,
            string channel,
            string eventKey,
            string payloadType,
            string handler,
            long timestampTicks)
        {
            Sequence = sequence;
            Kind = kind ?? string.Empty;
            Channel = channel ?? string.Empty;
            EventKey = eventKey ?? string.Empty;
            PayloadType = payloadType ?? string.Empty;
            Handler = handler ?? string.Empty;
            TimestampTicks = timestampTicks;
        }

        internal long Sequence { get; }
        internal string Kind { get; }
        internal string Channel { get; }
        internal string EventKey { get; }
        internal string PayloadType { get; }
        internal string Handler { get; }
        internal long TimestampTicks { get; }
    }

    /// <summary>
    /// 保存当前诊断版本和有界活动副本，使 JSON 构建阶段不持有注册表锁。
    /// </summary>
    internal sealed class EventKitDiagnosticSnapshot
    {
        /// <summary>创建 EventKit 诊断快照。</summary>
        internal EventKitDiagnosticSnapshot(long version, long sequence, EventKitActivityRecord[] activities)
        {
            Version = version;
            Sequence = sequence;
            Activities = activities;
        }

        internal long Version { get; }
        internal long Sequence { get; }
        internal EventKitActivityRecord[] Activities { get; }
    }

    /// <summary>
    /// 表示一个可在 Workbench 中筛选的 Runtime 事件注册或活动事实。
    /// </summary>
    internal sealed class EventKitRegistrationSnapshot
    {
        internal string Channel { get; set; }
        internal string EventKey { get; set; }
        internal string PayloadType { get; set; }
        internal int HandlerCount { get; set; }
        internal long LastSequence { get; set; }
        internal long LastTimestampTicks { get; set; }
        internal bool Deprecated { get; set; }
    }

    /// <summary>
    /// 汇总 EventKit 当前注册表与有界活动历史，供命令和 Telemetry 共用。
    /// </summary>
    internal sealed class EventKitWorkbenchSnapshot
    {
        /// <summary>创建完整 Workbench 快照。</summary>
        internal EventKitWorkbenchSnapshot(
            long version,
            long sequence,
            IReadOnlyList<EventKitRegistrationSnapshot> registrations,
            EventKitActivityRecord[] activities)
        {
            Version = version;
            Sequence = sequence;
            Registrations = registrations;
            Activities = activities;
        }

        internal long Version { get; }
        internal long Sequence { get; }
        internal IReadOnlyList<EventKitRegistrationSnapshot> Registrations { get; }
        internal EventKitActivityRecord[] Activities { get; }
    }

    /// <summary>
    /// 提供固定容量环形缓冲区，避免 EventKit 活动历史无界增长。
    /// </summary>
    /// <typeparam name="T">缓冲区记录类型。</typeparam>
    internal sealed class EventKitBoundedBuffer<T>
    {
        private readonly T[] mItems;
        private int mHead;
        private int mCount;

        /// <summary>创建指定容量的 EventKit 缓冲区。</summary>
        internal EventKitBoundedBuffer(int capacity)
        {
            mItems = new T[capacity];
        }

        /// <summary>追加记录，并在容量满时覆盖最早记录。</summary>
        internal void Add(T item)
        {
            if (mCount == mItems.Length)
            {
                mItems[mHead] = item;
                mHead = (mHead + 1) % mItems.Length;
                return;
            }

            mItems[(mHead + mCount) % mItems.Length] = item;
            mCount++;
        }

        /// <summary>清空全部记录和可能持有的对象引用。</summary>
        internal void Clear()
        {
            System.Array.Clear(mItems, 0, mItems.Length);
            mHead = 0;
            mCount = 0;
        }

        /// <summary>按从旧到新的顺序创建独立数组。</summary>
        internal T[] ToArray()
        {
            T[] result = new T[mCount];
            for (var index = 0; index < mCount; index++)
            {
                result[index] = mItems[(mHead + index) % mItems.Length];
            }

            return result;
        }
    }
}
#endif
