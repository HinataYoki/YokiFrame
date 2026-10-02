#if GODOT && TOOLS
using System;
using System.IO;
using System.Text.Json;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Godot Runtime 与 Godot Editor 共用的 FileBridge JSON 能力。
    /// Editor 通过源码链接编译同一文件，序列化配置和空结果拒绝只存在这一处。
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
            return YokiFrameFileBridgeStorageDiagnostics.CountJsonFiles(directoryPath);
        }

        /// <summary>
        /// 扫描 engine 根下 JSON 证据的数量、总字节数和最旧更新时间。
        /// </summary>
        /// <param name="engineRoot">engine 协议根。</param>
        /// <returns>协议存储诊断。</returns>
        public static YokiFrameFileBridgeStorageInfo ReadStorageDiagnostics(string engineRoot)
        {
            return YokiFrameFileBridgeStorageDiagnostics.Read(engineRoot);
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
    }
}
#endif
