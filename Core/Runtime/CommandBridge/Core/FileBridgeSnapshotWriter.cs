using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// Snapshot 通道用于覆盖写“当前状态”，避免把高频状态塞进命令响应或事件历史。
    /// </summary>
    public static class FileBridgeSnapshotWriter
    {
        /// <summary>
        /// 写入 engine-scoped snapshot 文件。
        /// </summary>
        /// <param name="yokiframeRoot">.yokiframe 根目录。</param>
        /// <param name="engineId">引擎实例标识。</param>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="snapshotName">Snapshot 名称。</param>
        /// <param name="payloadJson">Snapshot data 字段 JSON。</param>
        /// <returns>写入的 snapshot 文件路径。</returns>
        public static string WriteSnapshot(string yokiframeRoot, string engineId, string kit, string snapshotName, string payloadJson)
        {
            EnsureIdentifier(engineId, nameof(engineId));
            EnsureIdentifier(kit, nameof(kit));
            EnsureIdentifier(snapshotName, nameof(snapshotName));

            var snapshotPath = Path.Combine(yokiframeRoot, "engines", engineId, "snapshots", kit, snapshotName + ".json");
            var content = BuildSnapshotJson(engineId, kit, snapshotName, payloadJson);
            FileBridgeFileSystem.AtomicWriteAllTextInRoot(yokiframeRoot, snapshotPath, content);
            return snapshotPath;
        }

        /// <summary>
        /// 构建标准 snapshot JSON envelope。
        /// </summary>
        /// <param name="engineId">引擎实例标识。</param>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="snapshotName">Snapshot 名称。</param>
        /// <param name="payloadJson">Snapshot data 字段 JSON。</param>
        /// <returns>标准 snapshot JSON envelope。</returns>
        public static string BuildSnapshotJson(string engineId, string kit, string snapshotName, string payloadJson)
        {
            EnsureIdentifier(engineId, nameof(engineId));
            EnsureIdentifier(kit, nameof(kit));
            EnsureIdentifier(snapshotName, nameof(snapshotName));

            var data = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
            return "{\"protocolVersion\":2,\"engineId\":\"" + JsonHelper.EscapeString(engineId) +
                   "\",\"kit\":\"" + JsonHelper.EscapeString(kit) +
                   "\",\"snapshot\":\"" + JsonHelper.EscapeString(snapshotName) +
                   "\",\"updatedAtUtc\":\"" + DateTime.UtcNow.ToString("O") +
                   "\",\"data\":" + data + "}";
        }

        private static void EnsureIdentifier(string value, string name)
        {
            if (!CommandBridgeProtocol.IsSafeIdentifier(value))
                throw new ArgumentException("Invalid snapshot identifier '" + value + "'", name);
        }
    }
}
