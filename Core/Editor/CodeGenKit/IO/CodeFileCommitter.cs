using System;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 负责生成文件的内容比较与持久化提交；实际落盘复用 CommandBridge 的共享原子写唯一实现，
    /// 本类型只保留"变更检测 + 三态结果"这一 CodeGen 特有职责。
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
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            bool targetExists = File.Exists(targetPath);
            if (targetExists && HasSameContent(targetPath, sUtf8WithoutBom.GetBytes(source)))
            {
                return CodeGenerationFileResult.Unchanged;
            }

            // 共享写入器内部完成同目录临时文件、flush(true) 落盘、原子替换与失败恢复。
            YokiFrameAtomicFileWriter.WriteAllText(targetPath, source);
            return targetExists ? CodeGenerationFileResult.Updated : CodeGenerationFileResult.Created;
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
    }
}
