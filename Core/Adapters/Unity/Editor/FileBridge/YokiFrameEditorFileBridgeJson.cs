#if UNITY_EDITOR

using System;
using System.IO;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Unity Editor FileBridge 使用的 JSON、原子写入和安全标识工具。
    /// </summary>
    internal static class YokiFrameEditorFileBridgeJson
    {
        /// <summary>
        /// 将对象序列化为 compact JSON。
        /// </summary>
        /// <param name="value">待序列化对象。</param>
        /// <returns>compact JSON 文本。</returns>
        public static string ToJson(object value)
        {
            return JsonUtility.ToJson(value, false);
        }

        /// <summary>
        /// 从 JSON 文本反序列化对象。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="json">JSON 文本。</param>
        /// <returns>反序列化对象。</returns>
        public static T FromJson<T>(string json)
        {
            return JsonUtility.FromJson<T>(json);
        }

        /// <summary>
        /// 使用共享原子写提交 JSON；临时文件、flush 与替换语义由 YokiFrameAtomicFileWriter 单源维护。
        /// </summary>
        /// <param name="targetPath">最终目标路径。</param>
        /// <param name="json">待写入 JSON 文本。</param>
        public static void WriteAtomic(string targetPath, string json)
        {
            YokiFrameAtomicFileWriter.WriteAllText(targetPath, json);
        }

        /// <summary>
        /// 判断字符串是否符合 FileBridge 安全 ID 规则。
        /// </summary>
        /// <param name="value">待检查标识。</param>
        /// <returns>安全时返回 true。</returns>
        public static bool IsSafeId(string value)
        {
            return YokiFrameSafeIdContract.IsSafeId(value);
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
        /// 统计 engine 协议目录下 JSON 证据文件的数量、体积和最旧更新时间。
        /// </summary>
        /// <param name="engineRoot">engine 协议根目录。</param>
        /// <returns>协议存储诊断摘要。</returns>
        public static YokiFrameEditorProtocolStorageInfo ReadProtocolStorageDiagnostics(string engineRoot)
        {
            YokiFrameEditorProtocolStorageInfo info = new YokiFrameEditorProtocolStorageInfo();
            if (!Directory.Exists(engineRoot))
            {
                return info;
            }

            foreach (var path in Directory.EnumerateFiles(
                         engineRoot,
                         "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                         SearchOption.AllDirectories))
            {
                AddProtocolFile(info, path);
            }

            return info;
        }

        /// <summary>
        /// 把单个 JSON 文件计入协议存储诊断，避免统计逻辑散落在命令 handler 中。
        /// </summary>
        /// <param name="info">待更新的诊断摘要。</param>
        /// <param name="path">JSON 文件路径。</param>
        private static void AddProtocolFile(YokiFrameEditorProtocolStorageInfo info, string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            info.fileCount++;
            info.totalBytes += fileInfo.Length;
            var lastWriteUtc = fileInfo.LastWriteTimeUtc.ToString("O");
            if (string.IsNullOrEmpty(info.oldestFileUtc) || string.CompareOrdinal(lastWriteUtc, info.oldestFileUtc) < 0)
            {
                info.oldestFileUtc = lastWriteUtc;
            }
        }
    }
}

#endif