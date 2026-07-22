#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>保存 LogKit Workbench state 使用的完整有界快照。</summary>
    internal sealed class LogKitWorkbenchSnapshot
    {
        internal int SchemaVersion;
        internal long DiagnosticVersion;
        internal long SettingsVersion;
        internal LogKitStats Stats;
        internal LogKitSettingsSnapshot Settings;
        internal LogKitHostEnvironmentSnapshot Host;
        internal LogKitFileState EditorFile;
        internal LogKitFileState PlayerFile;
        internal LogKitEntry[] Entries;
        internal int TotalCount;
        internal int DroppedCount;
    }

    /// <summary>保存已经过类型归一化和长度约束的 LogKit 设置。</summary>
    internal sealed class LogKitSettingsSnapshot
    {
        internal bool Enabled;
        internal string MinimumLevel = string.Empty;
        internal bool SaveLogInEditor;
        internal bool SaveLogInPlayer;
        internal bool EnableImGuiInPlayer;
        internal bool EnableEncryption;
        internal int MaxQueueSize;
        internal int MaxSameLogCount;
        internal int MaxRetentionDays;
        internal int MaxFileSizeMb;
        internal int ImGuiMaxLogCount;
        internal string LogDirectory = string.Empty;
        internal string EditorFileName = string.Empty;
        internal string PlayerFileName = string.Empty;
    }

    /// <summary>保存一个宿主日志文件的轻量元数据，不读取文件正文。</summary>
    internal sealed class LogKitFileState
    {
        internal string Kind = string.Empty;
        internal string Path = string.Empty;
        internal string FileName = string.Empty;
        internal bool Exists;
        internal long SizeBytes;
        internal string ModifiedUtc = string.Empty;
    }

    /// <summary>保存显式 read_log_file 命令返回的有界文件尾部预览。</summary>
    internal sealed class LogKitFilePreview
    {
        internal string Kind = string.Empty;
        internal string Path = string.Empty;
        internal string FileName = string.Empty;
        internal bool Exists;
        internal long SizeBytes;
        internal string ModifiedUtc = string.Empty;
        internal int LineCount;
        internal bool Truncated;
        internal string Content = string.Empty;
        internal string ErrorMessage = string.Empty;
    }
}
#endif
