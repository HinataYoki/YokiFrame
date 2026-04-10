using System;
using System.Collections.Generic;
using UnityEditor;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// Legacy FsmKit runtime monitor publisher.
    /// </summary>
    /// <remarks>
    /// This debugger collects runtime FSM snapshots and forwards monitor events into the shared editor data bus.
    /// The page layer consumes shared channel contracts, while this class remains the runtime-side publisher.
    /// </remarks>
    public static class FsmDebugger
    {
        #region Channels

        /// <summary>
        /// Published when the active FSM list changes.
        /// Payload type: <c>IFSM</c>.
        /// </summary>
        public const string CHANNEL_FSM_LIST_CHANGED = DataChannels.FSM_LIST_CHANGED;

        /// <summary>
        /// Published when an FSM runtime state changes.
        /// Payload type: <c>IFSM</c>.
        /// </summary>
        public const string CHANNEL_FSM_STATE_CHANGED = DataChannels.FSM_STATE_CHANGED;

        /// <summary>
        /// Published when a transition history entry is appended.
        /// Payload type: <see cref="TransitionEntry"/>.
        /// </summary>
        public const string CHANNEL_FSM_HISTORY_LOGGED = DataChannels.FSM_HISTORY_LOGGED;

        #endregion

        /// <summary>
        /// One transition history record emitted by the FSM runtime monitor.
        /// </summary>
        public struct TransitionEntry
        {
            public float Time;
            public string FsmName;
            public string Action;
            public string FromState;
            public string ToState;
        }

        /// <summary>
        /// Runtime statistics tracked for one FSM instance.
        /// </summary>
        public class FsmRuntimeStats
        {
            /// <summary>
            /// Time when the current state was entered.
            /// </summary>
            public float StateEnterTime;

            /// <summary>
            /// Most recent previous state name.
            /// </summary>
            public string PreviousState;

            /// <summary>
            /// Visit count for each state.
            /// </summary>
            public readonly Dictionary<string, int> StateVisitCounts = new(8);

            /// <summary>
            /// Set of every state visited since the FSM was created or cleared.
            /// </summary>
            public readonly HashSet<string> VisitedStates = new(8);
        }

        #region Runtime Snapshot State

        private static readonly List<WeakReference<IFSM>> sActiveFsms = new(32);
        private static readonly List<TransitionEntry> sTransitionHistory = new(256);
        private static readonly Dictionary<string, FsmRuntimeStats> sFsmStats = new(16);

        /// <summary>
        /// Maximum number of transition history records retained in memory.
        /// </summary>
        public const int MAX_HISTORY_COUNT = 300;

        /// <summary>
        /// Cached transition history used by the legacy viewer and unified tool page.
        /// </summary>
        public static IReadOnlyList<TransitionEntry> TransitionHistory => sTransitionHistory;

        /// <summary>
        /// Whether transition history recording is enabled.
        /// </summary>
        public static bool RecordTransitions { get; set; } = true;

        #endregion

        #region Editor Bridge Publish

        /// <summary>
        /// Publishes a payload into the shared editor bus through the runtime-safe reflection bridge.
        /// </summary>
        private static void NotifyEditorDataChanged<T>(string channel, T data) =>
            EditorBridgeReflectionUtility.NotifyDataChanged(channel, data);

        #endregion

        /// <summary>
        /// Gets runtime statistics for the specified FSM, creating the entry when needed.
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
        /// Gets the elapsed time in seconds for the current state of the specified FSM.
        /// </summary>
        public static float GetStateDuration(string fsmName)
        {
            if (!sFsmStats.TryGetValue(fsmName, out var stats))
            {
                return 0f;
            }

            return UnityEngine.Time.time - stats.StateEnterTime;
        }

        #region Lifecycle

        /// <summary>
        /// Installs runtime callbacks from <c>FsmEditorHook</c> and clears cached data when Play Mode ends.
        /// </summary>
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
                    ClearRuntimeMonitorState(EditorPrefs.GetBool("FsmKitViewer_ClearHistoryOnStop", true));
                }
            };
        }

        /// <summary>
        /// Clears cached FSM monitor state retained by the legacy publisher.
        /// </summary>
        /// <param name="clearHistory">Whether transition history should also be cleared.</param>
        public static void ClearRuntimeMonitorState(bool clearHistory)
        {
            sActiveFsms.Clear();
            sFsmStats.Clear();

            if (clearHistory)
            {
                sTransitionHistory.Clear();
            }
        }

        #endregion

        #region Runtime Publish Handlers

        /// <summary>
        /// Handles runtime FSM creation.
        /// </summary>
        private static void OnFsmCreated(IFSM fsm)
        {
            sActiveFsms.Add(new WeakReference<IFSM>(fsm));
            sFsmStats[fsm.Name] = new FsmRuntimeStats();
            CleanupDeadReferences();

            NotifyEditorDataChanged(CHANNEL_FSM_LIST_CHANGED, fsm);
        }

        /// <summary>
        /// Handles runtime FSM disposal.
        /// </summary>
        private static void OnFsmDisposed(IFSM fsm)
        {
            AddHistory(fsm.Name, "Dispose", null, null);
            sFsmStats.Remove(fsm.Name);
            RemoveFsm(fsm);

            NotifyEditorDataChanged(CHANNEL_FSM_LIST_CHANGED, fsm);
        }

        /// <summary>
        /// Handles runtime FSM clear operations.
        /// </summary>
        private static void OnFsmCleared(IFSM fsm)
        {
            AddHistory(fsm.Name, "Clear", null, null);

            if (sFsmStats.TryGetValue(fsm.Name, out var stats))
            {
                stats.StateVisitCounts.Clear();
                stats.VisitedStates.Clear();
                stats.PreviousState = null;
            }

            NotifyEditorDataChanged(CHANNEL_FSM_STATE_CHANGED, fsm);
        }

        /// <summary>
        /// Handles FSM start events and initializes the first state snapshot.
        /// </summary>
        private static void OnFsmStarted(IFSM fsm, string initialState)
        {
            AddHistory(fsm.Name, "Start", null, initialState);

            var stats = GetStats(fsm.Name);
            stats.StateEnterTime = UnityEngine.Time.time;
            stats.VisitedStates.Add(initialState);
            stats.StateVisitCounts.TryGetValue(initialState, out var count);
            stats.StateVisitCounts[initialState] = count + 1;

            NotifyEditorDataChanged(CHANNEL_FSM_STATE_CHANGED, fsm);
        }

        /// <summary>
        /// Handles FSM state transitions and updates runtime statistics.
        /// </summary>
        private static void OnStateChanged(IFSM fsm, string fromState, string toState)
        {
            AddHistory(fsm.Name, "Change", fromState, toState);

            var stats = GetStats(fsm.Name);
            stats.PreviousState = fromState;
            stats.StateEnterTime = UnityEngine.Time.time;
            stats.VisitedStates.Add(toState);
            stats.StateVisitCounts.TryGetValue(toState, out var count);
            stats.StateVisitCounts[toState] = count + 1;

            NotifyEditorDataChanged(CHANNEL_FSM_STATE_CHANGED, fsm);
        }

        /// <summary>
        /// Handles runtime state-add events.
        /// </summary>
        private static void OnStateAdded(IFSM fsm, string stateName)
        {
            AddHistory(fsm.Name, "Add", null, stateName);
            NotifyEditorDataChanged(CHANNEL_FSM_STATE_CHANGED, fsm);
        }

        /// <summary>
        /// Handles runtime state-remove events.
        /// </summary>
        private static void OnStateRemoved(IFSM fsm, string stateName)
        {
            AddHistory(fsm.Name, "Remove", stateName, null);
            NotifyEditorDataChanged(CHANNEL_FSM_STATE_CHANGED, fsm);
        }

        /// <summary>
        /// Appends one transition history record and notifies the editor timeline channel.
        /// </summary>
        private static void AddHistory(string fsmName, string action, string fromState, string toState)
        {
            if (!RecordTransitions) return;

            if (sTransitionHistory.Count >= MAX_HISTORY_COUNT)
            {
                sTransitionHistory.RemoveAt(0);
            }

            var entry = new TransitionEntry
            {
                Time = UnityEngine.Time.time,
                FsmName = fsmName,
                Action = action,
                FromState = fromState,
                ToState = toState
            };

            sTransitionHistory.Add(entry);
            NotifyEditorDataChanged(CHANNEL_FSM_HISTORY_LOGGED, entry);
        }

        #endregion

        #region Snapshot Query Helpers

        /// <summary>
        /// Removes one FSM from the active weak-reference list.
        /// </summary>
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

        /// <summary>
        /// Removes dead weak references from the active FSM cache.
        /// </summary>
        private static void CleanupDeadReferences()
        {
            for (int i = sActiveFsms.Count - 1; i >= 0; i--)
            {
                if (!sActiveFsms[i].TryGetTarget(out _))
                {
                    sActiveFsms.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Writes all currently active FSM instances into the supplied list.
        /// </summary>
        public static void GetActiveFsms(List<IFSM> result)
        {
            result.Clear();
            CleanupDeadReferences();

            foreach (var weakRef in sActiveFsms)
            {
                if (weakRef.TryGetTarget(out var fsm))
                {
                    result.Add(fsm);
                }
            }
        }

        /// <summary>
        /// Clears retained transition history.
        /// </summary>
        public static void ClearHistory()
        {
            sTransitionHistory.Clear();
        }

        #endregion
    }
}
