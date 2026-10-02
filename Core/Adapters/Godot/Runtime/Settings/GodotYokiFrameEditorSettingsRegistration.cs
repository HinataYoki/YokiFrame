#if GODOT && TOOLS
namespace YokiFrame
{
    /// <summary>
    /// 暴露给 Godot Editor Adapter 的编辑器配置注册入口。
    /// Runtime 不引用编辑器文件；Editor 在插件进入时注册，未注册时保持 ProjectSettings 结果。
    /// </summary>
    public static class GodotYokiFrameEditorSettingsRegistration
    {
        /// <summary>
        /// 注册编辑器配置叠加实现。传入 null 时清除实现。
        /// </summary>
        /// <param name="handler">由 Editor Adapter 提供的读取与合并实现。</param>
        public static void Register(GodotYokiFrameEditorSettingsOverlay.ApplyHandler handler)
        {
            GodotYokiFrameEditorSettingsOverlay.Register(handler);
        }
    }
}
#endif
