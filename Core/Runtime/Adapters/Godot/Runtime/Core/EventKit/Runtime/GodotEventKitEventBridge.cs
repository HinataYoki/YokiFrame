#if GODOT
using System;
using System.Text;
using YokiFrame;
using YokiFrame;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot EventKit 响应式事件桥：订阅 Base EventKit hook，发布 snapshot 与 event_update。
    /// </summary>
    public static class GodotEventKitEventBridge
    {
        private const string ENGINE_ID = "godot-runtime";
        private const string KIT_NAME = "EventKit";
        private const string SNAPSHOT_NAME = "state";
        private const int MAX_RECENT_EVENTS = 160;
        private const double FLUSH_INTERVAL_SECONDS = 0.1d;

        private static readonly CommandBridgeTelemetryBuffer<EventRecord> sRecentEvents =
            new CommandBridgeTelemetryBuffer<EventRecord>(MAX_RECENT_EVENTS);
        private static readonly CommandBridgeTelemetryFlushGate sFlushGate =
            new CommandBridgeTelemetryFlushGate(FLUSH_INTERVAL_SECONDS);

        private static bool sInitialized;
        private static double sClockSeconds;
        private static string sYokiframeRoot;

        private readonly struct EventRecord
        {
            public readonly string Kind;
            public readonly string Channel;
            public readonly string EventKey;
            public readonly string Handler;
            public readonly string PayloadType;
            public readonly string SourceFile;
            public readonly int SourceLine;
            public readonly string Time;

            public EventRecord(string kind, string channel, string eventKey, string handler, string payloadType, string sourceFile, int sourceLine, string time)
            {
                Kind = kind;
                Channel = channel;
                EventKey = eventKey;
                Handler = handler;
                PayloadType = payloadType;
                SourceFile = sourceFile;
                SourceLine = sourceLine;
                Time = time;
            }
        }

        public static void Init(string yokiframeRoot)
        {
            sYokiframeRoot = yokiframeRoot;
            if (sInitialized)
                return;

            EasyEventEditorHook.OnRegister -= OnRegister;
            EasyEventEditorHook.OnRegister += OnRegister;
            EasyEventEditorHook.OnUnRegister -= OnUnRegister;
            EasyEventEditorHook.OnUnRegister += OnUnRegister;
            EasyEventEditorHook.OnSend -= OnSend;
            EasyEventEditorHook.OnSend += OnSend;
            EasyEventEditorHook.OnClear -= OnClear;
            EasyEventEditorHook.OnClear += OnClear;

            EventKitCommandHandler.RecentEventsProvider = BuildRecentEventsJson;
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

        private static void OnRegister(string channel, string eventKey, Delegate handler)
        {
            AddRecord("register", channel, eventKey, ResolveHandlerName(handler), null, null, 0);
        }

        private static void OnUnRegister(string channel, string eventKey, Delegate handler)
        {
            AddRecord("unregister", channel, eventKey, ResolveHandlerName(handler), null, null, 0);
        }

        private static void OnSend(string channel, string eventKey, object args, string sourceFile, int sourceLine)
        {
            AddRecord("send", channel, eventKey, null, args != null ? args.GetType().Name : null, NormalizeSourceFile(sourceFile), sourceLine);
        }

        private static void OnClear(string channel, string eventKey)
        {
            AddRecord("clear", channel, eventKey, null, null, null, 0);
        }

        private static void AddRecord(string kind, string channel, string eventKey, string handler, string payloadType, string sourceFile, int sourceLine)
        {
            sRecentEvents.Add(new EventRecord(
                kind,
                string.IsNullOrEmpty(channel) ? "Unknown" : channel,
                string.IsNullOrEmpty(eventKey) ? "Unknown" : eventKey,
                handler,
                payloadType,
                sourceFile,
                sourceLine,
                DateTime.Now.ToString("HH:mm:ss.fff")));
            RequestTelemetryFlush();
        }

        private static void RequestTelemetryFlush()
        {
            sFlushGate.Request(sClockSeconds);
        }

        private static bool FlushTelemetryIfDue()
        {
            PublishSnapshot();
            GodotEventStreamWriter.Write("event_update", "{\"event\":\"snapshot_updated\",\"kit\":\"EventKit\"}");
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
            return new EventKitCommandHandler().HandleAction("list_registrations", "{}");
        }

        private static string BuildRecentEventsJson()
        {
            var items = sRecentEvents.Items;
            var sb = new StringBuilder(256);
            sb.Append("{\"events\":[");
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                var record = items[i];
                sb.Append("{\"kind\":\"");
                sb.Append(JsonHelper.EscapeString(record.Kind));
                sb.Append("\",\"channel\":\"");
                sb.Append(JsonHelper.EscapeString(record.Channel));
                sb.Append("\",\"eventKey\":\"");
                sb.Append(JsonHelper.EscapeString(record.EventKey));
                sb.Append("\",\"time\":\"");
                sb.Append(JsonHelper.EscapeString(record.Time));
                sb.Append('"');
                if (!string.IsNullOrEmpty(record.Handler))
                {
                    sb.Append(",\"handler\":\"");
                    sb.Append(JsonHelper.EscapeString(record.Handler));
                    sb.Append('"');
                }
                if (!string.IsNullOrEmpty(record.PayloadType))
                {
                    sb.Append(",\"payloadType\":\"");
                    sb.Append(JsonHelper.EscapeString(record.PayloadType));
                    sb.Append('"');
                }
                if (!string.IsNullOrEmpty(record.SourceFile))
                {
                    sb.Append(",\"sourceFile\":\"");
                    sb.Append(JsonHelper.EscapeString(record.SourceFile));
                    sb.Append('"');
                }
                if (record.SourceLine > 0)
                {
                    sb.Append(",\"sourceLine\":");
                    sb.Append(record.SourceLine);
                }
                sb.Append('}');
            }
            sb.Append("],\"count\":");
            sb.Append(items.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string ResolveHandlerName(Delegate handler)
        {
            if (handler == null)
                return null;

            var declaringType = handler.Method.DeclaringType != null ? handler.Method.DeclaringType.Name : "Unknown";
            return declaringType + "." + handler.Method.Name;
        }

        private static string NormalizeSourceFile(string sourceFile)
        {
            return string.IsNullOrEmpty(sourceFile) ? null : sourceFile.Replace('\\', '/');
        }
    }
}
#endif
