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

        private static readonly List<WeakReference<IFSM>> sActiveFsms = new(32);
        private static readonly List<TransitionEntry> sTransitionHistory = new(256);
        
        public const int MAX_HISTORY_COUNT = 300;

        public static IReadOnlyList<TransitionEntry> TransitionHistory => sTransitionHistory;
        
        public static bool RecordTransitions { get; set; } = true;

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
                    if (EditorPrefs.GetBool("FsmKitViewer_ClearHistoryOnStop", true))
                        sTransitionHistory.Clear();
                }
            };
        }

        private static void OnFsmCreated(IFSM fsm)
        {
            sActiveFsms.Add(new WeakReference<IFSM>(fsm));
            CleanupDeadReferences();
        }

        private static void OnFsmDisposed(IFSM fsm)
        {
            AddHistory(fsm.Name, "Dispose", null, null);
            RemoveFsm(fsm);
        }

        private static void OnFsmCleared(IFSM fsm)
        {
            AddHistory(fsm.Name, "Clear", null, null);
        }

        private static void OnFsmStarted(IFSM fsm, string initialState)
        {
            AddHistory(fsm.Name, "Start", null, initialState);
        }

        private static void OnStateChanged(IFSM fsm, string fromState, string toState)
        {
            AddHistory(fsm.Name, "Change", fromState, toState);
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
