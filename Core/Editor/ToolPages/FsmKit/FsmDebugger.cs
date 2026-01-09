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
        }

        private static void OnFsmDisposed(IFSM fsm)
        {
            AddHistory(fsm.Name, "Dispose", null, null);
            sFsmStats.Remove(fsm.Name);
            RemoveFsm(fsm);
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
        }

        private static void OnStateAdded(IFSM fsm, string stateName)
        {
            AddHistory(fsm.Name, "Add", null, stateName);
        }

        private static void OnStateRemoved(IFSM fsm, string stateName)
        {
            AddHistory(fsm.Name, "Remove", stateName, null);
        }

        private static void AddHistory(string fsmName, string action, string fromState, string toState)
        {
            if (!RecordTransitions) return;
            
            if (sTransitionHistory.Count >= MAX_HISTORY_COUNT)
                sTransitionHistory.RemoveAt(0);

            sTransitionHistory.Add(new TransitionEntry
            {
                Time = UnityEngine.Time.time,
                FsmName = fsmName,
                Action = action,
                FromState = fromState,
                ToState = toState
            });
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
