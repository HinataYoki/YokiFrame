#if GODOT && TOOLS
namespace YokiFrame
{
    /// <summary>
    /// Godot Runtime FileBridge 路径入口。
    /// 实际路径规则由共享 <see cref="YokiFrameFileBridgeEnginePathSet"/> 维护，本类型只绑定 godot-runtime。
    /// </summary>
    internal sealed class GodotFileBridgePaths : YokiFrameFileBridgeEnginePathSet
    {
        /// <summary>
        /// 创建绑定 Godot Runtime engine 的协议路径集合。
        /// </summary>
        /// <param name="projectRoot">Godot 项目根目录。</param>
        public GodotFileBridgePaths(string projectRoot)
            : base(projectRoot, GodotFileBridgeHost.ENGINE_ID)
        {
        }
    }
}
#endif
