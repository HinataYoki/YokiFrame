#if UNITY_5_3_OR_NEWER
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 安装 Unity 侧 LogKit 后端，使 Core LogKit 输出进入 Unity Console。
    /// </summary>
    public static class UnityLogKitRuntimeInstaller
    {
        private static readonly UnityEngineLogger sDefaultLogger = new UnityEngineLogger();
        private static IEngineLogger sInstalledLogger;

        /// <summary>
        /// 获取 Unity LogKit 适配层当前是否由本安装器持有。
        /// </summary>
        public static bool IsInstalled
        {
            get { return sInstalledLogger != null && ReferenceEquals(LogKit.GetLogger(), sInstalledLogger); }
        }

        /// <summary>
        /// 注册 Unity 默认 logger 和 Runtime Settings 工厂；实际对象创建延迟到 Kit 首次访问。
        /// </summary>
        public static void RegisterDefaultFactories()
        {
            KitSettings.RegisterDefaultStoreFactory(CreateDefaultSettingsStore);
            LogKit.RegisterDefaultLoggerFactory(CreateDefaultLogger);
            LogKitSettings.RuntimeSettingsApplied -= ApplyPlayerOverlaySettings;
            LogKitSettings.RuntimeSettingsApplied += ApplyPlayerOverlaySettings;
        }

        /// <summary>
        /// 在新 Unity 子系统会话中清理静态状态并重新注册惰性宿主工厂。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAndRegisterDefaultFactories()
        {
            UnityLogKitPlayerOverlay.Reset();
            KitSettings.Reset();
            LogKit.Reset();
            RegisterDefaultFactories();
        }

        /// <summary>
        /// 安装默认 UnityEngine.Debug 日志后端。
        /// </summary>
        public static void Install()
        {
            Install(sDefaultLogger);
        }

        /// <summary>
        /// 安装指定日志后端；传入 null 时使用默认 UnityEngine.Debug 后端。
        /// </summary>
        /// <param name="logger">要安装的日志后端。</param>
        public static void Install(IEngineLogger logger)
        {
            IEngineLogger finalLogger = logger ?? sDefaultLogger;
            sInstalledLogger = finalLogger;
            LogKit.SetLogger(finalLogger);
            LogKitSettings.ApplyBaseRuntimeSettings();
        }

        /// <summary>
        /// 关闭由本安装器注入的日志后端；如果外部已替换后端，则只清理安装器状态。
        /// </summary>
        public static void Shutdown()
        {
            LogKitSettings.RuntimeSettingsApplied -= ApplyPlayerOverlaySettings;
            UnityLogKitPlayerOverlay.Reset();
            if (sInstalledLogger != null && ReferenceEquals(LogKit.GetLogger(), sInstalledLogger))
            {
                LogKit.ClearLogger();
            }

            sInstalledLogger = null;
        }

        /// <summary>创建 Unity 默认日志后端；Editor 工具环境由独立 Editor Adapter 管理。</summary>
        private static IEngineLogger CreateDefaultLogger()
        {
            sInstalledLogger = sDefaultLogger;
            return sDefaultLogger;
        }

        /// <summary>
        /// 在 Core 完成 Runtime Settings 同步后，按当前配置创建、更新或销毁 Unity Player 调试覆盖层。
        /// </summary>
        private static void ApplyPlayerOverlaySettings()
        {
            UnityLogKitPlayerOverlay.ApplySettings(
                LogKitSettings.GetBool(
                    LogKitSettings.ENABLE_IMGUI_IN_PLAYER_KEY,
                    LogKitSettings.DEFAULT_ENABLE_IMGUI_IN_PLAYER),
                LogKitSettings.GetInt(
                    LogKitSettings.IMGUI_MAX_LOG_COUNT_KEY,
                    LogKitSettings.DEFAULT_IMGUI_MAX_LOG_COUNT));
        }

        /// <summary>加载当前 Unity Resources Runtime Settings，损坏时只报告诊断并回退空 Store。</summary>
        private static IKitSettingsStore CreateDefaultSettingsStore()
        {
            bool loaded = UnityYokiFrameRuntimeSettingsLoader.TryLoad(out YokiFrameRuntimeSettingsStore store, out string errorMessage);
            if (!loaded && !string.IsNullOrWhiteSpace(errorMessage))
            {
                UnityEngine.Debug.LogWarning(errorMessage);
            }

            return store;
        }
    }
}
#endif
