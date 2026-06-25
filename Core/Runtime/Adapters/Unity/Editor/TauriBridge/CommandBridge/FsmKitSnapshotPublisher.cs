#if !GODOT
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YokiFrame;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 将 FsmKit 当前状态发布到 Snapshot 通道。Snapshot 是覆盖写的当前状态，不承担事件历史职责。
    /// </summary>
    internal static class FsmKitSnapshotPublisher
    {
        private const string ENGINE_ID = "unity-editor";
        private const string KIT_NAME = "FsmKit";
        private const string SNAPSHOT_NAME = "state";
        private const double THROTTLED_PUBLISH_INTERVAL_SECONDS = 0.1d;

        private static readonly CommandBridgeSnapshotPublisher sPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, KIT_NAME, SNAPSHOT_NAME, BuildSnapshotPayloadJson);
        private static readonly CommandBridgeTelemetryFlushGate sFlushGate =
            new CommandBridgeTelemetryFlushGate(THROTTLED_PUBLISH_INTERVAL_SECONDS);

        private static bool sUpdateRegistered;
        private static string sPendingYokiframeRoot;

        internal static event Action SnapshotPublished;

        public static void Publish()
        {
            Publish(GetDefaultYokiframeRoot());
        }

        internal static void Publish(string yokiframeRoot)
        {
            Publish(yokiframeRoot, false);
        }

        private static void Publish(string yokiframeRoot, bool notifyPublished)
        {
            if (string.IsNullOrEmpty(yokiframeRoot))
                return;

            ClearPendingPublish();
            var payloadJson = BuildSnapshotPayloadJson();
            sPublisher.Publish(yokiframeRoot, payloadJson);
            UnitySharedMemoryTelemetry.TryWriteLatest(ENGINE_ID, KIT_NAME, SNAPSHOT_NAME, payloadJson);
            if (notifyPublished)
                NotifySnapshotPublished();
        }

        public static void RequestPublish()
        {
            RequestPublish(GetDefaultYokiframeRoot(), EditorApplication.timeSinceStartup, true);
        }

        internal static void RequestPublish(string yokiframeRoot, double nowSeconds)
        {
            RequestPublish(yokiframeRoot, nowSeconds, false);
        }

        private static void RequestPublish(string yokiframeRoot, double nowSeconds, bool registerEditorUpdate)
        {
            if (string.IsNullOrEmpty(yokiframeRoot))
                return;

            sPendingYokiframeRoot = yokiframeRoot;
            sFlushGate.Request(nowSeconds);

            if (!registerEditorUpdate || sUpdateRegistered)
                return;

            EditorApplication.update += FlushPendingFromEditor;
            sUpdateRegistered = true;
        }

        internal static bool TryFlushPending(double nowSeconds)
        {
            if (!sFlushGate.ConsumeIfDue(nowSeconds))
                return false;

            var yokiframeRoot = sPendingYokiframeRoot;
            Publish(yokiframeRoot, true);
            return true;
        }

        internal static void ClearPendingPublish()
        {
            if (sUpdateRegistered)
            {
                EditorApplication.update -= FlushPendingFromEditor;
                sUpdateRegistered = false;
            }

            sFlushGate.Clear();
            sPendingYokiframeRoot = null;
        }

        public static void TryPublish()
        {
            try
            {
                Publish();
            }
            catch (Exception e)
            {
                LogKit.Warning("[FsmKitSnapshotPublisher] 写入 FSM snapshot 失败: " + e.Message);
            }
        }

        private static void FlushPendingFromEditor()
        {
            TryFlushPending(EditorApplication.timeSinceStartup);
        }

        private static void NotifySnapshotPublished()
        {
            try
            {
                SnapshotPublished?.Invoke();
            }
            catch (Exception e)
            {
                LogKit.Warning("[FsmKitSnapshotPublisher] 发布 FSM snapshot 刷新事件失败: " + e.Message);
            }
        }

        private static string BuildSnapshotPayloadJson()
        {
            return new FsmKitCommandHandler().HandleAction("list_all", "{}");
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
