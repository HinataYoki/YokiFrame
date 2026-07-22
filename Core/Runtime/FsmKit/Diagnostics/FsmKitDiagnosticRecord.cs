#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>
    /// 保存单个 FSM 的稳定诊断身份和有界历史，不持有任何宿主对象。
    /// </summary>
    internal sealed class FsmKitDiagnosticRecord
    {
        private const int MAX_RECORD_COUNT = 200;

        private readonly FsmKitBoundedBuffer<FsmKitTransitionRecord> mHistory =
            new FsmKitBoundedBuffer<FsmKitTransitionRecord>(MAX_RECORD_COUNT);
        private readonly FsmKitBoundedBuffer<FsmKitStateEventRecord> mStateEvents =
            new FsmKitBoundedBuffer<FsmKitStateEventRecord>(MAX_RECORD_COUNT);
        private readonly Dictionary<string, long> mEntryCounts =
            new Dictionary<string, long>(StringComparer.Ordinal);

        /// <summary>
        /// 创建绑定指定状态机实例的诊断记录。
        /// </summary>
        /// <param name="instanceId">当前进程内稳定且唯一的实例标识。</param>
        /// <param name="name">对外显示和兼容查询使用的诊断名称。</param>
        /// <param name="fsm">状态机实例。</param>
        internal FsmKitDiagnosticRecord(string instanceId, string name, IFSM fsm)
        {
            InstanceId = instanceId;
            Name = name;
            Fsm = fsm;
            Version = 1L;
        }

        /// <summary>获取当前进程内稳定的实例标识。</summary>
        internal string InstanceId { get; }

        /// <summary>获取当前诊断名称。</summary>
        internal string Name { get; private set; }

        /// <summary>获取绑定的状态机实例。</summary>
        internal IFSM Fsm { get; }

        /// <summary>获取当前实例诊断事实的单调版本。</summary>
        internal long Version { get; private set; }

        /// <summary>
        /// 更新诊断名称；实例标识和已有历史保持不变。
        /// </summary>
        /// <param name="name">新的非空诊断名称。</param>
        internal void Rename(string name)
        {
            Name = name;
            MarkChanged();
        }

        /// <summary>
        /// 追加一次成功启动或状态切换记录，只保留最新两百条。
        /// </summary>
        /// <param name="from">来源状态。</param>
        /// <param name="to">目标状态。</param>
        internal void RecordTransition(string from, string to)
        {
            mHistory.Add(new FsmKitTransitionRecord(from, to, CreateTimestamp()));
            if (!string.IsNullOrEmpty(to))
            {
                mEntryCounts.TryGetValue(to, out long current);
                mEntryCounts[to] = current + 1L;
            }

            MarkChanged();
        }

        /// <summary>
        /// 追加一次状态加入或移除记录，只保留最新两百条。
        /// </summary>
        /// <param name="eventName">稳定事件名称。</param>
        /// <param name="state">状态枚举名称。</param>
        internal void RecordStateEvent(string eventName, string state)
        {
            mStateEvents.Add(new FsmKitStateEventRecord(eventName, state, CreateTimestamp()));
            MarkChanged();
        }

        /// <summary>
        /// 清空历史与状态事件，但保留 FSM 注册身份供后续重新使用。
        /// </summary>
        internal void ClearRecords()
        {
            mHistory.Clear();
            mStateEvents.Clear();
            mEntryCounts.Clear();
            MarkChanged();
        }

        /// <summary>
        /// 标记未生成历史记录的状态机生命周期变化。
        /// </summary>
        internal void NotifyStateChanged()
        {
            MarkChanged();
        }

        /// <summary>
        /// 创建可脱离注册表锁读取的诊断快照。
        /// </summary>
        /// <returns>包含当前状态机引用和两类历史副本的快照。</returns>
        internal FsmKitDiagnosticSnapshot CreateSnapshot()
        {
            return new FsmKitDiagnosticSnapshot(
                InstanceId,
                Name,
                Version,
                Fsm,
                mHistory.ToArray(),
                mStateEvents.ToArray(),
                new Dictionary<string, long>(mEntryCounts, StringComparer.Ordinal));
        }

        /// <summary>
        /// 生成与旧工作台兼容的毫秒级本地时间文本。
        /// </summary>
        /// <returns>格式为 HH:mm:ss.fff 的时间。</returns>
        private static string CreateTimestamp()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 推进当前实例版本；版本只在持有注册表锁时修改。
        /// </summary>
        private void MarkChanged()
        {
            Version++;
        }
    }

    /// <summary>
    /// 提供固定容量的环形缓冲区，避免诊断历史无界增长。
    /// </summary>
    /// <typeparam name="T">记录值类型。</typeparam>
    internal sealed class FsmKitBoundedBuffer<T>
    {
        private readonly T[] mItems;
        private int mHead;
        private int mCount;

        /// <summary>
        /// 创建指定容量的缓冲区。
        /// </summary>
        /// <param name="capacity">必须大于零的最大记录数。</param>
        internal FsmKitBoundedBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            mItems = new T[capacity];
        }

        /// <summary>
        /// 追加记录；容量已满时覆盖最早记录。
        /// </summary>
        /// <param name="item">要追加的记录。</param>
        internal void Add(T item)
        {
            if (mCount == mItems.Length)
            {
                mItems[mHead] = item;
                mHead = (mHead + 1) % mItems.Length;
                return;
            }

            int writeIndex = (mHead + mCount) % mItems.Length;
            mItems[writeIndex] = item;
            mCount++;
        }

        /// <summary>
        /// 清除全部记录并释放缓冲区内可能存在的对象引用。
        /// </summary>
        internal void Clear()
        {
            Array.Clear(mItems, 0, mItems.Length);
            mHead = 0;
            mCount = 0;
        }

        /// <summary>
        /// 按从旧到新的顺序创建记录副本。
        /// </summary>
        /// <returns>不会暴露内部缓冲区的独立数组。</returns>
        internal T[] ToArray()
        {
            T[] snapshot = new T[mCount];
            for (var index = 0; index < mCount; index++)
            {
                snapshot[index] = mItems[(mHead + index) % mItems.Length];
            }

            return snapshot;
        }
    }

    /// <summary>表示一次状态机启动或状态切换。</summary>
    internal readonly struct FsmKitTransitionRecord
    {
        /// <summary>创建不可变转换记录。</summary>
        internal FsmKitTransitionRecord(string from, string to, string time)
        {
            From = from ?? string.Empty;
            To = to ?? string.Empty;
            Time = time ?? string.Empty;
        }

        internal string From { get; }
        internal string To { get; }
        internal string Time { get; }
    }

    /// <summary>表示一次状态加入或移除。</summary>
    internal readonly struct FsmKitStateEventRecord
    {
        /// <summary>创建不可变状态生命周期记录。</summary>
        internal FsmKitStateEventRecord(string eventName, string state, string time)
        {
            EventName = eventName ?? string.Empty;
            State = state ?? string.Empty;
            Time = time ?? string.Empty;
        }

        internal string EventName { get; }
        internal string State { get; }
        internal string Time { get; }
    }

    /// <summary>
    /// 提供脱离注册表锁的单实例诊断快照。
    /// </summary>
    internal sealed class FsmKitDiagnosticSnapshot
    {
        /// <summary>创建单实例诊断快照。</summary>
        internal FsmKitDiagnosticSnapshot(
            string instanceId,
            string name,
            long version,
            IFSM fsm,
            FsmKitTransitionRecord[] history,
            FsmKitStateEventRecord[] stateEvents,
            IReadOnlyDictionary<string, long> entryCounts)
        {
            InstanceId = instanceId;
            Name = name;
            Version = version;
            Fsm = fsm;
            History = history;
            StateEvents = stateEvents;
            EntryCounts = entryCounts;
        }

        internal string InstanceId { get; }
        internal string Name { get; }
        internal long Version { get; }
        internal IFSM Fsm { get; }
        internal FsmKitTransitionRecord[] History { get; }
        internal FsmKitStateEventRecord[] StateEvents { get; }
        internal IReadOnlyDictionary<string, long> EntryCounts { get; }
    }
}
#endif
