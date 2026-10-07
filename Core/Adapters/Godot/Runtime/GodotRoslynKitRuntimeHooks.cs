#if GODOT && TOOLS
using System;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// Core 侧的 RoslynKit 启动钩子。真正安装留在 RoslynKit 程序集，避免 Core 反向依赖 Tool。
    /// </summary>
    public static class GodotRoslynKitRuntimeHooks
    {
        /// <summary>由 RoslynKit 安装器注册。</summary>
        public static Action<Node, Func<string>, Func<long>, string> EnsureInstalled { get; set; }

        /// <summary>由 RoslynKit 安装器注册。</summary>
        public static Action Tick { get; set; }

        /// <summary>由 RoslynKit 安装器注册。</summary>
        public static Action Shutdown { get; set; }
    }
}
#endif
