#if !GODOT
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity LogKit 运行时安装器。对外仍使用 Base 的 LogKit API，Unity 专属后端在这里接入。
    /// </summary>
    public static class UnityLogKitRuntimeInstaller
    {
        private static IEngineLogger sLogger;
        private static bool sInstalled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInstall()
        {
            EnsureLoggerAdapterRegistered();
            UnityRuntimeSettingsBridge.EnsureInstalled();
            Install(UnityRuntimeSettingsBridge.GetLogKitOptions(UnityLogKitOptions.CreateDefault()), null);
        }

        internal static void EnsureLoggerAdapterRegistered()
        {
            LogKit.SetLoggerAdapter(UnityEngineLogger.WrapLegacyLogger);
        }

        /// <summary>
        /// 安装 Unity LogKit 后端并按运行时配置启用文件写入或 IMGUI 面板。
        /// </summary>
        /// <param name="options">Unity LogKit 运行时配置；传入 null 时使用默认配置。</param>
        /// <param name="logger">日志后端；传入 null 时复用已有后端或创建 UnityEngineLogger。</param>
        public static void Install(UnityLogKitOptions options, IEngineLogger logger)
        {
            EnsureLoggerAdapterRegistered();
            var finalLogger = logger ?? sLogger ?? new UnityEngineLogger();
            sLogger = finalLogger;
            LogKit.SetLogger(finalLogger);
            LogKitSettings.ApplyBaseRuntimeSettings();

            var finalOptions = options != null ? options : UnityLogKitOptions.CreateDefault();
            finalOptions.Normalize();

            if (finalOptions.ShouldSaveForCurrentRuntime())
                UnityLogKitFileWriter.Initialize(finalOptions);
            else
                UnityLogKitFileWriter.Shutdown();

            if (!Application.isEditor && finalOptions.EnableIMGUIInPlayer)
                UnityLogKitIMGUIConsole.Enable(finalOptions.IMGUIMaxLogCount);
            else if (Application.isEditor || !finalOptions.EnableIMGUIInPlayer)
                UnityLogKitIMGUIConsole.Disable();

            sInstalled = true;
        }

        /// <summary>
        /// 关闭 Unity LogKit 后端并释放文件写入器和 IMGUI 面板。
        /// </summary>
        public static void Shutdown()
        {
            UnityLogKitFileWriter.Shutdown();
            UnityLogKitIMGUIConsole.Disable();
            if (sInstalled && ReferenceEquals(LogKit.GetLogger(), sLogger))
                LogKit.ClearLogger();

            sInstalled = false;
            sLogger = null;
        }
    }
}
#endif
