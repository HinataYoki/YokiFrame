#if GODOT
using System.Runtime.CompilerServices;

#pragma warning disable CA2255 // 可选 Tool 不能由 Core 反向引用，程序集加载钩子只负责注册惰性工厂。

namespace YokiFrame.Godot
{
    /// <summary>在 Godot Tool Adapter 程序集加载时注册惰性默认后端工厂。</summary>
    public static class GodotAudioKitRuntimeInstaller
    {
        /// <summary>模块加载时登记会话重置和默认工厂；不创建后端也不覆盖显式后端。</summary>
        [ModuleInitializer]
        internal static void RegisterDefaultBackendFactory()
        {
            EnsureInstalled();
        }

        /// <summary>供 Godot 薄 bootstrap 显式确保程序集加载并注册默认后端工厂。</summary>
        public static void EnsureInstalled()
        {
            YokiFrameSession.Register(YokiFrameSession.RELEASE_HOSTS_ORDER, "audiokit", AudioKit.Reset);
            YokiFrameSession.Register(
                YokiFrameSession.RESTORE_FACTORIES_ORDER,
                "audiokit-godot-factory",
                RegisterGodotFactory);
            RegisterGodotFactory();
        }

        /// <summary>登记不会立即创建后端的 Godot 默认工厂。</summary>
        private static void RegisterGodotFactory()
        {
            AudioKit.RegisterDefaultBackendFactory(static () => new GodotAudioKitBackend());
        }
    }
}
#pragma warning restore CA2255
#endif
