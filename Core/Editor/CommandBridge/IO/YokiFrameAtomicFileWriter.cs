#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 提供三宿主与工具链 Client 共用的原子文本写入：同目录临时文件、落盘 flush、
    /// 原子替换；目标已存在时优先 File.Replace，平台不支持时走备份-提交-恢复路径。
    /// 唯一物理事实源，由 Unity/Godot FileBridge 与 YokiFrame.Client 源码链接复用（Adapter
    /// 经 YokiFrame.Editor 的 InternalsVisibleTo 访问），禁止在调用方再复制私有原子写实现。
    /// </summary>
    internal static class YokiFrameAtomicFileWriter
    {
        private static readonly UTF8Encoding sUtf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// 将文本以临时文件加原子替换的方式写入目标路径；写入完成前只暴露同目录临时文件。
        /// </summary>
        /// <param name="targetPath">最终目标文件完整路径。</param>
        /// <param name="contents">待写入的完整文本内容。</param>
        public static void WriteAllText(string targetPath, string contents)
        {
            var directoryPath = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new DirectoryNotFoundException("Atomic write target path has no directory: " + targetPath);
            }

            Directory.CreateDirectory(directoryPath);
            var tempPath = Path.Combine(
                directoryPath,
                Path.GetFileName(targetPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                WriteTemporaryFile(tempPath, contents);
                ReplaceFile(tempPath, targetPath);
            }
            finally
            {
                DeleteTempFile(tempPath);
            }
        }

        /// <summary>创建独占临时文件并完整落盘；flush(true) 保证掉电后不产生半写正式文件。</summary>
        private static void WriteTemporaryFile(string tempPath, string contents)
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, sUtf8NoBom))
            {
                writer.Write(contents);
                writer.Flush();
                stream.Flush(true);
            }
        }

        /// <summary>优先使用平台原子替换；不支持时通过同目录备份完成可恢复替换，失败时保留旧文件。</summary>
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
                // Mono 与部分文件系统以 IOException 表达替换不受支持；统一转入备份-提交-恢复路径。
                ReplaceWithRecoverableMove(tempPath, targetPath);
            }
            catch (NotSupportedException)
            {
                ReplaceWithRecoverableMove(tempPath, targetPath);
            }
        }

        /// <summary>在不支持 File.Replace 的宿主上先保留旧文件，再提交新文件；提交失败会恢复旧文件。</summary>
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

            // 备份清理是证据维护而非正确性步骤；失败时保留备份即可，不影响已提交的新文件。
            try
            {
                DeleteTempFile(backupPath);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>新文件提交失败时恢复同目录备份；恢复也失败时同时保留两段异常证据和备份路径。</summary>
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
                    "Atomic replacement and rollback both failed; backup remains at: " + backupPath,
                    new AggregateException(moveException, restoreException));
            }
        }

        /// <summary>删除未成功替换的临时文件或已提交后的备份，避免后续扫描误判。</summary>
        private static void DeleteTempFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
#endif