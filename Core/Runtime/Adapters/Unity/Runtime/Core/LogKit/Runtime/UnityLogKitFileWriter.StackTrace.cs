#if !GODOT
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity 日志文件写入器的堆栈清理和文件修复逻辑。
    /// </summary>
    public static partial class UnityLogKitFileWriter
    {
        private static readonly Regex sStackCleanRegex =
            new(@"^\s*at\s+(.*?)(?=\s*\[|\s*in\s|<|$)", RegexOptions.Compiled);
        private static readonly char[] sNewLineChars = { '\n', '\r' };

        [ThreadStatic] private static StringBuilder sCachedStackBuilder;

        private static string CleanStackTrace(string rawStack)
        {
            if (string.IsNullOrEmpty(rawStack))
                return string.Empty;

            if (sCachedStackBuilder == null)
                sCachedStackBuilder = new(1024);
            sCachedStackBuilder.Length = 0;

            var lines = rawStack.Split(sNewLineChars, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (IsUnityStackFrame(line))
                    continue;

                var match = sStackCleanRegex.Match(line);
                sCachedStackBuilder.AppendLine(match.Success ? match.Groups[1].Value.Trim() : line.Trim());
            }

            return sCachedStackBuilder.ToString();
        }

        private static bool IsUnityStackFrame(string line)
        {
            return line.IndexOf("UnityEngine.Application", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("UnityEngine.Logger", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("UnityEngine.Debug", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("YokiFrame.UnityLogKitFileWriter", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("System.Environment", StringComparison.Ordinal) >= 0;
        }

        private static void RepairLastLine(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return;

                var info = new FileInfo(filePath);
                if (info.Length <= 0)
                    return;

                using (var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    var newline = Encoding.UTF8.GetBytes(Environment.NewLine);
                    stream.Write(newline, 0, newline.Length);
                    stream.Flush();
                }
            }
            catch
            {
            }
        }
    }
}
#endif
