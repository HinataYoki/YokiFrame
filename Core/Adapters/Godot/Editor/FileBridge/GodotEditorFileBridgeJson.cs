#if GODOT && TOOLS
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Godot Editor FileBridge 的 JSON、原子写入和存储诊断能力。
    /// </summary>
    internal static class GodotEditorFileBridgeJson
    {
        private static readonly JsonSerializerOptions sOptions = CreateOptions();

        /// <summary>
        /// 将协议 DTO 序列化为 camelCase compact JSON。
        /// </summary>
        /// <typeparam name="T">协议 DTO 类型。</typeparam>
        /// <param name="value">待序列化对象。</param>
        /// <returns>compact JSON。</returns>
        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, sOptions);
        }

        /// <summary>
        /// 反序列化协议 DTO 并拒绝空结果。
        /// </summary>
        /// <typeparam name="T">目标 DTO 类型。</typeparam>
        /// <param name="json">完整 JSON。</param>
        /// <returns>反序列化对象。</returns>
        public static T Deserialize<T>(string json)
        {
            var value = JsonSerializer.Deserialize<T>(json, sOptions);
            if (value == null)
            {
                throw new InvalidDataException("Godot Editor FileBridge JSON deserialized to null.");
            }

            return value;
        }

        /// <summary>
        /// 使用同目录临时文件、落盘 flush 和原子重命名提交 JSON。
        /// </summary>
        /// <param name="targetPath">正式目标路径。</param>
        /// <param name="json">完整 JSON。</param>
        public static void WriteAtomic(string targetPath, string json)
        {
            var directoryPath = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new DirectoryNotFoundException("Godot Editor FileBridge target path has no directory.");
            }

            Directory.CreateDirectory(directoryPath);
            var temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                WriteTemporaryFile(temporaryPath, json);
                File.Move(temporaryPath, targetPath, true);
            }
            finally
            {
                DeleteIfExists(temporaryPath);
            }
        }

        /// <summary>
        /// 统计指定目录顶层 JSON 文件数量。
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
        /// 扫描 engine 根下全部 JSON 证据的数量、字节数和最旧更新时间。
        /// </summary>
        /// <param name="engineRoot">engine 协议根。</param>
        /// <returns>协议存储统计。</returns>
        public static GodotEditorProtocolStorageInfo ReadStorageDiagnostics(string engineRoot)
        {
            GodotEditorProtocolStorageInfo info = new GodotEditorProtocolStorageInfo();
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
        /// 验证 payloadJson 是合法 JSON，避免损坏文本进入 dispatcher。
        /// </summary>
        /// <param name="payloadJson">待验证 payload。</param>
        public static void ValidatePayloadJson(string payloadJson)
        {
            JsonHelper.EnsureValidJson(payloadJson);
        }

        /// <summary>
        /// 创建 Editor Host 共用的 camelCase JSON 配置。
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
        /// 把单个 JSON 文件计入协议存储统计。
        /// </summary>
        /// <param name="info">待更新统计。</param>
        /// <param name="path">JSON 文件路径。</param>
        private static void AddStorageFile(GodotEditorProtocolStorageInfo info, string path)
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

        /// <summary>
        /// 以无 BOM UTF-8 与 WriteThrough 写入临时文件并强制落盘。
        /// </summary>
        /// <param name="temporaryPath">临时路径。</param>
        /// <param name="json">完整 JSON。</param>
        private static void WriteTemporaryFile(string temporaryPath, string json)
        {
            using FileStream stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough);
            using StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(json);
            writer.Flush();
            stream.Flush(true);
        }

        /// <summary>
        /// 删除存在的临时文件，缺失时保持幂等。
        /// </summary>
        /// <param name="path">待删除路径。</param>
        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
#endif
