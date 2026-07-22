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
            KitSettings.RegisterDefaultStoreFactory(GodotYokiFrameRuntimeSettingsLoader.Load);
        }
    }
}
#pragma warning restore CA2255
#endif
