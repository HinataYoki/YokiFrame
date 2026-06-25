#if !GODOT
#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// EventKit 编辑器数据桥：把运行时 EventKit hook 转换为 snapshot 与 event_update。
    /// </summary>
    [InitializeOnLoad]
    internal static class EventKitEditorDataBridge
    {
        private const string ENGINE_ID = "unity-editor";
        private const string KIT_NAME = "EventKit";
        private const string SNAPSHOT_NAME = "state";
        private const int MAX_RECENT_EVENTS = 160;
        private const double FLUSH_INTERVAL_SECONDS = 0.1d;

        private static readonly CommandBridgeTelemetryBuffer<EventRecord> sRecentEvents =
            new CommandBridgeTelemetryBuffer<EventRecord>(MAX_RECENT_EVENTS);
        private static readonly CommandBridgeTelemetryFlushGate sFlushGate =
            new CommandBridgeTelemetryFlushGate(FLUSH_INTERVAL_SECONDS);
        private static readonly CommandBridgeSnapshotPublisher sSnapshotPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, KIT_NAME, SNAPSHOT_NAME, BuildSnapshotPayloadJson);

        private static bool sUpdateRegistered;
        private static string sPendingYokiframeRoot;
        private static EventRecord sLatestEvent;
        private static bool sHasLatestEvent;

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

        static EventKitEditorDataBridge()
        {
            EasyEventEditorHook.OnRegister -= OnRegister;
            EasyEventEditorHook.OnRegister += OnRegister;
            EasyEventEditorHook.OnUnRegister -= OnUnRegister;
            EasyEventEditorHook.OnUnRegister += OnUnRegister;
            EasyEventEditorHook.OnSend -= OnSend;
            EasyEventEditorHook.OnSend += OnSend;
            EasyEventEditorHook.OnClear -= OnClear;
            EasyEventEditorHook.OnClear += OnClear;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EventKitCommandHandler.RecentEventsProvider = BuildRecentEventsJson;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                sRecentEvents.Clear();
                sHasLatestEvent = false;
                PublishNow();
                PushSnapshotUpdatedEvent();
            }
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
            sLatestEvent = new EventRecord(
                kind,
                string.IsNullOrEmpty(channel) ? "Unknown" : channel,
                string.IsNullOrEmpty(eventKey) ? "Unknown" : eventKey,
                handler,
                payloadType,
                sourceFile,
                sourceLine,
                DateTime.Now.ToString("HH:mm:ss.fff"));
            sHasLatestEvent = true;
            sRecentEvents.Add(sLatestEvent);

            RequestPublish();
        }

        private static void RequestPublish()
        {
            var yokiframeRoot = GetDefaultYokiframeRoot();
            if (string.IsNullOrEmpty(yokiframeRoot))
                return;

            sPendingYokiframeRoot = yokiframeRoot;
            sFlushGate.Request(EditorApplication.timeSinceStartup);
            if (sUpdateRegistered)
                return;

            EditorApplication.update += FlushPendingFromEditor;
            sUpdateRegistered = true;
        }

        private static void FlushPendingFromEditor()
        {
            if (!sFlushGate.ConsumeIfDue(EditorApplication.timeSinceStartup))
                return;

            if (sUpdateRegistered)
            {
                EditorApplication.update -= FlushPendingFromEditor;
                sUpdateRegistered = false;
            }

            Publish(sPendingYokiframeRoot);
            sPendingYokiframeRoot = null;
            PushSnapshotUpdatedEvent();
        }

        private static void PublishNow()
        {
            Publish(GetDefaultYokiframeRoot());
        }

        private static void Publish(string yokiframeRoot)
        {
            if (string.IsNullOrEmpty(yokiframeRoot))
                return;

            try
            {
                sSnapshotPublisher.Publish(yokiframeRoot);
            }
            catch (Exception e)
            {
                LogKit.Warning("[EventKitEditorDataBridge] 写入 EventKit snapshot 失败: " + e.Message);
            }
        }

        private static string BuildSnapshotPayloadJson()
        {
            return new EventKitCommandHandler().HandleAction("list_registrations", "{}");
        }

        private static void PushSnapshotUpdatedEvent()
        {
            var sb = new StringBuilder(192);
            sb.Append("{\"event\":\"snapshot_updated\",\"kit\":\"EventKit\"");
            if (sHasLatestEvent)
            {
                sb.Append(",\"record\":");
                AppendEventRecordJson(sb, sLatestEvent);
            }
            sb.Append('}');
            UnityEventStreamWriter.Write("event_update", sb.ToString());
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

                AppendEventRecordJson(sb, items[i]);
            }
            sb.Append("],\"count\":");
            sb.Append(items.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendEventRecordJson(StringBuilder sb, EventRecord record)
        {
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

        private static string ResolveHandlerName(Delegate handler)
        {
            if (handler == null)
                return null;

            var declaringType = handler.Method.DeclaringType != null ? handler.Method.DeclaringType.Name : "Unknown";
            return declaringType + "." + handler.Method.Name;
        }

        private static string NormalizeSourceFile(string sourceFile)
        {
            if (string.IsNullOrEmpty(sourceFile))
                return null;

            var normalized = sourceFile.Replace('\\', '/');
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                return normalized;

            var normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
            if (normalized.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(normalizedRoot.Length + 1);

            return normalized;
        }

        private static string GetDefaultYokiframeRoot()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return string.IsNullOrEmpty(projectRoot) ? null : Path.Combine(projectRoot, ".yokiframe");
        }
    }
}
#endif
#endif
