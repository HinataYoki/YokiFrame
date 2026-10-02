#if GODOT
using System.Runtime.CompilerServices;

#pragma warning disable CA2255 // 模块初始化只注册惰性工厂，不创建 Provider。

namespace YokiFrame
{
    /// <summary>
    /// 在 Godot Adapter 程序集加载时注册 ResKit 默认工厂，避免 Bootstrap 维护 Kit 清单。
    /// </summary>
    internal static class GodotResKitRuntimeInstaller
    {
        /// <summary>
        /// 模块加载时注册工厂；真正的 Provider 仍等到第一次资源调用才创建。
        /// </summary>
        [ModuleInitializer]
        internal static void RegisterOnAssemblyLoad()
        {
            ResKit.RegisterDefaultProviderFactory(CreateDefaultProvider);
        }

        /// <summary>
        /// 构造 Godot 默认资源后端。只应由 ResKit 的惰性工厂调用。
        /// </summary>
        /// <returns>新的 Godot ResourceLoader Provider。</returns>
        private static IResourceProvider CreateDefaultProvider()
        {
            return new GodotResourceProvider();
        }
    }
}
#pragma warning restore CA2255
#endif
