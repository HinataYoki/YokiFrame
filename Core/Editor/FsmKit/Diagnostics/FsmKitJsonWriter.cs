#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 将 FsmKit 诊断快照写成稳定 JSON；只依赖 Core 契约，不使用宿主序列化器。
    /// </summary>
    internal static class FsmKitJsonWriter
    {
        private const int MAX_STATE_TREE_DEPTH = 8;

        /// <summary>
        /// 写入全部已注册 FSM 的摘要列表。
        /// </summary>
        /// <param name="snapshots">按注册顺序排列的诊断快照。</param>
        /// <returns>列表结果 JSON。</returns>
        internal static string WriteList(FsmKitDiagnosticSnapshot[] snapshots)
        {
            var builder = new StringBuilder(256);
            builder.Append("{\"fsms\":");
            AppendListArray(builder, snapshots);
            builder.Append(",\"count\":");
            builder.Append(snapshots.Length);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>
        /// 写入一个 FSM 的状态、选择和递归状态树。
        /// </summary>
        /// <param name="snapshot">目标诊断快照。</param>
        /// <returns>状态结果 JSON。</returns>
        internal static string WriteState(FsmKitDiagnosticSnapshot snapshot)
        {
            var builder = new StringBuilder(512);
            AppendStateObject(builder, snapshot, CreateEntryCountLookup(new[] { snapshot }));
            return builder.ToString();
        }

        /// <summary>
        /// 写入有界状态转换历史。
        /// </summary>
        /// <param name="history">按时间从旧到新排列的记录。</param>
        /// <returns>历史结果 JSON。</returns>
        internal static string WriteHistory(FsmKitTransitionRecord[] history)
        {
            var builder = new StringBuilder(256);
            AppendHistoryObject(builder, history);
            return builder.ToString();
        }

        /// <summary>
        /// 写入有界状态加入和移除事件。
        /// </summary>
        /// <param name="events">按时间从旧到新排列的记录。</param>
        /// <returns>状态事件结果 JSON。</returns>
        internal static string WriteStateEvents(FsmKitStateEventRecord[] events)
        {
            var builder = new StringBuilder(256);
            AppendStateEventsObject(builder, events);
            return builder.ToString();
        }

        /// <summary>
        /// 写入 Workbench 一次刷新需要的列表、选中详情和两类历史。
        /// </summary>
        /// <param name="snapshots">全部 FSM 快照。</param>
        /// <param name="selected">当前选中的 FSM。</param>
        /// <param name="historyJson">历史对象 JSON，可来自外部 provider。</param>
        /// <param name="stateEventsJson">状态事件对象 JSON，可来自外部 provider。</param>
        /// <returns>聚合结果 JSON。</returns>
        internal static string WriteWorkbench(
            FsmKitDiagnosticSnapshot[] snapshots,
            FsmKitDiagnosticSnapshot selected,
            string historyJson,
            string stateEventsJson)
        {
            var builder = new StringBuilder(1024);
            var entryCountsByFsm = CreateEntryCountLookup(snapshots);
            builder.Append('{');
            AppendStringProperty(builder, "fsmName", selected?.Name ?? string.Empty, false);
            AppendStringProperty(builder, "instanceId", selected?.InstanceId ?? string.Empty, true);
            builder.Append(",\"fsms\":");
            AppendListArray(builder, snapshots);
            AppendIntProperty(builder, "count", snapshots.Length, true);
            builder.Append(",\"selected\":");
            if (selected == null)
            {
                builder.Append("{}");
            }
            else
            {
                AppendStateObject(builder, selected, entryCountsByFsm);
            }
            builder.Append(",\"history\":");
            builder.Append(historyJson);
            builder.Append(",\"stateEvents\":");
            builder.Append(stateEventsJson);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>追加 FSM 摘要数组。</summary>
        private static void AppendListArray(StringBuilder builder, FsmKitDiagnosticSnapshot[] snapshots)
        {
            builder.Append('[');
            for (var index = 0; index < snapshots.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendSummary(builder, snapshots[index]);
            }

            builder.Append(']');
        }

        /// <summary>追加单个 FSM 摘要。</summary>
        private static void AppendSummary(StringBuilder builder, FsmKitDiagnosticSnapshot snapshot)
        {
            IFSM fsm = snapshot.Fsm;
            builder.Append('{');
            AppendStringProperty(builder, "instanceId", snapshot.InstanceId, false);
            AppendStringProperty(builder, "name", snapshot.Name, true);
            AppendStringProperty(builder, "machineState", fsm.MachineState.ToString(), true);
            AppendStringProperty(builder, "currentState", GetCurrentStateName(fsm), true);
            AppendIntProperty(builder, "currentStateId", fsm.CurrentStateId, true);
            AppendIntProperty(builder, "stateCount", fsm.GetAllStates().Count, true);
            builder.Append('}');
        }

        /// <summary>追加一个完整 FSM 状态对象。</summary>
        private static void AppendStateObject(
            StringBuilder builder,
            FsmKitDiagnosticSnapshot snapshot,
            IReadOnlyDictionary<IFSM, IReadOnlyDictionary<string, long>> entryCountsByFsm)
        {
            IFSM fsm = snapshot.Fsm;
            builder.Append('{');
            AppendStringProperty(builder, "fsmName", snapshot.Name, false);
            AppendStringProperty(builder, "instanceId", snapshot.InstanceId, true);
            AppendStringProperty(builder, "machineState", fsm.MachineState.ToString(), true);
            AppendStringProperty(builder, "currentState", GetCurrentStateName(fsm), true);
            AppendIntProperty(builder, "currentStateId", fsm.CurrentStateId, true);
            AppendIntProperty(builder, "stateCount", fsm.GetAllStates().Count, true);
            builder.Append(",\"states\":");
            var visited = new HashSet<IFSM>(FsmReferenceComparer.Instance);
            AppendStateTreeArray(builder, fsm, visited, entryCountsByFsm, 0);
            builder.Append('}');
        }

        /// <summary>递归追加状态树，循环引用或超过八层时写入空子数组。</summary>
        private static void AppendStateTreeArray(
            StringBuilder builder,
            IFSM fsm,
            HashSet<IFSM> visited,
            IReadOnlyDictionary<IFSM, IReadOnlyDictionary<string, long>> entryCountsByFsm,
            int depth)
        {
            if (fsm == null || depth > MAX_STATE_TREE_DEPTH || !visited.Add(fsm))
            {
                builder.Append("[]");
                return;
            }

            List<FsmStateTreeEntry> states = CreateOrderedEntries(fsm);
            builder.Append('[');
            for (var index = 0; index < states.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendStateNode(builder, fsm, states[index], visited, entryCountsByFsm, depth);
            }

            builder.Append(']');
            visited.Remove(fsm);
        }

        /// <summary>按首次加入顺序创建可稳定输出的状态条目。</summary>
        private static List<FsmStateTreeEntry> CreateOrderedEntries(IFSM fsm)
        {
            IReadOnlyDictionary<int, IState> states = fsm.GetAllStates();
            var entries = new List<FsmStateTreeEntry>(states.Count);
            foreach (var pair in states)
            {
                entries.Add(new FsmStateTreeEntry(
                    pair.Key,
                    fsm.GetStateOrderIndex(pair.Key),
                    pair.Value));
            }

            entries.Sort(CompareStateEntries);
            return entries;
        }

        /// <summary>按加入顺序比较状态条目，id 用作稳定次级排序。</summary>
        private static int CompareStateEntries(FsmStateTreeEntry left, FsmStateTreeEntry right)
        {
            int order = left.OrderIndex.CompareTo(right.OrderIndex);
            return order != 0 ? order : left.Id.CompareTo(right.Id);
        }

        /// <summary>追加普通或复合状态节点。</summary>
        private static void AppendStateNode(
            StringBuilder builder,
            IFSM owner,
            FsmStateTreeEntry entry,
            HashSet<IFSM> visited,
            IReadOnlyDictionary<IFSM, IReadOnlyDictionary<string, long>> entryCountsByFsm,
            int depth)
        {
            IFSM child = entry.State as IFSM;
            string stateName = GetStateName(owner, entry.Id);
            builder.Append('{');
            AppendIntProperty(builder, "id", entry.Id, false);
            AppendIntProperty(builder, "orderIndex", entry.OrderIndex, true);
            AppendStringProperty(builder, "name", stateName, true);
            AppendLongProperty(builder, "entryCount", GetEntryCount(entryCountsByFsm, owner, stateName), true);
            AppendStringProperty(builder, "stateType", entry.State?.GetType().Name ?? "null", true);
            AppendBoolProperty(builder, "isCurrent", entry.Id == owner.CurrentStateId, true);
            AppendBoolProperty(builder, "isComposite", child != null, true);
            if (child != null)
            {
                AppendCompositeFields(builder, child, visited, entryCountsByFsm, depth + 1);
            }

            builder.Append('}');
        }

        /// <summary>追加嵌套状态机的摘要和子节点。</summary>
        private static void AppendCompositeFields(
            StringBuilder builder,
            IFSM child,
            HashSet<IFSM> visited,
            IReadOnlyDictionary<IFSM, IReadOnlyDictionary<string, long>> entryCountsByFsm,
            int depth)
        {
            AppendStringProperty(builder, "childMachineName", child.Name, true);
            AppendStringProperty(builder, "machineState", child.MachineState.ToString(), true);
            AppendStringProperty(builder, "currentState", GetCurrentStateName(child), true);
            AppendIntProperty(builder, "currentStateId", child.CurrentStateId, true);
            AppendIntProperty(builder, "stateCount", child.GetAllStates().Count, true);
            builder.Append(",\"children\":");
            AppendStateTreeArray(builder, child, visited, entryCountsByFsm, depth);
        }

        /// <summary>按状态机引用建立累计进入次数索引，使嵌套 FSM 节点读取各自所属机器的计数。</summary>
        private static IReadOnlyDictionary<IFSM, IReadOnlyDictionary<string, long>> CreateEntryCountLookup(
            FsmKitDiagnosticSnapshot[] snapshots)
        {
            Dictionary<IFSM, IReadOnlyDictionary<string, long>> lookup =
                new Dictionary<IFSM, IReadOnlyDictionary<string, long>>(FsmReferenceComparer.Instance);
            for (var index = 0; index < snapshots.Length; index++)
            {
                lookup[snapshots[index].Fsm] = snapshots[index].EntryCounts;
            }

            return lookup;
        }

        /// <summary>读取指定机器状态的累计进入次数；未注册的嵌套机器返回零。</summary>
        private static long GetEntryCount(
            IReadOnlyDictionary<IFSM, IReadOnlyDictionary<string, long>> entryCountsByFsm,
            IFSM owner,
            string stateName)
        {
            return entryCountsByFsm.TryGetValue(owner, out var counts)
                && counts.TryGetValue(stateName, out var count)
                    ? count
                    : 0L;
        }

        /// <summary>追加历史对象。</summary>
        private static void AppendHistoryObject(StringBuilder builder, FsmKitTransitionRecord[] history)
        {
            builder.Append("{\"history\":[");
            for (var index = 0; index < history.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                FsmKitTransitionRecord record = history[index];
                builder.Append('{');
                AppendStringProperty(builder, "from", record.From, false);
                AppendStringProperty(builder, "to", record.To, true);
                AppendStringProperty(builder, "time", record.Time, true);
                builder.Append('}');
            }

            builder.Append("],\"count\":");
            builder.Append(history.Length);
            builder.Append('}');
        }

        /// <summary>追加状态生命周期对象。</summary>
        private static void AppendStateEventsObject(StringBuilder builder, FsmKitStateEventRecord[] events)
        {
            builder.Append("{\"events\":[");
            for (var index = 0; index < events.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                FsmKitStateEventRecord record = events[index];
                builder.Append('{');
                AppendStringProperty(builder, "eventName", record.EventName, false);
                AppendStringProperty(builder, "state", record.State, true);
                AppendStringProperty(builder, "time", record.Time, true);
                builder.Append('}');
            }

            builder.Append("],\"count\":");
            builder.Append(events.Length);
            builder.Append('}');
        }

        /// <summary>读取当前状态的枚举名称；无选择时返回 null 文本。</summary>
        private static string GetCurrentStateName(IFSM fsm)
        {
            return fsm.CurrentStateId < 0 ? "null" : GetStateName(fsm, fsm.CurrentStateId);
        }

        /// <summary>把整数状态标识转换为枚举名称，失败时回落数字文本。</summary>
        private static string GetStateName(IFSM fsm, int stateId)
        {
            try
            {
                object enumValue = Enum.ToObject(fsm.EnumType, stateId);
                string name = Enum.GetName(fsm.EnumType, enumValue);
                return string.IsNullOrEmpty(name)
                    ? stateId.ToString(CultureInfo.InvariantCulture)
                    : name;
            }
            catch (ArgumentException)
            {
                return stateId.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>追加已正确转义的字符串属性。</summary>
        private static void AppendStringProperty(
            StringBuilder builder,
            string name,
            string value,
            bool leadingComma)
        {
            AppendPropertyPrefix(builder, name, leadingComma);
            builder.Append('"');
            builder.Append(JsonHelper.EscapeString(value));
            builder.Append('"');
        }

        /// <summary>追加整数属性。</summary>
        private static void AppendIntProperty(
            StringBuilder builder,
            string name,
            int value,
            bool leadingComma)
        {
            AppendPropertyPrefix(builder, name, leadingComma);
            builder.Append(value);
        }

        /// <summary>追加 Int64 数值属性，累计计数不会受有界历史容量限制。</summary>
        private static void AppendLongProperty(
            StringBuilder builder,
            string name,
            long value,
            bool leadingComma)
        {
            AppendPropertyPrefix(builder, name, leadingComma);
            builder.Append(value);
        }

        /// <summary>追加布尔属性。</summary>
        private static void AppendBoolProperty(
            StringBuilder builder,
            string name,
            bool value,
            bool leadingComma)
        {
            AppendPropertyPrefix(builder, name, leadingComma);
            builder.Append(value ? "true" : "false");
        }

        /// <summary>追加属性逗号、名称和冒号。</summary>
        private static void AppendPropertyPrefix(StringBuilder builder, string name, bool leadingComma)
        {
            if (leadingComma)
            {
                builder.Append(',');
            }

            builder.Append('"');
            builder.Append(name);
            builder.Append("\":");
        }

        /// <summary>保存状态树排序需要的只读字段。</summary>
        private readonly struct FsmStateTreeEntry
        {
            /// <summary>创建状态树条目。</summary>
            internal FsmStateTreeEntry(int id, int orderIndex, IState state)
            {
                Id = id;
                OrderIndex = orderIndex;
                State = state;
            }

            internal int Id { get; }
            internal int OrderIndex { get; }
            internal IState State { get; }
        }

        /// <summary>按对象引用判断 FSM，用于准确截断循环状态树。</summary>
        private sealed class FsmReferenceComparer : IEqualityComparer<IFSM>
        {
            internal static readonly FsmReferenceComparer Instance = new FsmReferenceComparer();

            /// <summary>仅在两个引用指向同一对象时返回 true。</summary>
            public bool Equals(IFSM left, IFSM right) => ReferenceEquals(left, right);

            /// <summary>获取不受类型自定义 Equals 影响的引用哈希。</summary>
            public int GetHashCode(IFSM value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
#endif
