#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// 将 ActionKit 编辑器交互能力安装到共享 Tool catalog；宿主 Adapter 可在自身生命周期中安全重复调用。
    /// </summary>
    public static class ActionKitEditorInstaller
    {
        private static readonly ActionKitInteractionProvider sProvider = new();

        /// <summary>
        /// 安装 ActionKit 编辑器交互 Provider；同一进程内重复调用保持幂等。
        /// </summary>
        public static void EnsureInstalled()
        {
            YokiFrameToolKitInteractionCatalog.Register(sProvider);
        }
    }
}
#endif
