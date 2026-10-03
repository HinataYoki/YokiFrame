#if GODOT
using System.Runtime.CompilerServices;

#pragma warning disable CA2255 // Godot 模块初始化只注册惰性工厂，不创建宿主对象。
namespace YokiFrame
{
    /// <summary>
    /// 注册 Godot ProjectSettings Runtime Store 工厂；首次 Kit Settings 访问时才读取当前项目配置。
    /// </summary>
    internal static class GodotYokiFrameRuntimeSettingsInstaller
    {
        /// <summary>模块加载时注册默认 Store 工厂，不触碰 Godot ProjectSettings。</summary>
        [ModuleInitializer]
        internal static void RegisterDefaultStoreFactory()
        {
            EnsureInstalled();
        }

        /// <summary>供 Godot Bootstrap 在场景树进入时重新确认默认 Store 工厂。</summary>
        internal static void EnsureInstalled()
        {
            YokiFrameSession.Register(
                YokiFrameSession.RESTORE_FACTORIES_ORDER,
                "godot-settings-factory",
                RegisterStoreFactory);
            RegisterStoreFactory();
        }

        /// <summary>重新登记设置 Store 工厂。会话重置会清掉上一轮工厂。</summary>
        private static void RegisterStoreFactory()
        {
            KitSettings.RegisterDefaultStoreFactory(CreateStore);
        }

        /// <summary>
        /// 创建当前项目的 Runtime Store。Tools 构建再由已注册的 Editor 回调叠加编辑器配置。
        /// </summary>
        /// <returns>仅包含当前项目覆盖值的 Store。</returns>
        private static YokiFrameRuntimeSettingsStore CreateStore()
        {
            YokiFrameRuntimeSettingsStore store = GodotYokiFrameRuntimeSettingsLoader.Load();
#if GODOT && TOOLS
            string errorMessage;
            if (!GodotYokiFrameEditorSettingsOverlay.TryApply(store, out errorMessage))
            {
                Godot.GD.PushWarning(errorMessage);
            }
#endif
            return store;
        }
    }
}
#pragma warning restore CA2255
#endif
