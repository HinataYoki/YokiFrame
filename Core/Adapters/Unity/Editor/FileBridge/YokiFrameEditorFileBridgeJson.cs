#if UNITY_EDITOR

using System;
using System.IO;
using System.Text;
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
        /// 使用临时文件和原子替换写入 JSON，避免工具侧读取半写文件。
        /// </summary>
        /// <param name="targetPath">最终目标路径。</param>
        /// <param name="json">待写入 JSON 文本。</param>
        public static void WriteAtomic(string targetPath, string json)
        {
            var directoryPath = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new DirectoryNotFoundException("FileBridge target path has no directory.");
            }

            Directory.CreateDirectory(directoryPath);
            var tempPath = Path.Combine(directoryPath, Path.GetFileName(targetPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            WriteTempThenMove(tempPath, targetPath, json);
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

        /// <summary>
        /// 将临时文件 flush 到磁盘后移动为正式文件。
        /// </summary>
        /// <param name="tempPath">临时文件路径。</param>
        /// <param name="targetPath">正式文件路径。</param>
        /// <param name="json">待写入 JSON。</param>
        private static void WriteTempThenMove(string tempPath, string targetPath, string json)
        {
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                ReplaceFile(tempPath, targetPath);
            }
            finally
            {
                DeleteTempFile(tempPath);
            }
        }

        /// <summary>
        /// 优先使用平台原子替换；不支持时通过同目录备份完成可恢复替换，失败时保留旧文件。
        /// </summary>
        /// <param name="tempPath">已完成写入的临时文件。</param>
        /// <param name="targetPath">正式目标文件。</param>
        private static void ReplaceFile(string tempPath, string targetPath)
        {
            if (!File.Exists(targetPath))
            {
                File.Move(tempPath, targetPath);
                return;
            }

            try
            {
                File.Replace(tempPath, targetPath, null);
            }
            catch (IOException)
            {
                ReplaceWithRecoverableMove(tempPath, targetPath);
            }
            catch (NotSupportedException)
            {
                ReplaceWithRecoverableMove(tempPath, targetPath);
            }
        }

        /// <summary>
        /// 在宿主文件系统不支持 File.Replace 时先保留旧文件，再提交新文件；提交失败会恢复旧文件。
        /// </summary>
        /// <param name="tempPath">已完成写入的临时文件。</param>
        /// <param name="targetPath">正式目标文件。</param>
        private static void ReplaceWithRecoverableMove(string tempPath, string targetPath)
        {
            var backupPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".bak";
            File.Move(targetPath, backupPath);
            try
            {
                File.Move(tempPath, targetPath);
            }
            catch (Exception moveException)
            {
                RestoreBackup(targetPath, backupPath, moveException);
                throw;
            }

            DeleteBackupFile(backupPath);
        }

        /// <summary>
        /// 新文件提交失败时恢复同目录备份；恢复也失败时同时保留两段异常证据和备份路径。
        /// </summary>
        /// <param name="targetPath">正式目标文件。</param>
        /// <param name="backupPath">旧文件备份。</param>
        /// <param name="moveException">新文件提交异常。</param>
        private static void RestoreBackup(string targetPath, string backupPath, Exception moveException)
        {
            try
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                File.Move(backupPath, targetPath);
            }
            catch (Exception restoreException)
            {
                throw new IOException(
                    "FileBridge replacement and rollback both failed; backup remains at: " + backupPath,
                    new AggregateException(moveException, restoreException));
            }
        }

        /// <summary>
        /// 删除已成功提交后的旧文件备份；失败时保留备份并记录诊断，不把已提交的新文件误报为失败。
        /// </summary>
        /// <param name="backupPath">待删除的旧文件备份。</param>
        private static void DeleteBackupFile(string backupPath)
        {
            try
            {
                DeleteTempFile(backupPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("YokiFrame FileBridge backup cleanup failed: " + exception.Message);
            }
        }

        /// <summary>
        /// 删除未成功替换的临时文件，避免后续扫描误判。
        /// </summary>
        /// <param name="tempPath">临时文件路径。</param>
        private static void DeleteTempFile(string tempPath)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

    }
}

#endif
