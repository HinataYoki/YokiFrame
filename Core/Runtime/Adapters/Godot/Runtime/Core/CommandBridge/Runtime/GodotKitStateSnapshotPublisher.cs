#if GODOT
using System;
using System.Text;
using Godot;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot 侧通用 Kit 快照发布器。
    /// 这些 Kit 没有专用运行时 hook，因此统一在 Adapter Tick 中节流发布，避免 Kit 热路径写文件。
    /// </summary>
    public static class GodotKitStateSnapshotPublisher
    {
        private const string ENGINE_ID = "godot-runtime";
        private const string SNAPSHOT_NAME = "state";
        private const string POOL_KIT_NAME = "PoolKit";
        private const string RES_KIT_NAME = "ResKit";
        private const string SINGLETON_KIT_NAME = "SingletonKit";
        private const string AUDIO_KIT_NAME = "AudioKit";
        private const string LOG_KIT_NAME = "LogKit";
        private const string SAVE_KIT_NAME = "SaveKit";
        private const string SAVE_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.SaveKitCommandHandler, YokiFrame.SaveKit";
        private const string LOCALIZATION_KIT_NAME = "LocalizationKit";
        private const string LOCALIZATION_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.LocalizationKitCommandHandler, YokiFrame.LocalizationKit";
        private const string SCENE_KIT_NAME = "SceneKit";
        private const string SCENE_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.SceneKitCommandHandler, YokiFrame.SceneKit";
        private const string SPATIAL_KIT_NAME = "SpatialKit";
        private const string SPATIAL_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.SpatialKitCommandHandler, YokiFrame.SpatialKit";
        private const string ACTION_KIT_NAME = "ActionKit";
        private const string ACTION_KIT_COMMAND_HANDLER_TYPE = "YokiFrame.ActionKitCommandHandler, YokiFrame.ActionKit";
        private const double POOL_PUBLISH_INTERVAL_SECONDS = 0.05d;
        private const double DEFAULT_PUBLISH_INTERVAL_SECONDS = 0.2d;

        private static readonly PoolKitCommandHandler sPoolHandler = new PoolKitCommandHandler();
        private static readonly ResKitCommandHandler sResHandler = new ResKitCommandHandler();
        private static readonly SingletonKitCommandHandler sSingletonHandler = new SingletonKitCommandHandler();
        private static readonly AudioKitCommandHandler sAudioHandler = new AudioKitCommandHandler();
        private static readonly LogKitCommandHandler sLogHandler = new LogKitCommandHandler();
        private static IKitCommandHandler sSaveHandler;
        private static IKitCommandHandler sLocalizationHandler;
        private static IKitCommandHandler sSceneHandler;
        private static IKitCommandHandler sSpatialHandler;
        private static IKitCommandHandler sActionHandler;

        private static readonly CommandBridgeSnapshotPublisher sPoolPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, POOL_KIT_NAME, SNAPSHOT_NAME, BuildPoolPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sResPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, RES_KIT_NAME, SNAPSHOT_NAME, BuildResPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sSingletonPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, SINGLETON_KIT_NAME, SNAPSHOT_NAME, BuildSingletonPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sAudioPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, AUDIO_KIT_NAME, SNAPSHOT_NAME, BuildAudioPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sLogPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, LOG_KIT_NAME, SNAPSHOT_NAME, BuildLogPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sSavePublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, SAVE_KIT_NAME, SNAPSHOT_NAME, BuildSavePayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sLocalizationPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, LOCALIZATION_KIT_NAME, SNAPSHOT_NAME, BuildLocalizationPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sScenePublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, SCENE_KIT_NAME, SNAPSHOT_NAME, BuildScenePayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sSpatialPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, SPATIAL_KIT_NAME, SNAPSHOT_NAME, BuildSpatialPayloadJson);
        private static readonly CommandBridgeSnapshotPublisher sActionPublisher =
            new CommandBridgeSnapshotPublisher(ENGINE_ID, ACTION_KIT_NAME, SNAPSHOT_NAME, BuildActionPayloadJson);

        private static string sYokiframeRoot;
        private static double sClockSeconds;
        private static double sLastPoolPublishSeconds = -POOL_PUBLISH_INTERVAL_SECONDS;
        private static double sLastDefaultPublishSeconds = -DEFAULT_PUBLISH_INTERVAL_SECONDS;
        private static string sLastPoolPayloadJson;
        private static string sLastPoolInvalidationKey;
        private static string sLastResPayloadJson;
        private static string sLastResInvalidationKey;
        private static string sLastSingletonPayloadJson;
        private static string sLastSingletonInvalidationKey;
        private static string sLastAudioPayloadJson;
        private static string sLastAudioInvalidationKey;
        private static string sLastLogPayloadJson;
        private static string sLastLogInvalidationKey;
        private static string sLastSavePayloadJson;
        private static string sLastSaveInvalidationKey;
        private static string sLastLocalizationPayloadJson;
        private static string sLastLocalizationInvalidationKey;
        private static string sLastScenePayloadJson;
        private static string sLastSceneInvalidationKey;
        private static string sLastSpatialPayloadJson;
        private static string sLastSpatialInvalidationKey;
        private static string sLastActionPayloadJson;
        private static string sLastActionInvalidationKey;

        public static void Init(string yokiframeRoot)
        {
            sYokiframeRoot = yokiframeRoot;
            sClockSeconds = 0d;
            sLastPoolPublishSeconds = -POOL_PUBLISH_INTERVAL_SECONDS;
            sLastDefaultPublishSeconds = -DEFAULT_PUBLISH_INTERVAL_SECONDS;
            ForcePublishAll();
        }

        public static void Tick(double delta)
        {
            if (string.IsNullOrEmpty(sYokiframeRoot))
                return;

            sClockSeconds += delta;
            if (sClockSeconds - sLastPoolPublishSeconds >= POOL_PUBLISH_INTERVAL_SECONDS)
            {
                sLastPoolPublishSeconds = sClockSeconds;
                PublishPoolIfChanged(false);
            }

            if (sClockSeconds - sLastDefaultPublishSeconds >= DEFAULT_PUBLISH_INTERVAL_SECONDS)
            {
                sLastDefaultPublishSeconds = sClockSeconds;
                PublishNonPoolIfChanged(false);
            }
        }

        public static void ForcePublishAll()
        {
            PublishPoolIfChanged(true);
            PublishNonPoolIfChanged(true);
        }

        private static void PublishPoolIfChanged(bool force)
        {
            PublishIfInvalidated(POOL_KIT_NAME, sPoolPublisher, BuildPoolPayloadJson, sPoolHandler, ref sLastPoolPayloadJson, ref sLastPoolInvalidationKey, force, PushPoolSnapshotUpdatedEvent);
        }

        private static void PublishNonPoolIfChanged(bool force)
        {
            PublishIfInvalidated(RES_KIT_NAME, sResPublisher, BuildResPayloadJson, sResHandler, ref sLastResPayloadJson, ref sLastResInvalidationKey, force, PushResSnapshotUpdatedEvent);
            PublishIfInvalidated(SINGLETON_KIT_NAME, sSingletonPublisher, BuildSingletonPayloadJson, sSingletonHandler, ref sLastSingletonPayloadJson, ref sLastSingletonInvalidationKey, force, PushSingletonSnapshotUpdatedEvent);
            PublishIfInvalidated(AUDIO_KIT_NAME, sAudioPublisher, BuildAudioPayloadJson, sAudioHandler, ref sLastAudioPayloadJson, ref sLastAudioInvalidationKey, force, PushAudioSnapshotUpdatedEvent);
            PublishIfInvalidated(LOG_KIT_NAME, sLogPublisher, BuildLogPayloadJson, sLogHandler, ref sLastLogPayloadJson, ref sLastLogInvalidationKey, force, PushLogSnapshotUpdatedEvent);
            PublishOptionalIfInvalidated(SAVE_KIT_NAME, sSavePublisher, EnsureSaveHandler, () => sSaveHandler, BuildSavePayloadJson, ref sLastSavePayloadJson, ref sLastSaveInvalidationKey, force, PushSaveSnapshotUpdatedEvent);
            PublishOptionalIfInvalidated(LOCALIZATION_KIT_NAME, sLocalizationPublisher, EnsureLocalizationHandler, () => sLocalizationHandler, BuildLocalizationPayloadJson, ref sLastLocalizationPayloadJson, ref sLastLocalizationInvalidationKey, force, PushLocalizationSnapshotUpdatedEvent);
            PublishOptionalIfInvalidated(SCENE_KIT_NAME, sScenePublisher, EnsureSceneHandler, () => sSceneHandler, BuildScenePayloadJson, ref sLastScenePayloadJson, ref sLastSceneInvalidationKey, force, PushSceneSnapshotUpdatedEvent);
            PublishOptionalIfInvalidated(SPATIAL_KIT_NAME, sSpatialPublisher, EnsureSpatialHandler, () => sSpatialHandler, BuildSpatialPayloadJson, ref sLastSpatialPayloadJson, ref sLastSpatialInvalidationKey, force, PushSpatialSnapshotUpdatedEvent);
            PublishOptionalIfInvalidated(ACTION_KIT_NAME, sActionPublisher, EnsureActionHandler, () => sActionHandler, BuildActionPayloadJson, ref sLastActionPayloadJson, ref sLastActionInvalidationKey, force, PushActionSnapshotUpdatedEvent);
        }

        private static void PublishIfChanged(
            string kitName,
            CommandBridgeSnapshotPublisher publisher,
            Func<string> payloadFactory,
            ref string lastPayloadJson,
            bool force,
            Action pushSnapshotUpdatedEvent)
        {
            if (string.IsNullOrEmpty(sYokiframeRoot))
                return;

            try
            {
                var payloadJson = payloadFactory();
                if (!force && string.Equals(payloadJson, lastPayloadJson, StringComparison.Ordinal))
                    return;

                publisher.Publish(sYokiframeRoot, payloadJson);
                AdapterSharedMemoryTelemetry.TryWriteLatest(ENGINE_ID, kitName, SNAPSHOT_NAME, payloadJson);
                pushSnapshotUpdatedEvent();
                lastPayloadJson = payloadJson;
            }
            catch (Exception e)
            {
                GD.PushWarning("[YokiFrame][GodotKitStateSnapshotPublisher] 写入 " + kitName + " snapshot 失败: " + e.Message);
            }
        }

        private static void PublishIfInvalidated(
            string kitName,
            CommandBridgeSnapshotPublisher publisher,
            Func<string> payloadFactory,
            IKitSnapshotInvalidationProvider invalidationProvider,
            ref string lastPayloadJson,
            ref string lastInvalidationKey,
            bool force,
            Action pushSnapshotUpdatedEvent)
        {
            if (invalidationProvider == null)
            {
                PublishIfChanged(kitName, publisher, payloadFactory, ref lastPayloadJson, force, pushSnapshotUpdatedEvent);
                return;
            }

            if (string.IsNullOrEmpty(sYokiframeRoot))
                return;

            try
            {
                var invalidationKey = invalidationProvider.GetSnapshotInvalidationKey();
                if (!force && string.Equals(invalidationKey, lastInvalidationKey, StringComparison.Ordinal))
                    return;

                var payloadJson = payloadFactory();
                if (!force && string.Equals(payloadJson, lastPayloadJson, StringComparison.Ordinal))
                {
                    lastInvalidationKey = invalidationKey;
                    return;
                }

                publisher.Publish(sYokiframeRoot, payloadJson);
                AdapterSharedMemoryTelemetry.TryWriteLatest(ENGINE_ID, kitName, SNAPSHOT_NAME, payloadJson);
                pushSnapshotUpdatedEvent();
                lastPayloadJson = payloadJson;
                lastInvalidationKey = invalidationKey;
            }
            catch (Exception e)
            {
                GD.PushWarning("[YokiFrame][GodotKitStateSnapshotPublisher] 写入 " + kitName + " snapshot 失败: " + e.Message);
            }
        }

        private static void PublishOptionalIfChanged(
            string kitName,
            CommandBridgeSnapshotPublisher publisher,
            Func<bool> ensureHandler,
            Func<string> payloadFactory,
            ref string lastPayloadJson,
            bool force,
            Action pushSnapshotUpdatedEvent)
        {
            if (!ensureHandler())
                return;

            PublishIfChanged(kitName, publisher, payloadFactory, ref lastPayloadJson, force, pushSnapshotUpdatedEvent);
        }

        private static void PublishOptionalIfInvalidated(
            string kitName,
            CommandBridgeSnapshotPublisher publisher,
            Func<bool> ensureHandler,
            Func<IKitCommandHandler> getHandler,
            Func<string> payloadFactory,
            ref string lastPayloadJson,
            ref string lastInvalidationKey,
            bool force,
            Action pushSnapshotUpdatedEvent)
        {
            if (!ensureHandler())
                return;

            PublishIfInvalidated(
                kitName,
                publisher,
                payloadFactory,
                getHandler() as IKitSnapshotInvalidationProvider,
                ref lastPayloadJson,
                ref lastInvalidationKey,
                force,
                pushSnapshotUpdatedEvent);
        }

        private static bool EnsureSaveHandler()
        {
            if (sSaveHandler != null)
                return true;

            // SaveKit 可独立于 Godot Adapter 安装；这里复用 Base helper，避免每个宿主重复写可选 Kit 反射胶水。
            return OptionalKitCommandHandlerRegistry.TryCreate(SAVE_KIT_COMMAND_HANDLER_TYPE, out sSaveHandler);
        }

        private static bool EnsureLocalizationHandler()
        {
            if (sLocalizationHandler != null)
                return true;

            // LocalizationKit 可独立于 Godot Adapter 安装；通过同一个 optional handler helper 接入。
            return OptionalKitCommandHandlerRegistry.TryCreate(LOCALIZATION_KIT_COMMAND_HANDLER_TYPE, out sLocalizationHandler);
        }

        private static bool EnsureSceneHandler()
        {
            if (sSceneHandler != null)
                return true;

            // SceneKit 可独立于 Godot Adapter 安装；继续复用 Base optional helper，不让宿主重复反射胶水。
            return OptionalKitCommandHandlerRegistry.TryCreate(SCENE_KIT_COMMAND_HANDLER_TYPE, out sSceneHandler);
        }

        private static bool EnsureSpatialHandler()
        {
            if (sSpatialHandler != null)
                return true;

            // SpatialKit 可独立于 Godot Adapter 安装；通过 Base optional helper 接入统一 snapshot 链路。
            return OptionalKitCommandHandlerRegistry.TryCreate(SPATIAL_KIT_COMMAND_HANDLER_TYPE, out sSpatialHandler);
        }

        private static bool EnsureActionHandler()
        {
            if (sActionHandler != null)
                return true;

            return OptionalKitCommandHandlerRegistry.TryCreate(ACTION_KIT_COMMAND_HANDLER_TYPE, out sActionHandler);
        }

        private static void PushPoolSnapshotUpdatedEvent()
        {
            GodotEventStreamWriter.Write("pool_update", "{\"event\":\"snapshot_updated\",\"kit\":\"PoolKit\"}");
        }

        private static void PushResSnapshotUpdatedEvent()
        {
            GodotEventStreamWriter.Write("res_update", "{\"event\":\"snapshot_updated\",\"kit\":\"ResKit\"}");
        }

        private static void PushSingletonSnapshotUpdatedEvent()
        {
            GodotEventStreamWriter.Write("singleton_update", "{\"event\":\"snapshot_updated\",\"kit\":\"SingletonKit\"}");
        }

        private static void PushAudioSnapshotUpdatedEvent()
        {
            GodotEventStreamWriter.Write("audio_update", "{\"event\":\"snapshot_updated\",\"kit\":\"AudioKit\"}");
        }

        private static void PushLogSnapshotUpdatedEvent()
        {
            GodotEventStreamWriter.Write("log_update", "{\"event\":\"snapshot_updated\",\"kit\":\"LogKit\"}");
        }

        private static void PushSaveSnapshotUpdatedEvent()
        {
            GodotEventStreamWriter.Write("save_update", "{\"event\":\"snapshot_updated\",\"kit\":\"SaveKit\"}");
        }

        private static void PushLocalizationSnapshotUpdatedEvent()
        {
            GodotEventStreamWriter.Write("localization_update", "{\"event\":\"snapshot_updated\",\"kit\":\"LocalizationKit\"}");
        }

        private static void PushSceneSnapshotUpdatedEvent()
        {
            GodotEventStreamWriter.Write("scene_update", "{\"event\":\"snapshot_updated\",\"kit\":\"SceneKit\"}");
        }

        private static void PushSpatialSnapshotUpdatedEvent()
        {
            GodotEventStreamWriter.Write("spatial_update", "{\"event\":\"snapshot_updated\",\"kit\":\"SpatialKit\"}");
        }

        private static void PushActionSnapshotUpdatedEvent()
        {
            GodotEventStreamWriter.Write("action_update", "{\"event\":\"snapshot_updated\",\"kit\":\"ActionKit\"}");
        }

        private static string BuildPoolPayloadJson()
        {
            return sPoolHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildResPayloadJson()
        {
            return sResHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildSingletonPayloadJson()
        {
            return sSingletonHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildAudioPayloadJson()
        {
            var stats = sAudioHandler.HandleAction("stats", "{}");
            var voices = sAudioHandler.HandleAction("list_voices", "{}");
            var history = sAudioHandler.HandleAction("get_history", "{}");

            var sb = new StringBuilder(stats.Length + voices.Length + history.Length + 56);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"voices\":");
            sb.Append(voices);
            sb.Append(",\"history\":");
            sb.Append(history);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildLogPayloadJson()
        {
            return LogKitCommandHandler.BuildSnapshotState();
        }

        private static string BuildSavePayloadJson()
        {
            if (!EnsureSaveHandler())
                return "{}";

            return sSaveHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildLocalizationPayloadJson()
        {
            if (!EnsureLocalizationHandler())
                return "{}";

            return sLocalizationHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildScenePayloadJson()
        {
            if (!EnsureSceneHandler())
                return "{}";

            return sSceneHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildSpatialPayloadJson()
        {
            if (!EnsureSpatialHandler())
                return "{}";

            return sSpatialHandler.HandleAction("get_workbench_snapshot", "{}");
        }

        private static string BuildActionPayloadJson()
        {
            if (!EnsureActionHandler())
                return "{}";

            return sActionHandler.HandleAction("get_workbench_snapshot", "{}");
        }
    }
}
#endif
