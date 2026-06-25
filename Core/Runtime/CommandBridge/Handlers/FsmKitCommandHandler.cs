using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 命令处理器：列出活动 FSM、查询状态和读取历史。
    /// </summary>
    public sealed class FsmKitCommandHandler : IKitCommandHandler
    {
        private const int MAX_STATE_TREE_DEPTH = 8;

        /// <inheritdoc />
        public string KitName => "FsmKit";

        /// <inheritdoc />
        public string[] SupportedActions => new[] { "list_all", "get_state", "get_history", "get_state_events", "get_workbench_snapshot" };

        /// <summary>
        /// 在这里注册 FSM，供命令桥查询。
        /// </summary>
        internal static readonly Dictionary<string, IFSM> sRegisteredFsms = new();

        /// <summary>
        /// 注册一个 FSM 实例，供命令桥查询和快照导出。
        /// </summary>
        /// <param name="name">FSM 在命令桥中的稳定名称。</param>
        /// <param name="fsm">要注册的 FSM 实例。</param>
        public static void RegisterFsm(string name, IFSM fsm) => sRegisteredFsms[name] = fsm;

        /// <summary>
        /// 注销指定名称的 FSM 实例。
        /// </summary>
        /// <param name="name">FSM 在命令桥中的稳定名称。</param>
        public static void UnregisterFsm(string name) => sRegisteredFsms.Remove(name);

        /// <summary>
        /// 清空所有已注册 FSM（在退出 Play Mode 时调用，防止 Domain Reload 关闭时
        /// 僵尸 FSM 泄漏到 Edit Mode）。
        /// </summary>
        public static void ClearAll() => sRegisteredFsms.Clear();

        /// <summary>
        /// 历史提供者注入点（可选）。Base 不记录状态历史，由 Adapter 编辑器层订阅
        /// FsmEditorHook 累积轨迹后注入；返回值须为已序列化的 history JSON 数组字符串。
        /// 入参为 fsmName，未注入或返回 null 时 get_history 回退为空数组。
        /// </summary>
        public static Func<string, string> HistoryProvider;

        /// <summary>
        /// 编辑器状态生命周期提供者注入点（可选）。Base 不记录 Add/Remove 轨迹，
        /// 由 Adapter 编辑器层缓存后注入；返回值须为已序列化的 events JSON 数组字符串。
        /// </summary>
        public static Func<string, string> StateLifecycleProvider;

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "list_all":
                    return ListAllFsms();
                case "get_state":
                    return GetState(payloadJson);
                case "get_history":
                    return GetHistory(payloadJson);
                case "get_state_events":
                    return GetStateEvents(payloadJson);
                case "get_workbench_snapshot":
                    return GetWorkbenchSnapshot(payloadJson);
                default:
                    throw new NotSupportedException($"Unknown FsmKit action '{action}'");
            }
        }

        private static string ListAllFsms()
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append("{\"fsms\":");
            AppendFsmListArray(sb);
            sb.Append(",\"count\":");
            sb.Append(sRegisteredFsms.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendFsmListArray(System.Text.StringBuilder sb)
        {
            sb.Append('[');
            bool first = true;
            foreach (var kvp in sRegisteredFsms)
            {
                if (!first) sb.Append(',');
                var fsm = kvp.Value;
                sb.Append("{\"name\":\"");
                sb.Append(EscapeJson(kvp.Key));
                sb.Append("\",\"machineState\":\"");
                sb.Append(fsm.MachineState.ToString());
                sb.Append('"');
                AppendStateDebugFields(sb, fsm, false);
                sb.Append('}');
                first = false;
            }
            sb.Append(']');
        }

        private static string GetState(string payloadJson)
        {
            var fsmName = JsonHelper.ExtractString(payloadJson, "fsmName");
            if (string.IsNullOrEmpty(fsmName))
                throw new ArgumentException("Missing 'fsmName' in payload");

            if (!sRegisteredFsms.TryGetValue(fsmName, out var fsm))
                throw new KeyNotFoundException($"FSM '{fsmName}' not found");

            return BuildStateJson(fsmName, fsm);
        }

        private static string BuildStateJson(string fsmName, IFSM fsm)
        {
            var sb = new System.Text.StringBuilder(128);
            sb.Append("{\"fsmName\":\"");
            sb.Append(EscapeJson(fsmName));
            sb.Append("\",\"machineState\":\"");
            sb.Append(fsm.MachineState.ToString());
            sb.Append('"');
            AppendStateDebugFields(sb, fsm, true);
            AppendStateTreeFields(sb, fsm);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetWorkbenchSnapshot(string payloadJson)
        {
            var fsmName = JsonHelper.ExtractString(payloadJson, "fsmName");
            if (string.IsNullOrEmpty(fsmName))
                throw new ArgumentException("Missing 'fsmName' in payload");

            if (!sRegisteredFsms.TryGetValue(fsmName, out var fsm))
                throw new KeyNotFoundException($"FSM '{fsmName}' not found");

            var sb = new System.Text.StringBuilder(512);
            sb.Append("{\"fsmName\":\"");
            sb.Append(EscapeJson(fsmName));
            sb.Append("\",\"fsms\":");
            AppendFsmListArray(sb);
            sb.Append(",\"count\":");
            sb.Append(sRegisteredFsms.Count);
            sb.Append(",\"selected\":");
            sb.Append(BuildStateJson(fsmName, fsm));
            sb.Append(",\"history\":");
            AppendProviderJson(sb, HistoryProvider, fsmName, "{\"history\":[],\"count\":0}");
            sb.Append(",\"stateEvents\":");
            AppendProviderJson(sb, StateLifecycleProvider, fsmName, "{\"events\":[],\"count\":0}");
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendProviderJson(System.Text.StringBuilder sb, Func<string, string> provider, string fsmName, string fallbackJson)
        {
            if (provider != null)
            {
                var json = provider(fsmName);
                if (!string.IsNullOrEmpty(json))
                {
                    sb.Append(json);
                    return;
                }
            }

            sb.Append(fallbackJson);
        }

        private static string GetHistory(string payloadJson)
        {
            // Base 不记录历史；若 Adapter 注入了 HistoryProvider 则委托其返回轨迹
            var provider = HistoryProvider;
            if (provider != null)
            {
                var fsmName = JsonHelper.ExtractString(payloadJson, "fsmName");
                var json = provider(fsmName);
                if (!string.IsNullOrEmpty(json)) return json;
            }
            return "{\"history\":[],\"count\":0}";
        }

        private static string GetStateEvents(string payloadJson)
        {
            var provider = StateLifecycleProvider;
            if (provider != null)
            {
                var fsmName = JsonHelper.ExtractString(payloadJson, "fsmName");
                var json = provider(fsmName);
                if (!string.IsNullOrEmpty(json)) return json;
            }
            return "{\"events\":[],\"count\":0}";
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void AppendStateDebugFields(System.Text.StringBuilder sb, IFSM fsm, bool includeStateId)
        {
            var currentStateName = TryGetCurrentStateName(fsm);
            if (!string.IsNullOrEmpty(currentStateName))
            {
                sb.Append(",\"currentState\":\"");
                sb.Append(EscapeJson(currentStateName));
                sb.Append('"');
            }

            if (includeStateId)
            {
                var currentStateId = TryGetCurrentStateId(fsm);
                if (currentStateId.HasValue)
                {
                    sb.Append(",\"currentStateId\":");
                    sb.Append(currentStateId.Value);
                }
            }

            var stateCount = TryGetStateCount(fsm);
            if (stateCount.HasValue)
            {
                sb.Append(",\"stateCount\":");
                sb.Append(stateCount.Value);
            }
        }

        private static void AppendStateTreeFields(System.Text.StringBuilder sb, IFSM fsm)
        {
#if UNITY_EDITOR || GODOT
            var visited = new HashSet<IFSM>();
            sb.Append(",\"states\":");
            AppendStateTreeArray(sb, fsm, visited, 0);
#endif
        }

#if UNITY_EDITOR || GODOT
        private static void AppendStateTreeArray(System.Text.StringBuilder sb, IFSM fsm, HashSet<IFSM> visited, int depth)
        {
            if (fsm == null || depth > MAX_STATE_TREE_DEPTH)
            {
                sb.Append("[]");
                return;
            }

            if (!visited.Add(fsm))
            {
                sb.Append("[]");
                return;
            }

            var states = fsm.GetAllStates();
            var currentStateId = fsm.CurrentStateId;
            sb.Append('[');
            bool first = true;
            foreach (var kvp in states)
            {
                if (!first) sb.Append(',');
                AppendStateNode(sb, fsm, kvp.Key, kvp.Value, currentStateId, visited, depth);
                first = false;
            }
            sb.Append(']');
            visited.Remove(fsm);
        }

        private static void AppendStateNode(
            System.Text.StringBuilder sb,
            IFSM owner,
            int stateId,
            IState state,
            int currentStateId,
            HashSet<IFSM> visited,
            int depth)
        {
            var stateName = TryGetEnumName(owner, stateId);
            var stateType = state != null ? state.GetType().Name : "null";
            var childFsm = state as IFSM;

            sb.Append('{');
            sb.Append("\"id\":");
            sb.Append(stateId);
            sb.Append(",\"orderIndex\":");
            sb.Append(owner.GetStateOrderIndex(stateId));
            sb.Append(",\"name\":\"");
            sb.Append(EscapeJson(stateName));
            sb.Append("\",\"stateType\":\"");
            sb.Append(EscapeJson(stateType));
            sb.Append("\",\"isCurrent\":");
            sb.Append(stateId == currentStateId ? "true" : "false");
            sb.Append(",\"isComposite\":");
            sb.Append(childFsm != null ? "true" : "false");

            if (childFsm != null)
            {
                sb.Append(",\"childMachineName\":\"");
                sb.Append(EscapeJson(childFsm.Name));
                sb.Append("\",\"machineState\":\"");
                sb.Append(childFsm.MachineState.ToString());
                sb.Append('"');

                var childCurrentStateName = TryGetCurrentStateName(childFsm);
                if (!string.IsNullOrEmpty(childCurrentStateName))
                {
                    sb.Append(",\"currentState\":\"");
                    sb.Append(EscapeJson(childCurrentStateName));
                    sb.Append('"');
                }

                sb.Append(",\"currentStateId\":");
                sb.Append(childFsm.CurrentStateId);
                sb.Append(",\"stateCount\":");
                sb.Append(TryGetStateCount(childFsm) ?? 0);
                sb.Append(",\"children\":");
                AppendStateTreeArray(sb, childFsm, visited, depth + 1);
            }

            sb.Append('}');
        }

        private static string TryGetEnumName(IFSM fsm, int stateId)
        {
            if (fsm == null || fsm.EnumType == null)
                return stateId.ToString();

            try
            {
                var name = Enum.GetName(fsm.EnumType, stateId);
                return string.IsNullOrEmpty(name) ? stateId.ToString() : name;
            }
            catch
            {
                return stateId.ToString();
            }
        }
#endif

        private static string TryGetCurrentStateName(IFSM fsm)
        {
#if UNITY_EDITOR || GODOT
            return fsm.CurrentState != null ? fsm.CurrentState.GetType().Name : "null";
#else
            var value = TryGetPropertyValue(fsm, "CurState");
            return value != null ? value.GetType().Name : "null";
#endif
        }

        private static int? TryGetCurrentStateId(IFSM fsm)
        {
#if UNITY_EDITOR || GODOT
            return fsm.CurrentStateId;
#else
            var value = TryGetPropertyValue(fsm, "CurEnum");
            if (value == null)
                return null;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return null;
            }
#endif
        }

        private static int? TryGetStateCount(IFSM fsm)
        {
#if UNITY_EDITOR || GODOT
            return fsm.GetAllStates().Count;
#else
            var type = fsm.GetType();
            while (type != null)
            {
                var field = type.GetField("mStateDic", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    var value = field.GetValue(fsm);
                    if (value is ICollection collection)
                        return collection.Count;
                }

                type = type.BaseType;
            }

            return null;
#endif
        }

#if !UNITY_EDITOR
        private static object TryGetPropertyValue(IFSM fsm, string propertyName)
        {
            var type = fsm.GetType();
            while (type != null)
            {
                var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                    return property.GetValue(fsm);

                type = type.BaseType;
            }

            return null;
        }
#endif
    }
}
