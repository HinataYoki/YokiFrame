#if GODOT && TOOLS
using System;

namespace YokiFrame
{
    /// <summary>
    /// Core Editor 侧的 RoslynKit 启动钩子。安装实现留在 RoslynKit 程序集。
    /// </summary>
    public static class GodotRoslynKitEditorHooks
    {
        /// <summary>由 RoslynKit 安装器注册。</summary>
        public static Action<Func<string>, Func<long>, string> EnsureInstalled { get; set; }

        /// <summary>由 RoslynKit 安装器注册。</summary>
        public static Action Tick { get; set; }

        /// <summary>由 RoslynKit 安装器注册。</summary>
        public static Action Shutdown { get; set; }
    }
}
#endif
