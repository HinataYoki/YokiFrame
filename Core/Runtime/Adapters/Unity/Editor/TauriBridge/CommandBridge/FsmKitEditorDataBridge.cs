#if !GODOT
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace YokiFrame.Unity
{
    /// <summary>
    /// FsmKit 编辑器数据桥（仅编辑器）。
    /// 职责：
    /// 1. 订阅 FsmEditorHook 的 7 个事件，自动填充 FsmKitCommandHandler.sRegisteredFsms
    /// 2. 累积每个 FSM 的状态切换轨迹，注入 FsmKitCommandHandler.HistoryProvider
    /// 3. 通过 UnityEventStreamWriter 将事件推送给 Tauri 前端（文件 I/O，替换旧 WS 推送）
    ///
    /// [InitializeOnLoad] 保证 Domain Reload 后自动重新挂接。
    /// </summary>
    [InitializeOnLoad]
    internal static class FsmKitEditorDataBridge
    {
        private static readonly Dictionary<string, List<TransitionRecord>> sHistory = new();
        private static readonly Dictionary<string, List<StateLifecycleRecord>> sStateLifecycle = new();
        private const int MAX_HISTORY_PER_FSM = 200;

        private readonly struct TransitionRecord
        {
            public readonly string From;
            public readonly string To;
            public readonly string Time;

            public TransitionRecord(string from, string to, string time)
            {
                From = from;
                To = to;
                Time = time;
            }
        }

        private readonly struct StateLifecycleRecord
        {
            public readonly string Event;
            public readonly string State;
            public readonly string Time;

            public StateLifecycleRecord(string evt, string state, string time)
            {
                Event = evt;
                State = state;
                Time = time;
            }
        }

        static FsmKitEditorDataBridge()
        {
            FsmEditorHook.OnFsmCreated -= OnFsmCreated;
            FsmEditorHook.OnFsmCreated += OnFsmCreated;
            FsmEditorHook.OnFsmDisposed -= OnFsmDisposed;
            FsmEditorHook.OnFsmDisposed += OnFsmDisposed;
            FsmEditorHook.OnFsmCleared -= OnFsmCleared;
            FsmEditorHook.OnFsmCleared += OnFsmCleared;
            FsmEditorHook.OnFsmStarted -= OnFsmStarted;
            FsmEditorHook.OnFsmStarted += OnFsmStarted;
            FsmEditorHook.OnStateChanged -= OnStateChanged;
            FsmEditorHook.OnStateChanged += OnStateChanged;
            FsmEditorHook.OnStateAdded -= OnStateAdded;
            FsmEditorHook.OnStateAdded += OnStateAdded;
            FsmEditorHook.OnStateRemoved -= OnStateRemoved;
            FsmEditorHook.OnStateRemoved += OnStateRemoved;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            FsmKitCommandHandler.HistoryProvider = BuildHistoryJson;
            FsmKitCommandHandler.StateLifecycleProvider = BuildStateLifecycleJson;
            FsmKitSnapshotPublisher.SnapshotPublished -= OnSnapshotPublished;
            FsmKitSnapshotPublisher.SnapshotPublished += OnSnapshotPublished;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                sHistory.Clear();
                sStateLifecycle.Clear();
                FsmKitCommandHandler.ClearAll();
                FsmKitSnapshotPublisher.TryPublish();
            }
        }

        private static void OnFsmCreated(IFSM fsm)
        {
            var name = ResolveName(fsm);
            EnsureRegisteredFsm(name, fsm);
            FsmKitSnapshotPublisher.TryPublish();
            PushEvent("fsm_update", BuildPayload("created", name, fsm, null, null));
        }

        private static void OnFsmDisposed(IFSM fsm)
        {
            var name = ResolveName(fsm);
            FsmKitCommandHandler.UnregisterFsm(name);
            sHistory.Remove(name);
            sStateLifecycle.Remove(name);
            FsmKitSnapshotPublisher.TryPublish();
            PushEvent("fsm_update", BuildPayload("disposed", name, fsm, null, null));
        }

        private static void OnFsmCleared(IFSM fsm)
        {
            var name = ResolveName(fsm);
            if (sHistory.TryGetValue(name, out var list))
                list.Clear();
            if (sStateLifecycle.TryGetValue(name, out var stateList))
                stateList.Clear();
            FsmKitSnapshotPublisher.TryPublish();
            PushEvent("fsm_update", BuildPayload("cleared", name, fsm, null, null));
        }

        private static void OnFsmStarted(IFSM fsm, string initialState)
        {
            var name = ResolveName(fsm);
            EnsureRegisteredFsm(name, fsm);
            AddHistoryRecord(name, "Start", initialState);
            FsmKitSnapshotPublisher.TryPublish();
            PushEvent("fsm_update", BuildPayload("started", name, fsm, null, initialState));
        }

        private static void OnStateChanged(IFSM fsm, string from, string to)
        {
            var name = ResolveName(fsm);
            EnsureRegisteredFsm(name, fsm);
            AddHistoryRecord(name, from, to);
            FsmKitSnapshotPublisher.RequestPublish();
        }

        private static void OnStateAdded(IFSM fsm, string stateName)
        {
            EnsureRegisteredFsm(ResolveName(fsm), fsm);
            AddStateLifecycleRecord(fsm, "added", stateName);
        }

        private static void OnStateRemoved(IFSM fsm, string stateName)
        {
            EnsureRegisteredFsm(ResolveName(fsm), fsm);
            AddStateLifecycleRecord(fsm, "removed", stateName);
        }

        private static void EnsureRegisteredFsm(string name, IFSM fsm)
        {
            if (fsm == null)
                return;

            FsmKitCommandHandler.RegisterFsm(name, fsm);
        }

        private static void AddHistoryRecord(string name, string from, string to)
        {
            if (!sHistory.TryGetValue(name, out var list))
            {
                list = new List<TransitionRecord>(16);
                sHistory[name] = list;
            }

            if (list.Count >= MAX_HISTORY_PER_FSM)
                list.RemoveAt(0);

            var time = System.DateTime.Now.ToString("HH:mm:ss.fff");
            list.Add(new TransitionRecord(from, to, time));
        }

        private static void AddStateLifecycleRecord(IFSM fsm, string evt, string stateName)
        {
            var name = ResolveName(fsm);
            if (!sStateLifecycle.TryGetValue(name, out var list))
            {
                list = new List<StateLifecycleRecord>(16);
                sStateLifecycle[name] = list;
            }

            if (list.Count >= MAX_HISTORY_PER_FSM)
                list.RemoveAt(0);

            list.Add(new StateLifecycleRecord(evt, stateName, System.DateTime.Now.ToString("HH:mm:ss.fff")));
            FsmKitSnapshotPublisher.RequestPublish();
            PushEvent("fsm_update", BuildLifecyclePayload(evt == "added" ? "state_added" : "state_removed", name, fsm, stateName));
        }

        private static void OnSnapshotPublished()
        {
            PushEvent("fsm_update", "{\"event\":\"snapshot_updated\",\"kit\":\"FsmKit\"}");
        }

        private static string ResolveName(IFSM fsm)
        {
            var name = fsm.Name;
            if (!string.IsNullOrEmpty(name)) return name;
            return fsm.EnumType != null ? fsm.EnumType.Name : "UnnamedFSM";
        }

        private static string BuildPayload(string evt, string fsmName, IFSM fsm, string from, string to)
        {
            var sb = new System.Text.StringBuilder(128);
            sb.Append("{\"event\":\"");
            sb.Append(evt);
            sb.Append("\",\"fsmName\":\"");
            sb.Append(fsmName ?? string.Empty);
            sb.Append("\",\"machineState\":\"");
            sb.Append(fsm.MachineState.ToString());
            if (from != null)
            {
                sb.Append("\",\"from\":\"");
                sb.Append(from);
            }
            if (to != null)
            {
                sb.Append("\",\"to\":\"");
                sb.Append(to);
            }
            sb.Append("\"}");
            return sb.ToString();
        }

        private static string BuildLifecyclePayload(string evt, string fsmName, IFSM fsm, string stateName)
        {
            var sb = new System.Text.StringBuilder(128);
            sb.Append("{\"event\":\"");
            sb.Append(evt);
            sb.Append("\",\"fsmName\":\"");
            sb.Append(Escape(fsmName));
            sb.Append("\",\"machineState\":\"");
            sb.Append(fsm.MachineState.ToString());
            sb.Append("\",\"state\":\"");
            sb.Append(Escape(stateName));
            sb.Append("\"}");
            return sb.ToString();
        }

        /// <summary>
        /// 通过 UnityEventStreamWriter 将事件推送给 Tauri 前端。
        /// 替代旧的 YokiWsClient.EnqueuePush（WebSocket 推送）。
        /// </summary>
        private static void PushEvent(string type, string payloadJson)
            => UnityEventStreamWriter.Write(type, payloadJson);

        private static string BuildHistoryJson(string fsmName)
        {
            if (string.IsNullOrEmpty(fsmName) || !sHistory.TryGetValue(fsmName, out var list))
                return null;

            var sb = new System.Text.StringBuilder(256);
            sb.Append("{\"history\":[");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var r = list[i];
                sb.Append("{\"from\":\"");
                sb.Append(Escape(r.From));
                sb.Append("\",\"to\":\"");
                sb.Append(Escape(r.To));
                sb.Append("\",\"time\":\"");
                sb.Append(Escape(r.Time));
                sb.Append("\"}");
            }
            sb.Append("],\"count\":");
            sb.Append(list.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildStateLifecycleJson(string fsmName)
        {
            if (string.IsNullOrEmpty(fsmName) || !sStateLifecycle.TryGetValue(fsmName, out var list))
                return null;

            var sb = new System.Text.StringBuilder(256);
            sb.Append("{\"events\":[");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var r = list[i];
                sb.Append("{\"event\":\"");
                sb.Append(Escape(r.Event));
                sb.Append("\",\"state\":\"");
                sb.Append(Escape(r.State));
                sb.Append("\",\"time\":\"");
                sb.Append(Escape(r.Time));
                sb.Append("\"}");
            }
            sb.Append("],\"count\":");
            sb.Append(list.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
#endif
