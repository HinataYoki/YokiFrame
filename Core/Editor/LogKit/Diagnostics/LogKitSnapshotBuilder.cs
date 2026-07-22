#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 从 Core LogKit 和宿主环境构建有界 Workbench state，避免实时帧携带无界日志文本。
    /// </summary>
    internal static class LogKitSnapshotBuilder
    {
        private const int MAX_HISTORY_ENTRIES = 32;
        private const int MESSAGE_MAX_BYTES = 220;
        private const int CONTEXT_MAX_BYTES = 64;
        private const int EXCEPTION_TYPE_MAX_BYTES = 48;
        private const int EXCEPTION_MESSAGE_MAX_BYTES = 96;
        private const int STACK_TRACE_MAX_BYTES = 320;
        private const int TIMESTAMP_MAX_BYTES = 48;
        private const int DIRECTORY_MAX_BYTES = 1024;
        private const int FILE_NAME_MAX_BYTES = 256;
        private const int FILE_PATH_MAX_BYTES = 2048;
        private static readonly object sFileStateLock = new object();
        private static readonly long sFileStateRefreshTicks = TimeSpan.FromSeconds(1).Ticks;
        private static LogKitFileState sCachedEditorFile;
        private static LogKitFileState sCachedPlayerFile;
        private static string sCachedEditorPath = string.Empty;
        private static string sCachedPlayerPath = string.Empty;
        private static long sNextFileStateRefreshTicks;

        /// <summary>
        /// 创建包含设置、能力、文件元数据和最多 32 条近期日志的完整状态。
        /// </summary>
        /// <returns>可交给稳定 JSON writer 的 LogKit 快照。</returns>
        internal static LogKitWorkbenchSnapshot Create()
        {
            LogKitHostEnvironmentSnapshot host = LogKitHostEnvironment.Capture();
            LogKitEntry[] entries = CreateHistoryEntries(out var stats, out var diagnosticVersion);
            CreateFileStates(host, out var editorFile, out var playerFile);
            return new LogKitWorkbenchSnapshot
            {
                SchemaVersion = 1,
                DiagnosticVersion = diagnosticVersion,
                SettingsVersion = LogKitSettings.SettingsVersion,
                Stats = stats,
                Settings = CreateSettings(),
                Host = host,
                EditorFile = editorFile,
                PlayerFile = playerFile,
                Entries = entries,
                TotalCount = stats.HistoryCount + stats.DroppedCount,
                DroppedCount = stats.DroppedCount
            };
        }

        /// <summary>把通用 Settings Store 投影成强类型且有界的 LogKit 设置。</summary>
        /// <returns>当前进程有效设置。</returns>
        private static LogKitSettingsSnapshot CreateSettings()
        {
            return new LogKitSettingsSnapshot
            {
                Enabled = LogKitSettings.Enabled,
                MinimumLevel = LogKitSettings.MinimumLevel.ToString(),
                SaveLogInEditor = ReadBool(LogKitSettings.SAVE_LOG_IN_EDITOR_KEY, LogKitSettings.DEFAULT_SAVE_LOG_IN_EDITOR),
                SaveLogInPlayer = ReadBool(LogKitSettings.SAVE_LOG_IN_PLAYER_KEY, LogKitSettings.DEFAULT_SAVE_LOG_IN_PLAYER),
                EnableImGuiInPlayer = ReadBool(LogKitSettings.ENABLE_IMGUI_IN_PLAYER_KEY, LogKitSettings.DEFAULT_ENABLE_IMGUI_IN_PLAYER),
                EnableEncryption = ReadBool(LogKitSettings.ENABLE_ENCRYPTION_KEY, LogKitSettings.DEFAULT_ENABLE_ENCRYPTION),
                MaxQueueSize = ReadInt(LogKitSettings.MAX_QUEUE_SIZE_KEY, LogKitSettings.DEFAULT_MAX_QUEUE_SIZE),
                MaxSameLogCount = ReadInt(LogKitSettings.MAX_SAME_LOG_COUNT_KEY, LogKitSettings.DEFAULT_MAX_SAME_LOG_COUNT),
                MaxRetentionDays = ReadInt(LogKitSettings.MAX_RETENTION_DAYS_KEY, LogKitSettings.DEFAULT_MAX_RETENTION_DAYS),
                MaxFileSizeMb = ReadInt(LogKitSettings.MAX_FILE_SIZE_MB_KEY, LogKitSettings.DEFAULT_MAX_FILE_SIZE_MB),
                ImGuiMaxLogCount = ReadInt(LogKitSettings.IMGUI_MAX_LOG_COUNT_KEY, LogKitSettings.DEFAULT_IMGUI_MAX_LOG_COUNT),
                LogDirectory = LimitText(LogKitSettings.GetString(LogKitSettings.LOG_DIRECTORY_KEY, string.Empty), DIRECTORY_MAX_BYTES),
                EditorFileName = LimitText(LogKitSettings.GetString(LogKitSettings.EDITOR_FILE_NAME_KEY, LogKitSettings.DEFAULT_EDITOR_FILE_NAME), FILE_NAME_MAX_BYTES),
                PlayerFileName = LimitText(LogKitSettings.GetString(LogKitSettings.PLAYER_FILE_NAME_KEY, LogKitSettings.DEFAULT_PLAYER_FILE_NAME), FILE_NAME_MAX_BYTES)
            };
        }

        /// <summary>读取一个布尔设置，保持设置默认值只定义在 LogKitSettings。</summary>
        private static bool ReadBool(string key, bool defaultValue)
        {
            return LogKitSettings.GetBool(key, defaultValue);
        }

        /// <summary>读取一个整数设置，保持设置默认值只定义在 LogKitSettings。</summary>
        private static int ReadInt(string key, int defaultValue)
        {
            return LogKitSettings.GetInt(key, defaultValue);
        }

        /// <summary>复制最新 32 条历史，并对每个可变文本字段施加 UTF-8 字节上限。</summary>
        /// <returns>最新日志位于数组前端的独立条目。</returns>
        private static LogKitEntry[] CreateHistoryEntries(
            out LogKitStats stats,
            out long diagnosticVersion)
        {
            LogKitEntry[] entries = LogKit.GetRecentHistory(
                MAX_HISTORY_ENTRIES,
                out stats,
                out diagnosticVersion);
            for (var index = 0; index < entries.Length; index++)
            {
                BoundEntry(entries[index]);
            }

            return entries;
        }

        /// <summary>原地裁剪已经复制出的历史条目，避免为同一帧再次分配条目对象。</summary>
        private static void BoundEntry(LogKitEntry entry)
        {
            entry.Message = LimitText(entry.Message, MESSAGE_MAX_BYTES);
            entry.Context = LimitText(entry.Context, CONTEXT_MAX_BYTES);
            entry.ExceptionType = LimitText(entry.ExceptionType, EXCEPTION_TYPE_MAX_BYTES);
            entry.ExceptionMessage = LimitText(entry.ExceptionMessage, EXCEPTION_MESSAGE_MAX_BYTES);
            entry.StackTrace = LimitText(entry.StackTrace, STACK_TRACE_MAX_BYTES);
            entry.TimestampUtc = LimitText(entry.TimestampUtc, TIMESTAMP_MAX_BYTES);
        }

        /// <summary>读取文件存在性、大小和修改时间，不读取文件正文。</summary>
        private static LogKitFileState CreateFileState(string kind, string path)
        {
            LogKitFileState state = new LogKitFileState
            {
                Kind = kind,
                Path = LimitText(path, FILE_PATH_MAX_BYTES),
                FileName = LimitText(GetFileName(path), FILE_NAME_MAX_BYTES)
            };
            try
            {
                FileInfo info = new FileInfo(path ?? string.Empty);
                state.Exists = info.Exists;
                if (info.Exists)
                {
                    state.SizeBytes = info.Length;
                    state.ModifiedUtc = info.LastWriteTimeUtc.ToString("O");
                }
            }
            catch (Exception)
            {
                state.Exists = false;
            }

            return state;
        }

        /// <summary>
        /// 复用一秒内的文件元数据；路径变化时立即失效，避免高频日志刷新反复访问磁盘。
        /// </summary>
        private static void CreateFileStates(
            LogKitHostEnvironmentSnapshot host,
            out LogKitFileState editor,
            out LogKitFileState player)
        {
            lock (sFileStateLock)
            {
                long nowTicks = DateTime.UtcNow.Ticks;
                if (ShouldRefreshFileStates(host, nowTicks))
                {
                    sCachedEditorPath = host.EditorPath;
                    sCachedPlayerPath = host.PlayerPath;
                    sCachedEditorFile = CreateFileState("editor", host.EditorPath);
                    sCachedPlayerFile = CreateFileState("player", host.PlayerPath);
                    sNextFileStateRefreshTicks = nowTicks + sFileStateRefreshTicks;
                }

                editor = CloneFileState(sCachedEditorFile, "editor");
                player = CloneFileState(sCachedPlayerFile, "player");
            }
        }

        /// <summary>判断缓存是否缺失、路径已变更或低频刷新期限已到。</summary>
        private static bool ShouldRefreshFileStates(LogKitHostEnvironmentSnapshot host, long nowTicks)
        {
            return sCachedEditorFile == null
                || sCachedPlayerFile == null
                || nowTicks >= sNextFileStateRefreshTicks
                || !string.Equals(sCachedEditorPath, host.EditorPath, StringComparison.Ordinal)
                || !string.Equals(sCachedPlayerPath, host.PlayerPath, StringComparison.Ordinal);
        }

        /// <summary>复制缓存文件状态，避免调用方持有可变缓存对象。</summary>
        private static LogKitFileState CloneFileState(LogKitFileState source, string kind)
        {
            return source == null
                ? new LogKitFileState { Kind = kind }
                : new LogKitFileState
                {
                    Kind = source.Kind,
                    Path = source.Path,
                    FileName = source.FileName,
                    Exists = source.Exists,
                    SizeBytes = source.SizeBytes,
                    ModifiedUtc = source.ModifiedUtc
                };
        }

        /// <summary>安全读取路径中的文件名；空路径或损坏路径返回空字符串。</summary>
        private static string GetFileName(string path)
        {
            try
            {
                return string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 按 UTF-8 字节限制文本，并把控制字符归一为空格；已满足约束的常见文本直接复用原字符串。
        /// </summary>
        private static string LimitText(string value, int maxBytes)
        {
            if (string.IsNullOrEmpty(value) || maxBytes <= 0)
            {
                return string.Empty;
            }

            return IsBoundedText(value, maxBytes)
                ? value
                : CreateBoundedText(value, maxBytes);
        }

        /// <summary>判断文本是否无需归一化、截断或复制即可满足 UTF-8 字节边界。</summary>
        private static bool IsBoundedText(string value, int maxBytes)
        {
            int usedBytes = 0;
            for (var index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (current < ' ' || char.IsSurrogate(current) && (
                        !char.IsHighSurrogate(current)
                        || index + 1 >= value.Length
                        || !char.IsLowSurrogate(value[index + 1])))
                {
                    return false;
                }

                bool isPair = char.IsHighSurrogate(current);
                int charBytes = GetUtf8ByteCount(current, isPair);
                if (usedBytes + charBytes > maxBytes)
                {
                    return false;
                }

                usedBytes += charBytes;
                if (isPair)
                {
                    index++;
                }
            }

            return true;
        }

        /// <summary>复制并归一化超限、控制字符或代理不完整的文本，保持输出可安全编码。</summary>
        private static string CreateBoundedText(string value, int maxBytes)
        {
            var builder = new StringBuilder(Math.Min(value.Length, maxBytes));
            int usedBytes = 0;
            for (var index = 0; index < value.Length; index++)
            {
                char current = value[index] < ' ' ? ' ' : value[index];
                bool isPair = char.IsHighSurrogate(current)
                    && index + 1 < value.Length
                    && char.IsLowSurrogate(value[index + 1]);
                int charBytes = GetUtf8ByteCount(current, isPair);
                if (usedBytes + charBytes > maxBytes)
                {
                    break;
                }

                if (isPair)
                {
                    builder.Append(current);
                    builder.Append(value[++index]);
                    usedBytes += charBytes;
                    continue;
                }

                if (char.IsSurrogate(current)) current = '?';
                builder.Append(current);
                usedBytes += charBytes;
            }

            return builder.ToString();
        }

        /// <summary>计算单个 UTF-16 字符或完整代理对的 UTF-8 字节数。</summary>
        private static int GetUtf8ByteCount(char current, bool isPair)
        {
            if (isPair) return 4;
            if (char.IsSurrogate(current)) return 1;
            if (current <= 0x7f) return 1;
            return current <= 0x7ff ? 2 : 3;
        }
    }
}
#endif
