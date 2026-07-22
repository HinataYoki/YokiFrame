#if GODOT
using System.Runtime.CompilerServices;
using Godot;

#pragma warning disable CA2255 // Godot 模块初始化只注册惰性 logger 工厂。

namespace YokiFrame
{
    /// <summary>
    /// 安装 Godot 侧 LogKit 后端，使 Core LogKit 输出进入 Godot 控制台。
    /// </summary>
    public static class GodotLogKitRuntimeInstaller
    {
        private static readonly GodotEngineLogger sDefaultLogger = new GodotEngineLogger();
        private static IEngineLogger sInstalledLogger;

        /// <summary>
        /// 获取 Godot LogKit 适配层当前是否由本安装器持有。
        /// </summary>
        public static bool IsInstalled
        {
            get { return sInstalledLogger != null && ReferenceEquals(LogKit.GetLogger(), sInstalledLogger); }
        }

        /// <summary>模块加载时注册默认 logger 工厂，实际 logger 创建延迟到首次写日志。</summary>
        [ModuleInitializer]
        internal static void RegisterDefaultLoggerFactory()
        {
            EnsureInstalled();
        }

        /// <summary>供 Godot Bootstrap 重新确认默认 logger 工厂，避免覆盖显式 logger。</summary>
        public static void EnsureInstalled()
        {
            LogKit.RegisterDefaultLoggerFactory(CreateDefaultLogger);
            LogKitSettings.RuntimeSettingsApplied -= ApplyPlayerOverlaySettings;
            LogKitSettings.RuntimeSettingsApplied += ApplyPlayerOverlaySettings;
        }

        /// <summary>
        /// 由 Godot Bootstrap 在进入场景树后提供覆盖层挂载点；未启用设置时不会创建任何 UI 节点。
        /// </summary>
        /// <param name="host">当前活跃的 Godot Runtime Bootstrap。</param>
        internal static void AttachPlayerOverlay(Node host)
        {
            GodotLogKitPlayerOverlay.Attach(host);
        }

        /// <summary>
        /// 安装默认 Godot GD 日志后端。
        /// </summary>
        public static void Install()
        {
            Install(sDefaultLogger);
        }

        /// <summary>
        /// 安装指定日志后端；传入 null 时使用默认 Godot GD 后端。
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
            GodotLogKitPlayerOverlay.Reset();
            if (sInstalledLogger != null && ReferenceEquals(LogKit.GetLogger(), sInstalledLogger))
            {
                LogKit.ClearLogger();
            }

            sInstalledLogger = null;
        }

        /// <summary>创建 Godot 默认日志后端；Tools 环境由 Godot Bootstrap 在工具构建中管理。</summary>
        private static IEngineLogger CreateDefaultLogger()
        {
            sInstalledLogger = sDefaultLogger;
            return sDefaultLogger;
        }

        /// <summary>
        /// 在 Core 完成 Runtime Settings 同步后，按当前配置创建、更新或销毁 Godot Player 调试覆盖层。
        /// </summary>
        private static void ApplyPlayerOverlaySettings()
        {
            GodotLogKitPlayerOverlay.ApplySettings(
                LogKitSettings.GetBool(
                    LogKitSettings.ENABLE_IMGUI_IN_PLAYER_KEY,
                    LogKitSettings.DEFAULT_ENABLE_IMGUI_IN_PLAYER),
                LogKitSettings.GetInt(
                    LogKitSettings.IMGUI_MAX_LOG_COUNT_KEY,
                    LogKitSettings.DEFAULT_IMGUI_MAX_LOG_COUNT));
        }
    }
}
#pragma warning restore CA2255
#endif
