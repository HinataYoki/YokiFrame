#if UNITY_EDITOR
namespace YokiFrame
{
    /// <summary>
    /// UIKit 编辑器命令响应数据。
    /// </summary>
    internal sealed class UIKitEditorCommandResult
    {
        private readonly string mMessage;
        private readonly string mPrefabPath;
        private readonly string mScriptPath;
        private readonly string mDesignerPath;
        private readonly int mChangedCount;
        private readonly int mSkippedCount;
        private readonly bool mRequiresCompile;

        private UIKitEditorCommandResult(
            string message,
            string prefabPath,
            string scriptPath,
            string designerPath,
            int changedCount,
            int skippedCount,
            bool requiresCompile)
        {
            mMessage = message;
            mPrefabPath = prefabPath ?? string.Empty;
            mScriptPath = scriptPath ?? string.Empty;
            mDesignerPath = designerPath ?? string.Empty;
            mChangedCount = changedCount;
            mSkippedCount = skippedCount;
            mRequiresCompile = requiresCompile;
        }

        /// <summary>
        /// 构造带 Prefab 与脚本路径的成功响应。
        /// </summary>
        public static UIKitEditorCommandResult Success(string message, string prefabPath, string scriptPath, string designerPath, bool requiresCompile) =>
            new(message, prefabPath, scriptPath, designerPath, 0, 0, requiresCompile);

        /// <summary>
        /// 构造批量变更统计的成功响应。
        /// </summary>
        public static UIKitEditorCommandResult Success(string message, int changedCount, int skippedCount) =>
            new(message, string.Empty, string.Empty, string.Empty, changedCount, skippedCount, false);

        /// <summary>
        /// 序列化为命令桥响应 JSON。
        /// </summary>
        public string ToJson()
        {
            return "{\"success\":true" +
                   ",\"message\":\"" + JsonHelper.EscapeString(mMessage) + "\"" +
                   ",\"prefabPath\":\"" + JsonHelper.EscapeString(mPrefabPath) + "\"" +
                   ",\"scriptPath\":\"" + JsonHelper.EscapeString(mScriptPath) + "\"" +
                   ",\"designerPath\":\"" + JsonHelper.EscapeString(mDesignerPath) + "\"" +
                   ",\"changedCount\":" + mChangedCount +
                   ",\"skippedCount\":" + mSkippedCount +
                   ",\"requiresCompile\":" + (mRequiresCompile ? "true" : "false") +
                   "}";
        }
    }
}
#endif
