#if UNITY_EDITOR || (GODOT && TOOLS)

using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 描述 FileBridge pending 命令的 claim 结果，区分正常竞争和真实存储故障。
    /// </summary>
    internal enum YokiFrameFileBridgeClaimResult
    {
        /// <summary>当前 Host 已成功取得命令所有权。</summary>
        Claimed,

        /// <summary>命令已被其它 Host 取得，当前调用应跳过。</summary>
        AlreadyClaimed,

        /// <summary>pending 文件在 claim 期间消失，当前调用应跳过。</summary>
        Missing,

        /// <summary>claim 因目录、权限或其它存储错误失败。</summary>
        StorageError
    }

    /// <summary>
    /// 提供跨进程 FileBridge 命令的原子 claim；同一请求只能被一个 Host 移入 processing。
    /// </summary>
    internal static class YokiFrameFileBridgeClaim
    {
        private const string CLAIM_SUFFIX = ".claim";

        /// <summary>
        /// 尝试把 pending 命令原子移动到 processing 目录。
        /// </summary>
        /// <param name="pendingPath">commands 顶层的待处理文件。</param>
        /// <param name="processingRoot">当前 engine 的 processing 目录。</param>
        /// <param name="claimedPath">成功认领后的文件路径。</param>
        /// <returns>当前 worker 成功认领时返回 true；文件已被其它 worker 认领时返回 false。</returns>
        public static bool TryClaim(string pendingPath, string processingRoot, out string claimedPath)
        {
            return TryClaim(pendingPath, processingRoot, out claimedPath, out _)
                == YokiFrameFileBridgeClaimResult.Claimed;
        }

        /// <summary>
        /// 尝试原子 claim，并把竞争、文件消失与真实存储错误分别返回给 Host 协调器。
        /// </summary>
        /// <param name="pendingPath">commands 顶层的待处理文件。</param>
        /// <param name="processingRoot">当前 engine 的 processing 目录。</param>
        /// <param name="claimedPath">成功认领后的文件路径。</param>
        /// <param name="storageException">StorageError 时的原始异常；其它结果为空。</param>
        /// <returns>本次 claim 的详细结果。</returns>
        public static YokiFrameFileBridgeClaimResult TryClaim(
            string pendingPath,
            string processingRoot,
            out string claimedPath,
            out Exception storageException)
        {
            claimedPath = Path.Combine(processingRoot, Path.GetFileName(pendingPath));
            var markerPath = claimedPath + CLAIM_SUFFIX;
            FileStream marker = null;
            var ownsMarker = false;
            storageException = null;
            try
            {
                Directory.CreateDirectory(processingRoot);
                if (File.Exists(claimedPath))
                {
                    claimedPath = string.Empty;
                    return YokiFrameFileBridgeClaimResult.AlreadyClaimed;
                }

                // Unix rename 可能覆盖已存在目标；先用 CreateNew 抢占同名 marker，保证只有一个 worker 能继续。
                marker = new FileStream(markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                ownsMarker = true;
                File.Move(pendingPath, claimedPath);
                return YokiFrameFileBridgeClaimResult.Claimed;
            }
            catch (FileNotFoundException)
            {
                claimedPath = string.Empty;
                return YokiFrameFileBridgeClaimResult.Missing;
            }
            catch (DirectoryNotFoundException exception)
            {
                storageException = exception;
                var pendingExists = File.Exists(pendingPath);
                claimedPath = string.Empty;
                return pendingExists
                    ? YokiFrameFileBridgeClaimResult.StorageError
                    : YokiFrameFileBridgeClaimResult.Missing;
            }
            catch (IOException) when (!ownsMarker
                && (File.Exists(markerPath) || File.Exists(claimedPath) || !File.Exists(pendingPath)))
            {
                var pendingExists = File.Exists(pendingPath);
                claimedPath = string.Empty;
                return pendingExists
                    ? YokiFrameFileBridgeClaimResult.AlreadyClaimed
                    : YokiFrameFileBridgeClaimResult.Missing;
            }
            catch (IOException exception) when (ownsMarker
                && (File.Exists(claimedPath) || !File.Exists(pendingPath)))
            {
                // marker 已由当前 worker 创建；只有确认目标已经出现或 pending 已消失时才判为竞争失败。
                var targetExists = File.Exists(claimedPath);
                var pendingExists = File.Exists(pendingPath);
                claimedPath = string.Empty;
                if (targetExists)
                {
                    return YokiFrameFileBridgeClaimResult.AlreadyClaimed;
                }

                if (!pendingExists)
                {
                    return YokiFrameFileBridgeClaimResult.Missing;
                }

                storageException = exception;
                return YokiFrameFileBridgeClaimResult.StorageError;
            }
            catch (IOException exception)
            {
                claimedPath = string.Empty;
                storageException = exception;
                return YokiFrameFileBridgeClaimResult.StorageError;
            }
            catch (UnauthorizedAccessException exception)
            {
                claimedPath = string.Empty;
                storageException = exception;
                return YokiFrameFileBridgeClaimResult.StorageError;
            }
            finally
            {
                marker?.Dispose();
                if (ownsMarker)
                {
                    TryDelete(markerPath);
                }
            }
        }

        /// <summary>
        /// 删除跨会话遗留且超过 lease 的 claim marker；不会触碰仍在 processing 的请求文件。
        /// </summary>
        /// <param name="processingRoot">当前 engine 的 processing 目录。</param>
        /// <param name="cutoffUtc">早于该时间的 marker 视为遗留。</param>
        public static void RemoveExpiredMarkers(string processingRoot, DateTime cutoffUtc)
        {
            if (!Directory.Exists(processingRoot))
            {
                return;
            }

            foreach (var markerPath in Directory.EnumerateFiles(
                         processingRoot,
                         "*" + CLAIM_SUFFIX,
                         SearchOption.TopDirectoryOnly))
            {
                if (File.GetLastWriteTimeUtc(markerPath) < cutoffUtc
                    && !IsActiveFailureEvidenceMarker(markerPath))
                {
                    TryDelete(markerPath);
                }
            }
        }

        /// <summary>
        /// 判断非空 claim marker 是否仍绑定一个 processing 命令；该形式是 deadletter 失败证据，不能在命令仍存在时清除。
        /// </summary>
        /// <param name="markerPath">processing 旁的 claim marker 路径。</param>
        /// <returns>marker 是非空失败证据且对应命令仍存在时返回 true。</returns>
        private static bool IsActiveFailureEvidenceMarker(string markerPath)
        {
            try
            {
                var markerInfo = new FileInfo(markerPath);
                if (markerInfo.Length == 0)
                {
                    return false;
                }

                var commandPath = markerPath.Substring(
                    0,
                    markerPath.Length - CLAIM_SUFFIX.Length);
                return File.Exists(commandPath);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>尽力删除 claim marker；清理失败留给下一轮 lease 回收。</summary>
        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

#endif
