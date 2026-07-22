#if UNITY_5_3_OR_NEWER
#if UNITY_EDITOR
namespace YokiFrame.Unity
{
    /// <summary>
    /// 定义 Unity Editor Adapter 向 Runtime Settings 加载流程提供的项目配置叠加入口。
    /// 整个类型不会进入 Player，Runtime Adapter 本身不读取 ProjectSettings。
    /// </summary>
    internal static class UnityYokiFrameRuntimeSettingsEditorOverlay
    {
        internal delegate bool ApplyHandler(
            YokiFrameRuntimeSettingsStore runtimeStore,
            out string errorMessage);

        private static ApplyHandler sHandler;

        /// <summary>
        /// 注册由 Unity Editor Adapter 拥有的项目配置读取实现。
        /// </summary>
        /// <param name="handler">Editor 项目配置合并实现；传入 null 时清除实现。</param>
        internal static void Register(ApplyHandler handler)
        {
            sHandler = handler;
        }

        /// <summary>
        /// 在 Runtime Resources 解析成功后应用 Editor 项目配置；未安装 Editor 实现时保持 Runtime Store 不变。
        /// </summary>
        /// <param name="runtimeStore">已解析且尚未发布的 Runtime Store。</param>
        /// <param name="errorMessage">Editor 配置读取或校验失败时的诊断。</param>
        /// <returns>没有 Editor 配置或配置已安全合并时返回 true。</returns>
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
#endif
