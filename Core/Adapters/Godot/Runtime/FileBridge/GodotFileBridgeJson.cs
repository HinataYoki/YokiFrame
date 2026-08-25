#if GODOT && TOOLS
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Godot Runtime FileBridge 的 JSON、原子写入和存储诊断能力。
    /// </summary>
    internal static class GodotFileBridgeJson
    {
        private static readonly JsonSerializerOptions sOptions = CreateOptions();

        /// <summary>
        /// 将协议 DTO 序列化为 camelCase compact JSON。
        /// </summary>
        /// <typeparam name="T">协议 DTO 类型。</typeparam>
        /// <param name="value">待序列化对象。</param>
        /// <returns>compact JSON 文本。</returns>
        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, sOptions);
        }

        /// <summary>
        /// 从 JSON 文本反序列化协议 DTO，并拒绝空结果。
        /// </summary>
        /// <typeparam name="T">目标 DTO 类型。</typeparam>
        /// <param name="json">JSON 文本。</param>
        /// <returns>反序列化对象。</returns>
        public static T Deserialize<T>(string json)
        {
            var value = JsonSerializer.Deserialize<T>(json, sOptions);
            if (value == null)
            {
                throw new InvalidDataException("Godot FileBridge JSON deserialized to null.");
            }

            return value;
        }

        /// <summary>
        /// 使用共享原子写提交 JSON；临时文件、flush 与替换语义由 YokiFrameAtomicFileWriter 单源维护。
        /// </summary>
        /// <param name="targetPath">正式目标路径。</param>
        /// <param name="json">完整 JSON 文本。</param>
        public static void WriteAtomic(string targetPath, string json)
        {
            YokiFrameAtomicFileWriter.WriteAllText(targetPath, json);
        }

        /// <summary>
        /// 统计指定目录顶层的 JSON 文件数量。
        /// </summary>
        /// <param name="directoryPath">待统计目录。</param>
        /// <returns>JSON 文件数量。</returns>
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
        /// 扫描 engine 根下 JSON 证据的数量、总字节数和最旧更新时间。
        /// </summary>
        /// <param name="engineRoot">engine 协议根。</param>
        /// <returns>协议存储诊断。</returns>
        public static GodotProtocolStorageInfo ReadStorageDiagnostics(string engineRoot)
        {
            GodotProtocolStorageInfo info = new GodotProtocolStorageInfo();
            if (!Directory.Exists(engineRoot))
            {
                return info;
            }

            foreach (var path in Directory.EnumerateFiles(
                         engineRoot,
                         "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                         SearchOption.AllDirectories))
            {
                AddStorageFile(info, path);
            }

            return info;
        }

        /// <summary>
        /// 创建 camelCase、大小写不敏感且不输出多余空白的 JSON 配置。
        /// </summary>
        /// <returns>序列化配置。</returns>
        private static JsonSerializerOptions CreateOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        /// <summary>
        /// 把单个 JSON 文件计入协议存储诊断。
        /// </summary>
        /// <param name="info">待更新诊断。</param>
        /// <param name="path">JSON 文件路径。</param>
        private static void AddStorageFile(GodotProtocolStorageInfo info, string path)
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
}
#endif
