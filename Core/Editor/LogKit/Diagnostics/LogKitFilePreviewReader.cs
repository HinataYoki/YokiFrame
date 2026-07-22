#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 只在显式命令中从已配置 Editor/Player 文件尾部读取有限字节，不参与周期状态刷新。
    /// </summary>
    internal static class LogKitFilePreviewReader
    {
        private const int MAX_PREVIEW_BYTES = 48 * 1024;
        private static readonly UTF8Encoding sUtf8 = new UTF8Encoding(false, false);

        /// <summary>
        /// 读取指定 kind 的有界尾部预览；文件缺失或读取失败仍返回固定响应对象。
        /// </summary>
        /// <param name="kind">只允许 editor 或 player。</param>
        /// <returns>文件元数据、尾部内容和可显示错误。</returns>
        internal static LogKitFilePreview Read(string kind)
        {
            var preview = new LogKitFilePreview { Kind = kind ?? string.Empty };
            if (!LogKitHostEnvironment.TryGetFilePath(kind, out var path))
            {
                preview.ErrorMessage = "LogKit file preview is unavailable for the current runtime.";
                return preview;
            }

            preview.Path = path;
            preview.FileName = Path.GetFileName(path);
            try
            {
                PopulatePreview(preview);
            }
            catch (Exception exception)
            {
                preview.ErrorMessage = exception.Message;
            }

            return preview;
        }

        /// <summary>读取文件元数据，并在文件存在时读取固定大小尾部。</summary>
        private static void PopulatePreview(LogKitFilePreview preview)
        {
            FileInfo info = new FileInfo(preview.Path);
            preview.Exists = info.Exists;
            if (!info.Exists)
            {
                return;
            }

            preview.SizeBytes = info.Length;
            preview.ModifiedUtc = info.LastWriteTimeUtc.ToString("O");
            preview.Content = ReadTail(preview.Path, info.Length, out var truncated);
            preview.Truncated = truncated;
            preview.LineCount = CountLines(preview.Content);
        }

        /// <summary>
        /// 使用共享读取方式取得文件尾部，允许仍在写入的日志文件被安全预览。
        /// </summary>
        private static string ReadTail(string path, long fileLength, out bool truncated)
        {
            int readCount = (int)Math.Min(fileLength, MAX_PREVIEW_BYTES);
            long start = Math.Max(0L, fileLength - readCount);
            byte[] buffer = new byte[readCount];
            using FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(start, SeekOrigin.Begin);
            int offset = ReadBytes(stream, buffer);
            truncated = start > 0L;
            string text = sUtf8.GetString(buffer, 0, offset);
            return truncated ? RemovePartialFirstLine(text) : text;
        }

        /// <summary>循环读取短读流，直到缓冲区填满或到达当前文件尾。</summary>
        private static int ReadBytes(Stream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int count = stream.Read(buffer, offset, buffer.Length - offset);
                if (count <= 0)
                {
                    break;
                }

                offset += count;
            }

            return offset;
        }

        /// <summary>尾读从文件中部开始时丢弃第一条不完整日志行。</summary>
        private static string RemovePartialFirstLine(string text)
        {
            int newline = text.IndexOf('\n');
            return newline >= 0 ? text.Substring(newline + 1) : string.Empty;
        }

        /// <summary>统计当前有界预览中的可见行数，不扫描文件其余内容。</summary>
        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int count = 1;
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] == '\n' && index + 1 < text.Length)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
#endif
