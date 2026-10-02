#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 统计 engine 协议目录中 JSON 证据的数量、体积和最旧更新时间。
    /// Unity Editor 与 Godot Runtime/Editor 共用本实现，避免三处扫描规则各自漂移。
    /// </summary>
    internal static class YokiFrameFileBridgeStorageDiagnostics
    {
        /// <summary>
        /// 统计指定目录顶层的 JSON 文件数量；目录不存在时返回 0。
        /// </summary>
        /// <param name="directoryPath">待统计目录。</param>
        /// <returns>顶层 JSON 文件数量。</returns>
        public static int CountJsonFiles(string directoryPath)
        {
            return Directory.Exists(directoryPath)
                ? Directory.GetFiles(
                    directoryPath,
                    "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                    SearchOption.TopDirectoryOnly).Length
                : 0;
        }

        /// <summary>
        /// 扫描 engine 根下全部 JSON 证据，包含嵌套协议目录。
        /// </summary>
        /// <param name="engineRoot">engine 协议根。</param>
        /// <returns>协议存储诊断；根目录不存在时返回空统计。</returns>
        public static YokiFrameFileBridgeStorageInfo Read(string engineRoot)
        {
            YokiFrameFileBridgeStorageInfo info = new YokiFrameFileBridgeStorageInfo();
            if (!Directory.Exists(engineRoot))
            {
                return info;
            }

            foreach (var path in Directory.EnumerateFiles(
                         engineRoot,
                         "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                         SearchOption.AllDirectories))
            {
                AddFile(info, path);
            }

            return info;
        }

        /// <summary>
        /// 把单个现存 JSON 文件计入诊断，并按 UTC 文本顺序保留最旧更新时间。
        /// </summary>
        /// <param name="info">待更新诊断。</param>
        /// <param name="path">JSON 文件路径。</param>
        /// <summary>
        /// 把单个现存 JSON 文件计入诊断，并按 UTC 文本顺序保留最旧更新时间。
        /// </summary>
        /// <param name="info">待更新诊断。</param>
        /// <param name="path">JSON 文件路径。</param>
        private static void AddFile(YokiFrameFileBridgeStorageInfo info, string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            info.FileCount++;
            info.TotalBytes += fileInfo.Length;
            var lastWriteUtc = fileInfo.LastWriteTimeUtc.ToString("O");
            if (string.IsNullOrEmpty(info.OldestFileUtc)
                || string.CompareOrdinal(lastWriteUtc, info.OldestFileUtc) < 0)
            {
                info.OldestFileUtc = lastWriteUtc;
            }
        }
    }

    /// <summary>
    /// 表示一次 FileBridge 协议目录扫描结果；字段名保持稳定，供宿主投影到各自 wire DTO。
    /// </summary>
    internal sealed class YokiFrameFileBridgeStorageInfo
    {
        /// <summary>获取扫描到的 JSON 文件数量。</summary>
        public int FileCount { get; set; }

        /// <summary>获取扫描到的 JSON 文件总字节数。</summary>
        public long TotalBytes { get; set; }

        /// <summary>获取最旧文件的 UTC 更新时间文本；没有文件时为空。</summary>
        public string OldestFileUtc { get; set; } = string.Empty;
    }
}
#endif
