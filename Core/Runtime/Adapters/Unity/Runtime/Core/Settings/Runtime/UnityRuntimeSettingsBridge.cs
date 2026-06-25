#if !GODOT
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity Runtime Settings 与 Base KitSettings 的安装桥。
    /// </summary>
    public static class UnityRuntimeSettingsBridge
    {
        private static YokiFrameRuntimeSettings sSettings;
        private static UnityRuntimeKitSettingsStore sStore;

        /// <summary>
        /// 获取当前安装的 Runtime Settings。
        /// </summary>
        public static YokiFrameRuntimeSettings CurrentSettings
        {
            get
            {
                EnsureInstalled();
                return sSettings;
            }
        }

        /// <summary>
        /// 确保 Runtime Settings 存储已安装到 Base KitSettings。
        /// </summary>
        public static void EnsureInstalled()
        {
            if (sSettings != null && sStore != null)
                return;

            var settings = Application.isEditor
                ? YokiFrameRuntimeSettings.LoadOrCreate()
                : YokiFrameRuntimeSettings.LoadRuntimeOrDefault();
            Install(settings, Application.isEditor);
        }

        /// <summary>
        /// 获取当前 Unity LogKit 配置。
        /// </summary>
        /// <param name="fallback">当前设置不可用时使用的回退配置。</param>
        /// <returns>当前 Unity LogKit 配置副本。</returns>
        public static UnityLogKitOptions GetLogKitOptions(UnityLogKitOptions fallback)
        {
            EnsureInstalled();
            if (sSettings != null)
                return sSettings.CreateLogKitOptions();

            return fallback != null ? fallback.Clone() : UnityLogKitOptions.CreateDefault();
        }

        /// <summary>
        /// 将当前 Runtime Settings 应用到运行时 LogKit。
        /// </summary>
        public static void ApplyCurrentRuntimeSettings()
        {
            if (sSettings == null)
                return;

            var logger = LogKit.GetLogger();
            UnityLogKitRuntimeInstaller.Install(sSettings.CreateLogKitOptions(), logger);
        }

        internal static void Install(YokiFrameRuntimeSettings settings, bool saveAssets)
        {
            if (settings == null)
                settings = YokiFrameRuntimeSettings.LoadRuntimeOrDefault();

            sSettings = settings;
            sSettings.Normalize();
            sStore = new(sSettings, saveAssets, ApplyCurrentRuntimeSettings);
            KitSettings.SetStore(sStore);
            LogKitSettings.ApplyBaseRuntimeSettings();
        }
    }
}
#endif
