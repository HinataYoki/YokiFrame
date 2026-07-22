#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>负责把 SaveKit Provider 幂等安装到 Tool Interaction catalog。</summary>
    public static class SaveKitEditorInstaller
    {
        private static readonly SaveKitInteractionProvider sProvider = new();

        /// <summary>注册 SaveKit Provider；同一进程重复调用不会创建第二个 owner。</summary>
        public static void EnsureInstalled()
        {
            YokiFrameToolKitInteractionCatalog.Register(sProvider);
        }
    }
}
#endif
