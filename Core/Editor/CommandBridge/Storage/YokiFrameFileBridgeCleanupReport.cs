#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
// 与 YokiFrameFileBridgePruner 同域：仅在 Editor/Tools 宿主与 .NET 工具链编译。
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 汇总一次 `.yokiframe` 清理的删除量、失败路径和锁状态。
    /// </summary>
    public sealed class YokiFrameFileBridgeCleanupReport
    {
        private readonly List<string> mFailedPaths = new List<string>();

        /// <summary>获取实际删除的文件数量。</summary>
        public int DeletedFileCount { get; private set; }

        /// <summary>获取实际删除的文件总字节数。</summary>
        public long DeletedByteCount { get; private set; }

        /// <summary>获取删除失败的文件数量。</summary>
        public int FailedFileCount => mFailedPaths.Count;

        /// <summary>获取本轮是否因其它进程持有锁而跳过。</summary>
        public bool SkippedDueToLock { get; internal set; }

        /// <summary>获取删除失败路径的只读快照。</summary>
        public IReadOnlyList<string> FailedPaths => mFailedPaths.AsReadOnly();

        /// <summary>获取是否出现删除失败或锁竞争。</summary>
        public bool HasFailures => FailedFileCount > 0 || SkippedDueToLock;

        /// <summary>
        /// 记录一次成功删除。
        /// </summary>
        /// <param name="byteCount">被删除文件的字节数。</param>
        internal void RecordDeleted(long byteCount)
        {
            DeletedFileCount++;
            DeletedByteCount += Math.Max(0L, byteCount);
        }

        /// <summary>
        /// 记录一次删除失败。
        /// </summary>
        /// <param name="path">删除失败的完整路径。</param>
        internal void RecordFailure(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                mFailedPaths.Add(path);
            }
        }
    }
}
#endif
