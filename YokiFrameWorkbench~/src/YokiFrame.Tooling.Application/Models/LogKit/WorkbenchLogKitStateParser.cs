using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.LogKit;

/// <summary>把 schemaVersion=1 的 Runtime LogKit payload 转换为稳定 read model。</summary>
internal static class WorkbenchLogKitStateParser
{
    internal const int SCHEMA_VERSION = 1;

    /// <summary>解析完整 state；无效 JSON 转换为空状态并保留原因。</summary>
    internal static WorkbenchLogKitState Parse(WorkbenchLogKitDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        if (string.IsNullOrWhiteSpace(dataSource.RawPayloadJson))
        {
            return CreateEmpty(dataSource, "LogKit payload is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(dataSource.RawPayloadJson);
            var root = document.RootElement;
            var schemaVersion = WorkbenchLogKitJsonReader.ReadInt32(root, "schemaVersion", -1);
            if (root.ValueKind != JsonValueKind.Object || schemaVersion != SCHEMA_VERSION)
            {
                return CreateEmpty(dataSource, "LogKit schemaVersion must be 1.");
            }

            return ParseRoot(dataSource, root, schemaVersion);
        }
        catch (JsonException exception)
        {
            return CreateEmpty(dataSource, "LogKit payload is invalid JSON: " + exception.Message);
        }
    }

    /// <summary>解析已验证版本的 state 根对象。</summary>
    private static WorkbenchLogKitState ParseRoot(
        WorkbenchLogKitDataSource dataSource,
        JsonElement root,
        int schemaVersion)
    {
        var settings = ParseSettings(WorkbenchLogKitJsonReader.ReadObject(root, "settings"));
        return new WorkbenchLogKitState(
            dataSource,
            schemaVersion,
            WorkbenchLogKitJsonReader.ReadInt64(root, "diagnosticVersion"),
            WorkbenchLogKitJsonReader.ReadInt64(root, "settingsVersion"),
            settings,
            ParseStats(WorkbenchLogKitJsonReader.ReadObject(root, "stats"), settings),
            ParseCapabilities(WorkbenchLogKitJsonReader.ReadObject(root, "capabilities")),
            ParseFiles(WorkbenchLogKitJsonReader.ReadObject(root, "files")),
            ParseHistory(WorkbenchLogKitJsonReader.ReadObject(root, "history")));
    }

    /// <summary>解析完整 LogKit 设置；缺失字段使用 Core 默认值。</summary>
    internal static WorkbenchLogKitSettings ParseSettings(JsonElement element)
    {
        var defaults = WorkbenchLogKitSettings.CreateDefault();
        return defaults with
        {
            Enabled = WorkbenchLogKitJsonReader.ReadBoolean(element, "enabled", defaults.Enabled),
            MinimumLevel = WorkbenchLogKitSettingsJson.NormalizeLevel(
                WorkbenchLogKitJsonReader.ReadString(element, "minimumLevel", defaults.MinimumLevel))
                ?? defaults.MinimumLevel,
            SaveLogInEditor = WorkbenchLogKitJsonReader.ReadBoolean(element, "saveLogInEditor", defaults.SaveLogInEditor),
            SaveLogInPlayer = WorkbenchLogKitJsonReader.ReadBoolean(element, "saveLogInPlayer", defaults.SaveLogInPlayer),
            EnableIMGUIInPlayer = WorkbenchLogKitJsonReader.ReadBoolean(element, "enableIMGUIInPlayer", defaults.EnableIMGUIInPlayer),
            EnableEncryption = WorkbenchLogKitJsonReader.ReadBoolean(element, "enableEncryption", defaults.EnableEncryption),
            MaxQueueSize = WorkbenchLogKitJsonReader.ReadInt32(element, "maxQueueSize", defaults.MaxQueueSize),
            MaxSameLogCount = WorkbenchLogKitJsonReader.ReadInt32(element, "maxSameLogCount", defaults.MaxSameLogCount),
            MaxRetentionDays = WorkbenchLogKitJsonReader.ReadInt32(element, "maxRetentionDays", defaults.MaxRetentionDays),
            MaxFileSizeMB = WorkbenchLogKitJsonReader.ReadInt32(element, "maxFileSizeMB", defaults.MaxFileSizeMB),
            ImguiMaxLogCount = WorkbenchLogKitJsonReader.ReadInt32(element, "imguiMaxLogCount", defaults.ImguiMaxLogCount),
            LogDirectory = WorkbenchLogKitJsonReader.ReadString(element, "logDirectory", defaults.LogDirectory),
            EditorFileName = WorkbenchLogKitJsonReader.ReadString(element, "editorFileName", defaults.EditorFileName),
            PlayerFileName = WorkbenchLogKitJsonReader.ReadString(element, "playerFileName", defaults.PlayerFileName)
        };
    }

    /// <summary>解析运行统计。</summary>
    private static WorkbenchLogKitStats ParseStats(JsonElement element, WorkbenchLogKitSettings settings)
    {
        return new WorkbenchLogKitStats(
            WorkbenchLogKitJsonReader.ReadString(element, "loggerName", "None"),
            WorkbenchLogKitJsonReader.ReadBoolean(element, "hasLogger"),
            WorkbenchLogKitJsonReader.ReadBoolean(element, "enabled", settings.Enabled),
            WorkbenchLogKitJsonReader.ReadString(element, "minimumLevel", settings.MinimumLevel),
            WorkbenchLogKitJsonReader.ReadInt32(element, "historyCount"),
            WorkbenchLogKitJsonReader.ReadInt32(element, "droppedCount"));
    }

    /// <summary>解析宿主能力。</summary>
    private static WorkbenchLogKitCapabilities ParseCapabilities(JsonElement element)
    {
        return new WorkbenchLogKitCapabilities(
            WorkbenchLogKitJsonReader.ReadBoolean(element, "settingsApply"),
            WorkbenchLogKitJsonReader.ReadBoolean(element, "filePreview"),
            WorkbenchLogKitJsonReader.ReadBoolean(element, "fileWriter"),
            WorkbenchLogKitJsonReader.ReadBoolean(element, "playerImGui"),
            WorkbenchLogKitJsonReader.ReadBoolean(element, "encryption"));
    }

    /// <summary>解析日志目录和两类文件元数据。</summary>
    private static WorkbenchLogKitFiles ParseFiles(JsonElement element)
    {
        return new WorkbenchLogKitFiles(
            WorkbenchLogKitJsonReader.ReadString(element, "directory"),
            ParseFileMetadata(WorkbenchLogKitJsonReader.ReadObject(element, "editor"), "editor"),
            ParseFileMetadata(WorkbenchLogKitJsonReader.ReadObject(element, "player"), "player"));
    }

    /// <summary>解析一类日志文件元数据。</summary>
    private static WorkbenchLogKitFileMetadata ParseFileMetadata(JsonElement element, string fallbackKind)
    {
        return new WorkbenchLogKitFileMetadata(
            WorkbenchLogKitJsonReader.ReadString(element, "kind", fallbackKind),
            WorkbenchLogKitJsonReader.ReadString(element, "path"),
            WorkbenchLogKitJsonReader.ReadString(element, "fileName"),
            WorkbenchLogKitJsonReader.ReadBoolean(element, "exists"),
            WorkbenchLogKitJsonReader.ReadInt64(element, "sizeBytes"),
            WorkbenchLogKitJsonReader.ReadString(element, "modifiedUtc"));
    }

    /// <summary>解析有界内存日志及完整计数。</summary>
    private static WorkbenchLogKitHistory ParseHistory(JsonElement element)
    {
        var entries = ParseHistoryEntries(WorkbenchLogKitJsonReader.ReadArray(element, "entries"));
        return new WorkbenchLogKitHistory(
            entries,
            WorkbenchLogKitJsonReader.ReadInt32(element, "count", entries.Count),
            WorkbenchLogKitJsonReader.ReadInt32(element, "totalCount", entries.Count),
            WorkbenchLogKitJsonReader.ReadInt32(element, "droppedCount"),
            WorkbenchLogKitJsonReader.ReadBoolean(element, "truncated"));
    }

    /// <summary>解析历史数组并忽略非对象条目。</summary>
    private static IReadOnlyList<WorkbenchLogKitHistoryEntry> ParseHistoryEntries(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WorkbenchLogKitHistoryEntry>();
        }

        List<WorkbenchLogKitHistoryEntry> entries = new();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                entries.Add(ParseHistoryEntry(element));
            }
        }

        return entries;
    }

    /// <summary>解析单条历史日志。</summary>
    private static WorkbenchLogKitHistoryEntry ParseHistoryEntry(JsonElement element)
    {
        return new WorkbenchLogKitHistoryEntry(
            WorkbenchLogKitJsonReader.ReadString(element, "level"),
            WorkbenchLogKitJsonReader.ReadString(element, "message"),
            WorkbenchLogKitJsonReader.ReadString(element, "context"),
            WorkbenchLogKitJsonReader.ReadString(element, "exceptionType"),
            WorkbenchLogKitJsonReader.ReadString(element, "exceptionMessage"),
            WorkbenchLogKitJsonReader.ReadString(element, "stackTrace"),
            WorkbenchLogKitJsonReader.ReadString(element, "timestampUtc"));
    }

    /// <summary>创建保留来源证据的安全空状态。</summary>
    private static WorkbenchLogKitState CreateEmpty(WorkbenchLogKitDataSource dataSource, string reason)
    {
        var settings = WorkbenchLogKitSettings.CreateDefault();
        return new WorkbenchLogKitState(
            dataSource.WithStaleReason(reason),
            0,
            0L,
            0L,
            settings,
            new WorkbenchLogKitStats("None", false, settings.Enabled, settings.MinimumLevel, 0, 0),
            new WorkbenchLogKitCapabilities(false, false, false, false, false),
            ParseFiles(default),
            new WorkbenchLogKitHistory(Array.Empty<WorkbenchLogKitHistoryEntry>(), 0, 0, 0, false));
    }
}
