#if GODOT
using System;
using System.Collections.Generic;
using System.Text;
using YokiFrame;
using YokiFrame;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot FsmKit 响应式事件桥：订阅 Base FSM 调试 hook，驱动命令查询与前端事件刷新。
    /// </summary>
    public static class GodotFsmKitEventBridge
    {
        private const string ENGINE_ID = "godot-runtime";
        private const string KIT_NAME = "FsmKit";
        private const string SNAPSHOT_NAME = "state";
        private const int MAX_HISTORY_PER_FSM = 200;
        private const double TELEMETRY_FLUSH_INTERVAL_MS = 100.0;
        private const double TELEMETRY_FLUSH_INTERVAL_SECONDS = TELEMETRY_FLUSH_INTERVAL_MS / 1000.0;

        private static readonly Dictionary<string, CommandBridgeTelemetryBuffer<TransitionRecord>> sHistory =
            new Dictionary<string, CommandBridgeTelemetryBuffer<TransitionRecord>>();
        private static readonly CommandBridgeTelemetryFlushGate sFlushGate =
            new CommandBridgeTelemetryFlushGate(TELEMETRY_FLUSH_INTERVAL_SECONDS);

        private static bool sInitialized;
        private static double sClockSeconds;
        private static string sYokiframeRoot;

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

        public static void Init(string yokiframeRoot)
        {
            sYokiframeRoot = yokiframeRoot;
            if (sInitialized)
                return;

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

            FsmKitCommandHandler.HistoryProvider = BuildHistoryJson;
            sInitialized = true;
        }

        public static void Tick(double delta)
        {
            if (!sInitialized)
                return;

            sClockSeconds += delta;
            if (sFlushGate.ConsumeIfDue(sClockSeconds))
                FlushTelemetryIfDue();
        }

        private static void OnFsmCreated(IFSM fsm)
        {
            var name = ResolveName(fsm);
            FsmKitCommandHandler.RegisterFsm(name, fsm);
            PublishSnapshot();
            PushEvent("created", name, fsm, null, null);
        }

        private static void OnFsmDisposed(IFSM fsm)
        {
            var name = ResolveName(fsm);
            FsmKitCommandHandler.UnregisterFsm(name);
            sHistory.Remove(name);
            PublishSnapshot();
            PushEvent("disposed", name, fsm, null, null);
        }

        private static void OnFsmCleared(IFSM fsm)
        {
            var name = ResolveName(fsm);
            CommandBridgeTelemetryBuffer<TransitionRecord> list;
            if (sHistory.TryGetValue(name, out list))
                list.Clear();
            PublishSnapshot();
            PushEvent("cleared", name, fsm, null, null);
        }

        private static void OnFsmStarted(IFSM fsm, string initialState)
        {
            var name = ResolveName(fsm);
            FsmKitCommandHandler.RegisterFsm(name, fsm);
            AddHistory(name, "Start", initialState);
            PublishSnapshot();
            PushEvent("started", name, fsm, null, initialState);
        }

        private static void OnStateChanged(IFSM fsm, string from, string to)
        {
            var name = ResolveName(fsm);
            FsmKitCommandHandler.RegisterFsm(name, fsm);
            AddHistory(name, from, to);
            RequestTelemetryFlush();
        }

        private static void OnStateAdded(IFSM fsm, string stateName)
        {
            var name = ResolveName(fsm);
            FsmKitCommandHandler.RegisterFsm(name, fsm);
            RequestTelemetryFlush();
            PushEvent("state_added", name, fsm, null, stateName);
        }

        private static void OnStateRemoved(IFSM fsm, string stateName)
        {
            var name = ResolveName(fsm);
            FsmKitCommandHandler.RegisterFsm(name, fsm);
            RequestTelemetryFlush();
            PushEvent("state_removed", name, fsm, stateName, null);
        }

        private static void AddHistory(string fsmName, string from, string to)
        {
            CommandBridgeTelemetryBuffer<TransitionRecord> list;
            if (!sHistory.TryGetValue(fsmName, out list))
            {
                list = new CommandBridgeTelemetryBuffer<TransitionRecord>(MAX_HISTORY_PER_FSM);
                sHistory[fsmName] = list;
            }

            list.Add(new TransitionRecord(from, to, DateTime.Now.ToString("HH:mm:ss.fff")));
        }

        private static void RequestTelemetryFlush()
        {
            sFlushGate.Request(sClockSeconds);
        }

        private static bool FlushTelemetryIfDue()
        {
            PublishSnapshot();
            PushSnapshotUpdatedEvent();
            return true;
        }

        private static void PublishSnapshot()
        {
            if (string.IsNullOrEmpty(sYokiframeRoot))
                return;

            var payloadJson = BuildSnapshotPayloadJson();
            FileBridgeSnapshotWriter.WriteSnapshot(
                sYokiframeRoot,
                ENGINE_ID,
                KIT_NAME,
                SNAPSHOT_NAME,
                payloadJson);
            AdapterSharedMemoryTelemetry.TryWriteLatest(ENGINE_ID, KIT_NAME, SNAPSHOT_NAME, payloadJson);
        }

        private static string BuildSnapshotPayloadJson()
        {
            return new FsmKitCommandHandler().HandleAction("list_all", "{}");
        }

        private static void PushSnapshotUpdatedEvent()
        {
            GodotEventStreamWriter.Write("fsm_update", "{\"event\":\"snapshot_updated\",\"kit\":\"FsmKit\"}");
        }

        private static void PushEvent(string evt, string fsmName, IFSM fsm, string from, string to)
        {
            GodotEventStreamWriter.Write("fsm_update", BuildPayload(evt, fsmName, fsm, from, to));
        }

        private static string ResolveName(IFSM fsm)
        {
            if (fsm == null)
                return "UnnamedFSM";

            if (!string.IsNullOrEmpty(fsm.Name))
                return fsm.Name;

            return fsm.EnumType != null ? fsm.EnumType.Name : "UnnamedFSM";
        }

        private static string BuildPayload(string evt, string fsmName, IFSM fsm, string from, string to)
        {
            var sb = new StringBuilder(160);
            sb.Append("{\"event\":\"");
            sb.Append(JsonHelper.EscapeString(evt));
            sb.Append("\",\"fsmName\":\"");
            sb.Append(JsonHelper.EscapeString(fsmName ?? string.Empty));
            sb.Append("\",\"machineState\":\"");
            sb.Append(fsm != null ? JsonHelper.EscapeString(fsm.MachineState.ToString()) : "Unknown");
            sb.Append('"');
            if (from != null)
            {
                sb.Append(",\"from\":\"");
                sb.Append(JsonHelper.EscapeString(from));
                sb.Append('"');
            }
            if (to != null)
            {
                sb.Append(",\"to\":\"");
                sb.Append(JsonHelper.EscapeString(to));
                sb.Append('"');
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildHistoryJson(string fsmName)
        {
            if (string.IsNullOrEmpty(fsmName))
                return null;

            CommandBridgeTelemetryBuffer<TransitionRecord> list;
            if (!sHistory.TryGetValue(fsmName, out list))
                return null;

            var sb = new StringBuilder(256);
            sb.Append("{\"history\":[");
            var items = list.Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                var record = items[i];
                sb.Append("{\"from\":\"");
                sb.Append(JsonHelper.EscapeString(record.From));
                sb.Append("\",\"to\":\"");
                sb.Append(JsonHelper.EscapeString(record.To));
                sb.Append("\",\"time\":\"");
                sb.Append(JsonHelper.EscapeString(record.Time));
                sb.Append("\"}");
            }

            sb.Append("],\"count\":");
            sb.Append(list.Count);
            sb.Append('}');
            return sb.ToString();
        }
    }
}
#endif
