#if GODOT && TOOLS
namespace YokiFrame
{
    /// <summary>
    /// 提供 Godot Editor Adapter 向 Runtime Settings 加载流程注册编辑器配置叠加的入口。
    /// Runtime 不引用编辑器文件或 Godot Editor API，未注册时保持 ProjectSettings 结果不变。
    /// </summary>
    public static class GodotYokiFrameEditorSettingsOverlay
    {
        public delegate bool ApplyHandler(
            YokiFrameRuntimeSettingsStore runtimeStore,
            out string errorMessage);

        private static ApplyHandler sHandler;

        /// <summary>
        /// 注册由 Godot Editor Adapter 拥有的编辑器配置读取实现。
        /// </summary>
        /// <param name="handler">编辑器配置合并实现；传入 null 时清除实现。</param>
        internal static void Register(ApplyHandler handler)
        {
            sHandler = handler;
        }

        /// <summary>
        /// 在 ProjectSettings 解析完成后应用编辑器配置；没有注册实现时直接成功。
        /// </summary>
        /// <param name="runtimeStore">已解析且尚未发布的 Runtime Store。</param>
        /// <param name="errorMessage">编辑器配置读取或校验失败时的诊断。</param>
        /// <returns>没有编辑器配置或配置已安全合并时返回 true。</returns>
        internal static bool TryApply(
            YokiFrameRuntimeSettingsStore runtimeStore,
            out string errorMessage)
        {
            ApplyHandler handler = sHandler;
            if (handler == null)
            {
                errorMessage = string.Empty;
                return true;
            }

            return handler(runtimeStore, out errorMessage);
        }
    }
}
#endif
