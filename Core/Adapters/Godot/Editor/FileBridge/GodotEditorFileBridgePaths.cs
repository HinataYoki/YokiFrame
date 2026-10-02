#if GODOT && TOOLS
namespace YokiFrame
{
    /// <summary>
    /// Godot Editor FileBridge 路径入口。
    /// 实际路径规则由共享 <see cref="YokiFrameFileBridgeEnginePathSet"/> 维护，本类型只绑定 godot-editor。
    /// </summary>
    internal sealed class GodotEditorFileBridgePaths : YokiFrameFileBridgeEnginePathSet
    {
        /// <summary>
        /// 创建绑定 Godot Editor engine 的协议路径集合。
        /// </summary>
        /// <param name="projectRoot">Godot 项目根目录。</param>
        public GodotEditorFileBridgePaths(string projectRoot)
            : base(projectRoot, GodotEditorFileBridgeHost.ENGINE_ID)
        {
        }
    }
}
#endif
