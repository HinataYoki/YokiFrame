#if !GODOT
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity Editor 侧 LogKit 命令包装器，负责打开日志目录和解密日志文件等宿主能力。
    /// </summary>
    internal sealed class UnityLogKitCommandHandler : IKitCommandHandler
    {
        private const string OPEN_LOG_FOLDER_ACTION = "open_log_folder";
        private const string DECRYPT_LOG_FILE_ACTION = "decrypt_log_file";
        private const string READ_LOG_FILE_ACTION = "read_log_file";
        private const string DECODED_LOG_SUFFIX = ".decoded.log";
        private const int MAX_LOG_PREVIEW_CHARACTERS = 256 * 1024;

        private static readonly string[] sSupportedActions =
        {
            "stats",
            "get_settings",
            "set_settings",
            "reset_settings",
            "get_history",
            "get_workbench_snapshot",
            "clear_history",
            OPEN_LOG_FOLDER_ACTION,
            DECRYPT_LOG_FILE_ACTION,
            READ_LOG_FILE_ACTION
        };

        private readonly LogKitCommandHandler mInner = new();

        internal static Func<UnityLogKitOptions> OptionsProvider { get; set; } =
            static () => UnityRuntimeSettingsBridge.GetLogKitOptions(UnityLogKitOptions.CreateDefault());

        internal static Action<string> RevealInFinder { get; set; } = EditorUtility.RevealInFinder;

        public string KitName => mInner.KitName;

        public string[] SupportedActions => sSupportedActions;

        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case OPEN_LOG_FOLDER_ACTION:
                    return OpenLogFolder();
                case DECRYPT_LOG_FILE_ACTION:
                    return DecryptLogFile(payloadJson);
                case READ_LOG_FILE_ACTION:
                    return ReadLogFile(payloadJson);
                default:
                    return mInner.HandleAction(action, payloadJson);
            }
        }

        private static string OpenLogFolder()
        {
            var options = GetOptions();
            var directory = ResolveLogDirectory(options);
            Directory.CreateDirectory(directory);

            var logFilePath = ResolveCurrentLogFile(directory, options);
            RevealPath(directory);

            return "{\"opened\":true,\"directory\":\"" +
                   JsonHelper.EscapeString(ToJsonPath(directory)) +
                   "\",\"revealedPath\":\"" +
                   JsonHelper.EscapeString(ToJsonPath(directory)) +
                   "\",\"currentLogFilePath\":\"" +
                   JsonHelper.EscapeString(ToJsonPath(logFilePath)) +
                   "\"}";
        }

        private static string ReadLogFile(string payloadJson)
        {
            var options = GetOptions();
            var directory = ResolveLogDirectory(options);
            Directory.CreateDirectory(directory);

            var inputPath = ResolveRequestedLogFile(directory, options, payloadJson);
            return BuildLogFileContentResponse(inputPath, default, false, 0);
        }

        private static string DecryptLogFile(string payloadJson)
        {
            var options = GetOptions();
            var directory = ResolveLogDirectory(options);
            Directory.CreateDirectory(directory);

            var inputPath = ResolveRequestedLogFile(directory, options, payloadJson);
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Log file does not exist", inputPath);

            var outputPath = inputPath + DECODED_LOG_SUFFIX;
            var decodedLineCount = DecodeLogFile(inputPath, outputPath);

            return BuildLogFileContentResponse(outputPath, inputPath, true, decodedLineCount);
        }

        private static int DecodeLogFile(string inputPath, string outputPath)
        {
            var lines = File.ReadAllLines(inputPath);
            var sb = new StringBuilder(Math.Max(256, lines.Length * 256));
            var decodedLineCount = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                sb.AppendLine(UnityLogKitFileWriter.DecryptString(line));
                decodedLineCount++;
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            return decodedLineCount;
        }

        private static string ResolveRequestedLogFile(string directory, UnityLogKitOptions options, string payloadJson)
        {
            var json = payloadJson ?? string.Empty;
            var requestedKind = JsonHelper.ExtractString(json, "kind");
            if (string.Equals(requestedKind, "editor", StringComparison.OrdinalIgnoreCase))
                return ResolvePathInLogDirectory(directory, options.EditorFileName);
            if (string.Equals(requestedKind, "player", StringComparison.OrdinalIgnoreCase))
                return ResolvePathInLogDirectory(directory, options.PlayerFileName);

            var requestedPath = JsonHelper.ExtractString(json, "filePath");
            if (!string.IsNullOrEmpty(requestedPath))
                return ResolvePathInLogDirectory(directory, requestedPath);

            var requestedFileName = JsonHelper.ExtractString(json, "fileName");
            if (!string.IsNullOrEmpty(requestedFileName))
                return ResolvePathInLogDirectory(directory, requestedFileName);

            return ResolveCurrentLogFile(directory, options);
        }

        private static UnityLogKitOptions GetOptions()
        {
            var provider = OptionsProvider;
            var options = provider != default ? provider() : default;
            if (options == default)
                options = UnityLogKitOptions.CreateDefault();

            options = options.Clone();
            options.Normalize();
            return options;
        }

        private static string ResolveLogDirectory(UnityLogKitOptions options)
        {
            var directory = options.ResolveLogDirectory();
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Log directory is empty");

            return Path.GetFullPath(directory);
        }

        private static string ResolveCurrentLogFile(string directory, UnityLogKitOptions options)
        {
            var fileName = Application.isEditor ? options.EditorFileName : options.PlayerFileName;
            if (string.IsNullOrEmpty(fileName))
                fileName = Application.isEditor
                    ? LogKitSettings.DEFAULT_EDITOR_FILE_NAME
                    : LogKitSettings.DEFAULT_PLAYER_FILE_NAME;

            return ResolvePathInLogDirectory(directory, fileName);
        }

        private static string BuildLogFileContentResponse(string filePath, string inputPath, bool decrypted, int decodedLineCount)
        {
            var exists = File.Exists(filePath);
            var content = string.Empty;
            var truncated = false;
            var lineCount = decodedLineCount;
            long sizeBytes = 0;
            var modifiedUtc = string.Empty;

            if (exists)
            {
                var info = new FileInfo(filePath);
                sizeBytes = info.Length;
                modifiedUtc = info.LastWriteTimeUtc.ToString("O");
                content = ReadLogPreviewText(filePath, out truncated);
                if (lineCount <= 0)
                    lineCount = CountLines(content);
            }

            var sb = new StringBuilder(Math.Min(content.Length + 256, MAX_LOG_PREVIEW_CHARACTERS + 512));
            AppendLogFileMetadata(sb, exists, decrypted, filePath, sizeBytes, modifiedUtc, lineCount, truncated);

            if (decrypted)
            {
                sb.Append(",\"outputPath\":\"");
                sb.Append(JsonHelper.EscapeString(ToJsonPath(filePath)));
                sb.Append('"');
            }

            if (!string.IsNullOrEmpty(inputPath))
            {
                sb.Append(",\"inputPath\":\"");
                sb.Append(JsonHelper.EscapeString(ToJsonPath(inputPath)));
                sb.Append('"');
            }

            sb.Append(",\"content\":\"");
            sb.Append(JsonHelper.EscapeString(content));
            sb.Append("\"}");
            return sb.ToString();
        }

        private static void AppendLogFileMetadata(
            StringBuilder sb,
            bool exists,
            bool decrypted,
            string filePath,
            long sizeBytes,
            string modifiedUtc,
            int lineCount,
            bool truncated)
        {
            sb.Append("{\"exists\":");
            sb.Append(exists ? "true" : "false");
            sb.Append(",\"decrypted\":");
            sb.Append(decrypted ? "true" : "false");
            sb.Append(",\"path\":\"");
            sb.Append(JsonHelper.EscapeString(ToJsonPath(filePath)));
            sb.Append("\",\"fileName\":\"");
            sb.Append(JsonHelper.EscapeString(Path.GetFileName(filePath)));
            sb.Append("\",\"sizeBytes\":");
            sb.Append(sizeBytes);
            sb.Append(",\"modifiedUtc\":\"");
            sb.Append(JsonHelper.EscapeString(modifiedUtc));
            sb.Append("\",\"lineCount\":");
            sb.Append(lineCount);
            sb.Append(",\"truncated\":");
            sb.Append(truncated ? "true" : "false");
        }

        private static string ReadLogPreviewText(string filePath, out bool truncated)
        {
            var text = File.ReadAllText(filePath, Encoding.UTF8);
            truncated = text.Length > MAX_LOG_PREVIEW_CHARACTERS;
            if (!truncated)
                return text;

            return text.Substring(text.Length - MAX_LOG_PREVIEW_CHARACTERS);
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var count = 1;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                    count++;
            }
            return count;
        }

        private static string ResolvePathInLogDirectory(string directory, string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Log file path is empty", nameof(path));

            var fullDirectory = Path.GetFullPath(directory);
            var fullPath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(fullDirectory, path));

            if (!IsPathInDirectory(fullPath, fullDirectory))
                throw new InvalidOperationException("Log file path is outside the configured LogKit directory");

            return fullPath;
        }

        private static bool IsPathInDirectory(string fullPath, string fullDirectory)
        {
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            var normalizedDirectory = fullDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullPath, normalizedDirectory, comparison))
                return true;

            var prefix = normalizedDirectory + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(prefix, comparison);
        }

        private static string ToJsonPath(string path) =>
            string.IsNullOrEmpty(path) ? string.Empty : Path.GetFullPath(path).Replace('\\', '/');

        private static void RevealPath(string path)
        {
            var reveal = RevealInFinder;
            if (reveal != default)
                reveal(path);
        }
    }
}
#endif
