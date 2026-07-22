#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using YokiFrame.Unity;

namespace YokiFrame
{
    /// <summary>
    /// Unity Editor 域加载时注册 Runtime Settings 与 LogKit 默认工厂，避免编辑器代码依赖手工初始化入口。
    /// </summary>
    internal static class UnityYokiFrameEditorAdapterInstaller
    {
        /// <summary>在 Unity Editor 域加载完成后注册惰性工厂，并建立不创建 logger 的工具环境。</summary>
        [InitializeOnLoadMethod]
        private static void RegisterDefaultFactories()
        {
            UnityLogKitRuntimeInstaller.RegisterDefaultFactories();
            ConfigureLogKitEnvironment();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// 配置 Unity Editor 的 LogKit 文件位置和真实工具能力；该操作不创建宿主 logger。
        /// </summary>
        internal static void ConfigureLogKitEnvironment()
        {
            LogKitHostEnvironment.Configure(
                Path.Combine(Application.persistentDataPath, "LogFiles"),
                true,
                true,
                false,
                true,
                false);
        }

        /// <summary>
        /// 在 PlayMode 或 EditMode 建立完成后重新绑定惰性工厂和环境，兼容关闭 Domain Reload 的项目。
        /// </summary>
        /// <param name="state">Unity 当前完成切换后的模式。</param>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode
                && state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            UnityLogKitRuntimeInstaller.RegisterDefaultFactories();
            ConfigureLogKitEnvironment();
        }
    }
}
#endif
