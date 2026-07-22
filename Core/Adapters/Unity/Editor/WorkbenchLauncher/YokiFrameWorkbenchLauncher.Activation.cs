#if UNITY_EDITOR

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Unity 启动器到既有 Workbench owner 的轻量激活协议。
    /// </summary>
    internal static partial class YokiFrameWorkbenchLauncher
    {
        /// <summary>
        /// 在启动候选进程前直接通知同项目 owner，避免开发版构建或冷启动遮蔽已有窗口激活。
        /// Unity Mono 对客户端 CurrentUserOnly 校验会访问未实现的 WindowsIdentity.Owner，当前用户限制由 Workbench owner 端负责。
        /// </summary>
        /// <param name="projectRoot">Workbench 对应项目根。</param>
        /// <returns>已有 owner 已确认激活时返回 true。</returns>
        private static bool TryActivateExistingWorkbench(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return false;
            }

            try
            {
                var pipeName = CreateActivationPipeName(projectRoot);
                using (NamedPipeClientStream client = new(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous))
                {
                    client.Connect(ACTIVATION_CONNECT_TIMEOUT_MS);
                    return SendActivationRequest(client);
                }
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is TimeoutException
                || exception is UnauthorizedAccessException
                || exception is InvalidOperationException
                || exception is NotImplementedException
                || exception is PlatformNotSupportedException)
            {
                return false;
            }
        }

        /// <summary>
        /// 按 Workbench 相同的项目路径语义生成不暴露绝对路径的管道名。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根。</param>
        /// <returns>与 Workbench owner 一致的项目级管道名。</returns>
        private static string CreateActivationPipeName(string projectRoot)
        {
            var normalizedProjectRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var identity = Application.platform == RuntimePlatform.WindowsEditor
                ? normalizedProjectRoot.ToUpperInvariant()
                : normalizedProjectRoot;
            byte[] hash;
            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity));
            }

            var hashText = BitConverter.ToString(hash, 0, 16).Replace("-", string.Empty).ToLowerInvariant();
            return ACTIVATION_PIPE_NAME_PREFIX + hashText;
        }

        /// <summary>
        /// 写入固定激活意图，并在有限时间内等待 owner 完成真实窗口前台恢复。
        /// </summary>
        /// <param name="client">已连接到项目 owner 的双向管道。</param>
        /// <returns>owner 返回 ACK 时返回 true。</returns>
        private static bool SendActivationRequest(NamedPipeClientStream client)
        {
            using (StreamWriter writer = new(client, new UTF8Encoding(false), 1024, true))
            {
                writer.AutoFlush = true;
                writer.WriteLine(ACTIVATION_MESSAGE);
            }

            using (StreamReader reader = new(client, Encoding.UTF8, true, 1024, false))
            {
                var responseTask = reader.ReadLineAsync();
                if (!responseTask.Wait(ACTIVATION_RESPONSE_TIMEOUT_MS))
                {
                    return false;
                }

                return string.Equals(
                    responseTask.GetAwaiter().GetResult(),
                    ACTIVATION_ACKNOWLEDGED,
                    StringComparison.Ordinal);
            }
        }
    }
}

#endif
