using System;
using System.Collections.Generic;
using UnityEditor;

namespace YokiFrame
{
    /// <summary>
    /// FSM 调试器 - 仅编辑器使用
    /// </summary>
    public static class FsmDebugger
    {
        #region 事件通道常量

        /// <summary>
        /// FSM 列表变化事件通道
        /// </summary>
        public const string CHANNEL_FSM_LIST_CHANGED = "FsmKit.FsmListChanged";

        /// <summary>
        /// FSM 状态变化事件通道
        /// </summary>
        public const string CHANNEL_FSM_STATE_CHANGED = "FsmKit.FsmStateChanged";

        /// <summary>
        /// 转换历史事件通道
        /// </summary>
        public const string CHANNEL_FSM_HISTORY_LOGGED = "FsmKit.HistoryLogged";

        #endregion

        /// <summary>
        /// 状态转换历史记录
        /// </summary>
        public struct TransitionEntry
        {
            public float Time;
            public string FsmName;
            public string Action;    // Start/Change/Add/Remove/Clear/Dispose
            public string FromState;
            public string ToState;
        }

        /// <summary>
        /// FSM 运行时统计数据
        /// </summary>
        public class FsmRuntimeStats
        {
            public float StateEnterTime;                                    // 当前状态进入时间
            public string PreviousState;                                    // 上一个状态
            public readonly Dictionary<string, int> StateVisitCounts = new(8);  // 状态访问次数
            public readonly HashSet<string> VisitedStates = new(8);             // 已访问过的状态
        }

        private static readonly List<WeakReference<IFSM>> sActiveFsms = new(32);
        private static readonly List<TransitionEntry> sTransitionHistory = new(256);
        private static readonly Dictionary<string, FsmRuntimeStats> sFsmStats = new(16);
        
        public const int MAX_HISTORY_COUNT = 300;

        public static IReadOnlyList<TransitionEntry> TransitionHistory => sTransitionHistory;
        
        public static bool RecordTransitions { get; set; } = true;

        #region 编辑器通知

        /// <summary>
        /// 通知编辑器数据变化（通过反射调用 EditorDataBridge）
        /// 避免运行时程序集直接引用编辑器程序集
        /// </summary>
        private static void NotifyEditorDataChanged<T>(string channel, T data)
        {
            var bridgeType = Type.GetType("YokiFrame.EditorTools.EditorDataBridge, YokiFrame.Core.Editor");
            if (bridgeType == null) return;

            // 获取泛型方法：NotifyDataChanged<T>(string, T)
            var methods = bridgeType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            System.Reflection.MethodInfo targetMethod = null;
            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                if (m.Name != "NotifyDataChanged" || !m.IsGenericMethodDefinition) continue;
                var parameters = m.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string))
                {
                    targetMethod = m;
                    break;
                }
            }

            if (targetMethod == null) return;

            var genericMethod = targetMethod.MakeGenericMethod(typeof(T));
            genericMethod.Invoke(null, new object[] { channel, data });
        }

        #endregion

        /// <summary>
        /// 获取 FSM 运行时统计数据
        /// </summary>
        public static FsmRuntimeStats GetStats(string fsmName)
        {
            if (!sFsmStats.TryGetValue(fsmName, out var stats))
            {
                stats = new FsmRuntimeStats();
                sFsmStats[fsmName] = stats;
            }
            return stats;
        }

        /// <summary>
        /// 获取当前状态持续时间
        /// </summary>
        public static float GetStateDuration(string fsmName)
        {
            if (!sFsmStats.TryGetValue(fsmName, out var stats))
                return 0f;
            return UnityEngine.Time.time - stats.StateEnterTime;
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            FsmEditorHook.OnFsmCreated = OnFsmCreated;
            FsmEditorHook.OnFsmDisposed = OnFsmDisposed;
            FsmEditorHook.OnFsmCleared = OnFsmCleared;
            FsmEditorHook.OnFsmStarted = OnFsmStarted;
            FsmEditorHook.OnStateChanged = OnStateChanged;
            FsmEditorHook.OnStateAdded = OnStateAdded;
            FsmEditorHook.OnStateRemoved = OnStateRemoved;

            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    sActiveFsms.Clear();
                    sFsmStats.Clear();
                    if (EditorPrefs.GetBool("FsmKitViewer_ClearHistoryOnStop", true))
                        sTransitionHistory.Clear();
                }
            };
        }

        private static void OnFsmCreated(IFSM fsm)
        {
            sActiveFsms.Add(new WeakReference<IFSM>(fsm));
            // 初始化统计数据
            sFsmStats[fsm.Name] = new FsmRuntimeStats();
            CleanupDeadReferences();
            
            // 通知编辑器 FSM 列表变化
            NotifyEditorDataChanged(CHANNEL_FSM_LIST_CHANGED, fsm);
        }

        private static void OnFsmDisposed(IFSM fsm)
        {
            AddHistory(fsm.Name, "Dispose", null, null);
            sFsmStats.Remove(fsm.Name);
            RemoveFsm(fsm);
            
            // 通知编辑器 FSM 列表变化
            NotifyEditorDataChanged(CHANNEL_FSM_LIST_CHANGED, fsm);
        }

        private static void OnFsmCleared(IFSM fsm)
        {
            AddHistory(fsm.Name, "Clear", null, null);
            // 重置统计数据
            if (sFsmStats.TryGetValue(fsm.Name, out var stats))
            {
                stats.StateVisitCounts.Clear();
                stats.VisitedStates.Clear();
                stats.PreviousState = null;
            }
            
            // 通知编辑器状态变化
            NotifyEditorDataChanged(CHANNEL_FSM_STATE_CHANGED, fsm);
        }

        private static void OnFsmStarted(IFSM fsm, string initialState)
        {
            AddHistory(fsm.Name, "Start", null, initialState);
            // 更新统计
            var stats = GetStats(fsm.Name);
            stats.StateEnterTime = UnityEngine.Time.time;
            stats.VisitedStates.Add(initialState);
            stats.StateVisitCounts.TryGetValue(initialState, out var count);
            stats.StateVisitCounts[initialState] = count + 1;
            
            // 通知编辑器状态变化
            NotifyEditorDataChanged(CHANNEL_FSM_STATE_CHANGED, fsm);
        }

        private static void OnStateChanged(IFSM fsm, string fromState, string toState)
        {
            AddHistory(fsm.Name, "Change", fromState, toState);
            // 更新统计
            var stats = GetStats(fsm.Name);
            stats.PreviousState = fromState;
            stats.StateEnterTime = UnityEngine.Time.time;
            stats.VisitedStates.Add(toState);
            stats.StateVisitCounts.TryGetValue(toState, out var count);
            stats.StateVisitCounts[toState] = count + 1;
            
            // 通知编辑器状态变化
            NotifyEditorDataChanged(CHANNEL_FSM_STATE_CHANGED, fsm);
        }

        private static void OnStateAdded(IFSM fsm, string stateName)
        {
            AddHistory(fsm.Name, "Add", null, stateName);
            
            // 通知编辑器状态变化
            NotifyEditorDataChanged(CHANNEL_FSM_STATE_CHANGED, fsm);
        }

        private static void OnStateRemoved(IFSM fsm, string stateName)
        {
            AddHistory(fsm.Name, "Remove", stateName, null);
            
            // 通知编辑器状态变化
            NotifyEditorDataChanged(CHANNEL_FSM_STATE_CHANGED, fsm);
        }

        private static void AddHistory(string fsmName, string action, string fromState, string toState)
        {
            if (!RecordTransitions) return;
            
            if (sTransitionHistory.Count >= MAX_HISTORY_COUNT)
                sTransitionHistory.RemoveAt(0);

            var entry = new TransitionEntry
            {
                Time = UnityEngine.Time.time,
                FsmName = fsmName,
                Action = action,
                FromState = fromState,
                ToState = toState
            };
            sTransitionHistory.Add(entry);
            
            // 通知编辑器历史记录变化
            NotifyEditorDataChanged(CHANNEL_FSM_HISTORY_LOGGED, entry);
        }

        private static void RemoveFsm(IFSM fsm)
        {
            for (int i = sActiveFsms.Count - 1; i >= 0; i--)
            {
                if (sActiveFsms[i].TryGetTarget(out var target) && target == fsm)
                {
                    sActiveFsms.RemoveAt(i);
                    break;
                }
            }
        }

        private static void CleanupDeadReferences()
        {
            for (int i = sActiveFsms.Count - 1; i >= 0; i--)
            {
                if (!sActiveFsms[i].TryGetTarget(out _))
                    sActiveFsms.RemoveAt(i);
            }
        }

        /// <summary>
        /// 获取所有活跃的 FSM 实例
        /// </summary>
        public static void GetActiveFsms(List<IFSM> result)
        {
            result.Clear();
            CleanupDeadReferences();
            
            foreach (var weakRef in sActiveFsms)
            {
                if (weakRef.TryGetTarget(out var fsm))
                    result.Add(fsm);
            }
        }

        public static void ClearHistory() => sTransitionHistory.Clear();
    }
}
