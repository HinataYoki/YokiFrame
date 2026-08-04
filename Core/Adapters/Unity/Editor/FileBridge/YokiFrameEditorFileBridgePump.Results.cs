#if UNITY_EDITOR

using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 承载 FileBridge pump 的响应落盘、归档和 deadletter 处理逻辑。
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
        /// 将响应写入 results 目录。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="response">命令响应。</param>
        private static void WriteResponse(string requestId, YokiFrameEditorCommandResponse response)
        {
            YokiFrameEditorFileBridgeJson.WriteAtomic(YokiFrameEditorFileBridgePaths.GetResponsePath(requestId), YokiFrameEditorFileBridgeJson.ToJson(response));
        }

        /// <summary>
        /// 将已完成命令移动到 archive，保留 commands 顶层只放待处理命令。
        /// </summary>
        /// <param name="commandPath">原始命令路径。</param>
        private static void ArchiveCommand(string commandPath)
        {
            var archivePath = YokiFrameEditorFileBridgePaths.GetArchivePath(commandPath);
            Directory.CreateDirectory(Path.GetDirectoryName(archivePath));
            if (File.Exists(archivePath))
            {
                archivePath = archivePath + "." + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            File.Move(commandPath, archivePath);
        }

        /// <summary>
        /// 将无法解析或不安全的命令移动到 deadletter，并写入诊断信息。
        /// </summary>
        /// <param name="commandPath">原始命令路径。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        private static void MoveToDeadletter(string commandPath, string errorCode, string errorMessage)
        {
            var deadletterId = CreateDeadletterId(commandPath);
            var info = new YokiFrameEditorDeadletterInfo
            {
                sourcePath = commandPath,
                errorCode = errorCode,
                errorMessage = errorMessage,
                writtenAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            YokiFrameEditorFileBridgeJson.WriteAtomic(YokiFrameEditorFileBridgePaths.GetDeadletterInfoPath(deadletterId), YokiFrameEditorFileBridgeJson.ToJson(info));
            MoveRequestToDeadletter(commandPath, deadletterId);
        }

        /// <summary>
        /// deadletter 写入失败时，在 processing 命令旁原子保留失败证据。
        /// </summary>
        /// <param name="commandPath">processing 命令路径。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误说明。</param>
        private static void WriteProcessingFailureEvidence(
            string commandPath,
            string errorCode,
            string errorMessage)
        {
            var evidence = new YokiFrameEditorDeadletterInfo
            {
                sourcePath = commandPath,
                errorCode = errorCode,
                errorMessage = errorMessage,
                writtenAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            YokiFrameEditorFileBridgeJson.WriteAtomic(
                commandPath + ".claim",
                YokiFrameEditorFileBridgeJson.ToJson(evidence));
        }

        /// <summary>
        /// 根据文件名生成安全 deadletter 标识。
        /// </summary>
        /// <param name="commandPath">原始命令路径。</param>
        /// <returns>安全 deadletter 标识。</returns>
        private static string CreateDeadletterId(string commandPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(commandPath);
            if (YokiFrameEditorFileBridgeJson.IsSafeId(fileName))
            {
                return fileName;
            }

            return "invalid-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 移动 deadletter 原始请求文件，若目标冲突则追加时间后缀。
        /// </summary>
        /// <param name="commandPath">原始命令路径。</param>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        private static void MoveRequestToDeadletter(string commandPath, string deadletterId)
        {
            var requestPath = YokiFrameEditorFileBridgePaths.GetDeadletterRequestPath(deadletterId);
            if (File.Exists(requestPath))
            {
                requestPath = requestPath + "." + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            File.Move(commandPath, requestPath);
        }
    }
}

#endif
