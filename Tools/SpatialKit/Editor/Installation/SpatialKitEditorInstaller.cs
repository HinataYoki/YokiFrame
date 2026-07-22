#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>负责把 SpatialKit Provider 幂等安装到 Tool Interaction catalog。</summary>
    public static class SpatialKitEditorInstaller
    {
        private static readonly SpatialKitInteractionProvider sProvider = new SpatialKitInteractionProvider();

        /// <summary>安装 SpatialKit Provider；同一进程重复调用保持幂等。</summary>
        public static void EnsureInstalled()
        {
            YokiFrameToolKitInteractionCatalog.Register(sProvider);
        }
    }
}
#endif
