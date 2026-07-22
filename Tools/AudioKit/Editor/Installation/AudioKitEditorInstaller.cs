#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>把 AudioKit 工具交互能力安装到共享 Tool catalog。</summary>
    public static class AudioKitEditorInstaller
    {
        private static readonly AudioKitInteractionProvider sProvider = new();

        /// <summary>幂等注册 AudioKit Provider。</summary>
        public static void EnsureInstalled() => YokiFrameToolKitInteractionCatalog.Register(sProvider);
    }
}
#endif
