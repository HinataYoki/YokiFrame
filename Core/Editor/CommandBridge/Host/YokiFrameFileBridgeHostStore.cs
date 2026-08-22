#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 提供三宿主共用的 FileBridge 命令存储实现：枚举、认领、lease、终态写入、归档与 deadletter。
    /// 宿主差异仅通过构造参数注入：路径契约、原子写 JSON 委托、deadletter 序列化委托、清理策略与排序规则。
    /// </summary>
    internal sealed class YokiFrameFileBridgeHostStore : IYokiFrameHostCommandStore
    {
        private readonly IYokiFrameFileBridgeEnginePaths mPaths;
        private readonly Action<string, string> mWriteAtomicJson;
        private readonly Func<string, string, string, string> mSerializeDeadletterInfo;
        private readonly Action mPruneAfterBatch;
        private readonly Action mPruneWhenPendingRootMissing;
        private readonly bool mSortPendingPathsOrdinalIgnoreCase;

        /// <summary>
        /// 创建共享命令存储。
        /// </summary>
        /// <param name="paths">宿主 engine 协议路径契约。</param>
        /// <param name="writeAtomicJson">宿主原子写 JSON 委托（临时文件 + flush + 原子替换）。</param>
        /// <param name="serializeDeadletterInfo">宿主 deadletter 诊断序列化委托，返回与既有 wire 格式一致的 JSON。</param>
        /// <param name="pruneAfterBatch">批次结束后的宿主清理策略。</param>
        /// <param name="pruneWhenPendingRootMissing">commands 根缺失时的宿主清理策略。</param>
        /// <param name="sortPendingPathsOrdinalIgnoreCase">是否对 pending 枚举结果做稳定排序（Godot 宿主保持原语义）。</param>
        public YokiFrameFileBridgeHostStore(
            IYokiFrameFileBridgeEnginePaths paths,
            Action<string, string> writeAtomicJson,
            Func<string, string, string, string> serializeDeadletterInfo,
            Action pruneAfterBatch,
            Action pruneWhenPendingRootMissing,
            bool sortPendingPathsOrdinalIgnoreCase)
        {
            mPaths = paths ?? throw new ArgumentNullException(nameof(paths));
            mWriteAtomicJson = writeAtomicJson ?? throw new ArgumentNullException(nameof(writeAtomicJson));
            mSerializeDeadletterInfo = serializeDeadletterInfo ?? throw new ArgumentNullException(nameof(serializeDeadletterInfo));
            mPruneAfterBatch = pruneAfterBatch ?? new Action(() => { });
            mPruneWhenPendingRootMissing = pruneWhenPendingRootMissing ?? new Action(() => { });
            mSortPendingPathsOrdinalIgnoreCase = sortPendingPathsOrdinalIgnoreCase;
        }

        /// <summary>复核宿主协议路径安全性。</summary>
        public void EnsureReady()
        {
            mPaths.EnsureReady();
        }

        /// <summary>获取 commands 根目录是否存在。</summary>
        public bool PendingRootExists => Directory.Exists(mPaths.CommandsRoot);

        /// <summary>读取 pending 命令；Godot 宿主按 OrdinalIgnoreCase 稳定排序，Unity 保持文件系统顺序。</summary>
        public IReadOnlyList<string> ReadPendingCommandPaths()
        {
            if (!Directory.Exists(mPaths.CommandsRoot))
            {
                return Array.Empty<string>();
            }

            var commandPaths = Directory.GetFiles(
                mPaths.CommandsRoot,
                "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                SearchOption.TopDirectoryOnly);
            if (mSortPendingPathsOrdinalIgnoreCase)
            {
                Array.Sort(commandPaths, StringComparer.OrdinalIgnoreCase);
            }

            return commandPaths;
        }

        /// <summary>读取 processing 目录中的已认领命令。</summary>
        public IReadOnlyList<string> ReadProcessingCommandPaths()
        {
            return Directory.Exists(mPaths.ProcessingRoot)
                ? Directory.GetFiles(
                    mPaths.ProcessingRoot,
                    "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                    SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
        }

        /// <summary>原子认领 pending 命令到 processing 目录。</summary>
        public YokiFrameFileBridgeClaimResult TryClaim(
            string pendingPath,
            out string claimedPath,
            out Exception storageException)
        {
            return YokiFrameFileBridgeClaim.TryClaim(
                pendingPath,
                mPaths.ProcessingRoot,
                out claimedPath,
                out storageException);
        }

        /// <summary>删除超过 lease 的遗留 claim marker。</summary>
        public void RemoveExpiredMarkers(DateTime cutoffUtc)
        {
            YokiFrameFileBridgeClaim.RemoveExpiredMarkers(mPaths.ProcessingRoot, cutoffUtc);
        }

        /// <summary>获取文件最后写入 UTC 时间。</summary>
        public DateTime GetLastWriteTimeUtc(string path)
        {
            return File.GetLastWriteTimeUtc(path);
        }

        /// <summary>认领成功后刷新 lease 起点，避免旧 mtime 立即触发过期回收。</summary>
        public void RefreshProcessingLease(string commandPath, DateTime claimedAtUtc)
        {
            File.SetLastWriteTimeUtc(commandPath, claimedAtUtc);
        }

        /// <summary>判断 processing 命令是否已有 terminal response。</summary>
        public bool HasTerminalResponse(string commandPath)
        {
            try
            {
                return File.Exists(mPaths.GetResponsePath(
                    Path.GetFileNameWithoutExtension(commandPath)));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>原子写入 terminal response。</summary>
        public void WriteResponse(string requestId, string responseJson)
        {
            mWriteAtomicJson(mPaths.GetResponsePath(requestId), responseJson);
        }

        /// <summary>归档已完成命令；冲突时追加 UTC 毫秒后缀保留全部证据。</summary>
        public void Archive(string commandPath)
        {
            var archivePath = mPaths.GetArchivePath(commandPath);
            var archiveDirectory = Path.GetDirectoryName(archivePath);
            if (!string.IsNullOrEmpty(archiveDirectory))
            {
                Directory.CreateDirectory(archiveDirectory);
            }

            if (File.Exists(archivePath))
            {
                archivePath += "." + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            File.Move(commandPath, archivePath);
        }

        /// <summary>写入 deadletter 诊断并移���原始请求作为不可丢失证据。</summary>
        public void MoveToDeadletter(string commandPath, string errorCode, string errorMessage)
        {
            var deadletterId = CreateDeadletterId(commandPath);
            mWriteAtomicJson(
                mPaths.GetDeadletterInfoPath(deadletterId),
                mSerializeDeadletterInfo(commandPath, errorCode, errorMessage));
            MoveDeadletterRequest(commandPath, deadletterId);
        }

        /// <summary>deadletter 写入失败时在 processing 命令旁保留失败证据 marker。</summary>
        public void WriteProcessingFailureEvidence(
            string commandPath,
            string errorCode,
            string errorMessage)
        {
            mWriteAtomicJson(
                commandPath + ".claim",
                mSerializeDeadletterInfo(commandPath, errorCode, errorMessage));
        }

        /// <summary>执行宿主批次后清理策略。</summary>
        public void PruneAfterBatch()
        {
            mPruneAfterBatch();
        }

        /// <summary>执行宿主 commands 根缺失时的清理策略。</summary>
        public void PruneWhenPendingRootMissing()
        {
            mPruneWhenPendingRootMissing();
        }

        /// <summary>根据原文件名生成安全 deadletter ID；不安全时使用时间加随机后缀避免同毫秒碰撞。</summary>
        /// <param name="commandPath">原始命令文件路径。</param>
        /// <returns>安全 deadletter 标识。</returns>
        private static string CreateDeadletterId(string commandPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(commandPath);
            return YokiFrameSafeIdContract.IsSafeId(fileName)
                ? fileName
                : "invalid-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>移动 deadletter 原始请求；目标冲突时追加 UTC 毫秒后缀。</summary>
        /// <param name="commandPath">原始命令文件路径。</param>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        private void MoveDeadletterRequest(string commandPath, string deadletterId)
        {
            if (!File.Exists(commandPath))
            {
                return;
            }

            var requestPath = mPaths.GetDeadletterRequestPath(deadletterId);
            if (File.Exists(requestPath))
            {
                requestPath += "." + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            File.Move(commandPath, requestPath);
        }
    }
}
#endif
