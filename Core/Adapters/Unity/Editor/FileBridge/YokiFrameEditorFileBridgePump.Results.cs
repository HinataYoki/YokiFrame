#if UNITY_EDITOR

using System;

namespace YokiFrame
{
    /// <summary>
    /// 承载 FileBridge pump 的响应构造与 deadletter 序列化；落盘、归档与 deadletter 移动已由共享命令存储承载。
    /// </summary>
    internal static partial class YokiFrameEditorFileBridgePump
    {
        /// <summary>
        /// 创建成功响应。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="resultJson">业务结果 JSON 字符串。</param>
        /// <returns>命令响应。</returns>
        private static YokiFrameEditorCommandResponse CreateSuccessResponse(string requestId, string resultJson)
        {
            return new YokiFrameEditorCommandResponse
            {
                requestId = requestId,
                status = "Success",
                resultJson = resultJson,
                completedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        /// <summary>
        /// 创建失败响应，供 CLI 得到 terminal response 而不是超时。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        /// <returns>命令响应。</returns>
        private static YokiFrameEditorCommandResponse CreateErrorResponse(string requestId, string errorCode, string errorMessage)
        {
            return new YokiFrameEditorCommandResponse
            {
                requestId = requestId,
                status = "Error",
                errorCode = errorCode,
                errorMessage = errorMessage,
                completedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        /// <summary>
        /// 序列化与既有 wire 格式一致的 deadletter 诊断 JSON，供共享命令存储写入证据。
        /// </summary>
        /// <param name="sourcePath">原始命令路径。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        /// <returns>deadletter 诊断 JSON 文本。</returns>
        private static string SerializeDeadletterInfo(string sourcePath, string errorCode, string errorMessage)
        {
            return YokiFrameEditorFileBridgeJson.ToJson(new YokiFrameEditorDeadletterInfo
            {
                sourcePath = sourcePath,
                errorCode = errorCode,
                errorMessage = errorMessage,
                writtenAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
        }
    }
}

#endif
