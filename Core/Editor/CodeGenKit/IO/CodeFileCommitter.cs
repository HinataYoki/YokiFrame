using System;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 负责生成文件的内容比较、持久化 flush 和同目录原子提交。
    /// </summary>
    internal static class CodeFileCommitter
    {
        private static readonly UTF8Encoding sUtf8WithoutBom = new UTF8Encoding(false);

        /// <summary>
        /// 将已完整渲染的源码提交到目标文件，失败时保留原正式文件。
        /// </summary>
        /// <param name="filePath">目标文件路径，可为相对路径。</param>
        /// <param name="source">已完成渲染的源码。</param>
        /// <returns>创建、更新或无变化结果。</returns>
        internal static CodeGenerationFileResult Commit(string filePath, string source)
        {
            string targetPath = NormalizeTargetPath(filePath);
            byte[] payload = sUtf8WithoutBom.GetBytes(source ?? throw new ArgumentNullException(nameof(source)));
            bool targetExists = File.Exists(targetPath);
            if (targetExists && HasSameContent(targetPath, payload))
            {
                return CodeGenerationFileResult.Unchanged;
            }

            string directory = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(directory);
            string tempPath = CreateTempPath(directory, Path.GetFileName(targetPath));
            try
            {
                WriteAndFlush(tempPath, payload);
                CommitTempFile(tempPath, targetPath, targetExists);
                return targetExists ? CodeGenerationFileResult.Updated : CodeGenerationFileResult.Created;
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        /// <summary>
        /// 规范化并验证目标文件路径，拒绝空路径和只指向目录的输入。
        /// </summary>
        /// <param name="filePath">调用方输入路径。</param>
        /// <returns>规范化绝对文件路径。</returns>
        private static string NormalizeTargetPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("生成文件路径不能为空。", nameof(filePath));
            }

            string targetPath = Path.GetFullPath(filePath);
            if (string.IsNullOrEmpty(Path.GetFileName(targetPath)))
            {
                throw new ArgumentException("生成文件路径必须包含文件名。", nameof(filePath));
            }

            return targetPath;
        }

        /// <summary>
        /// 比较目标文件与 UTF-8 payload，先按长度快速拒绝再执行字节比较。
        /// </summary>
        /// <param name="path">现有目标文件。</param>
        /// <param name="payload">待提交 UTF-8 字节。</param>
        /// <returns>字节完全一致时返回 true。</returns>
        private static bool HasSameContent(string path, byte[] payload)
        {
            FileInfo info = new FileInfo(path);
            if (info.Length != payload.Length)
            {
                return false;
            }

            byte[] existing = File.ReadAllBytes(path);
            for (var index = 0; index < existing.Length; index++)
            {
                if (existing[index] != payload[index])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 在目标目录创建不可预测的临时文件名，保证后续提交不跨卷。
        /// </summary>
        /// <param name="directory">目标文件所在目录。</param>
        /// <param name="fileName">目标文件名。</param>
        /// <returns>尚不存在的临时文件路径。</returns>
        private static string CreateTempPath(string directory, string fileName)
        {
            return Path.Combine(directory, "." + fileName + "." + Guid.NewGuid().ToString("N") + ".tmp");
        }

        /// <summary>
        /// 独占写入临时文件并 flush 到稳定存储，正式文件此时保持不变。
        /// </summary>
        /// <param name="tempPath">同目录临时文件路径。</param>
        /// <param name="payload">完整 UTF-8 payload。</param>
        private static void WriteAndFlush(string tempPath, byte[] payload)
        {
            using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(payload, 0, payload.Length);
                stream.Flush(true);
            }
        }

        /// <summary>
        /// 将已 flush 的临时文件提交为正式文件；更新路径不提供非原子 fallback。
        /// </summary>
        /// <param name="tempPath">已完成写入的临时文件。</param>
        /// <param name="targetPath">正式目标文件。</param>
        /// <param name="targetExists">生成前目标文件是否存在。</param>
        private static void CommitTempFile(string tempPath, string targetPath, bool targetExists)
        {
            if (targetExists)
            {
                File.Replace(tempPath, targetPath, null);
                return;
            }

            File.Move(tempPath, targetPath);
        }
    }
}
