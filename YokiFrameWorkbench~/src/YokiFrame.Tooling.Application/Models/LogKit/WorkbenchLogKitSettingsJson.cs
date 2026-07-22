using System.Globalization;
using System.Text.Json.Nodes;
using YokiFrame.Protocol.Common;

namespace YokiFrame.Tooling.Application.Models.LogKit;

/// <summary>集中构造和校验 Application 所有 LogKit 设置 payload。</summary>
internal static class WorkbenchLogKitSettingsJson
{
    private const int MAX_DIRECTORY_LENGTH = 4096;
    private const int MAX_FILE_NAME_LENGTH = 255;
    private static readonly char[] sInvalidPathChars = Path.GetInvalidPathChars();

    /// <summary>验证完整设置，失败时返回可显示诊断。</summary>
    internal static bool TryValidate(WorkbenchLogKitSettings settings, out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (NormalizeLevel(settings.MinimumLevel) == null)
        {
            errorMessage = "LogKit minimumLevel must be Debug, Info, Warning or Error.";
            return false;
        }

        if (!ValidateNumbers(settings, out errorMessage)
            || !ValidateDirectory(settings.LogDirectory, out errorMessage)
            || !ValidateFileName(settings.EditorFileName, "editorFileName", out errorMessage)
            || !ValidateFileName(settings.PlayerFileName, "playerFileName", out errorMessage))
        {
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>构造 set_settings 使用的顶层完整 JSON 对象。</summary>
    internal static string CreateCommandPayload(WorkbenchLogKitSettings settings)
    {
        EnsureValid(settings);
        return CreateJsonObject(settings).ToJsonString(YokiFrameJson.CompactOptions);
    }

    /// <summary>构造 Player Runtime Settings 稀疏条目使用的稳定字符串值。</summary>
    internal static IReadOnlyList<KeyValuePair<string, string>> CreateRuntimeStringValues(
        WorkbenchLogKitSettings settings)
    {
        EnsureValid(settings);
        return new KeyValuePair<string, string>[]
        {
            new("enabled", BooleanText(settings.Enabled)),
            new("minimumLevel", NormalizeLevel(settings.MinimumLevel)!),
            new("saveLogInPlayer", BooleanText(settings.SaveLogInPlayer)),
            new("enableIMGUIInPlayer", BooleanText(settings.EnableIMGUIInPlayer)),
            new("enableEncryption", BooleanText(settings.EnableEncryption)),
            new("maxQueueSize", IntegerText(settings.MaxQueueSize)),
            new("maxSameLogCount", IntegerText(settings.MaxSameLogCount)),
            new("maxRetentionDays", IntegerText(settings.MaxRetentionDays)),
            new("maxFileSizeMB", IntegerText(settings.MaxFileSizeMB)),
            new("imguiMaxLogCount", IntegerText(settings.ImguiMaxLogCount)),
            new("logDirectory", settings.LogDirectory),
            new("playerFileName", settings.PlayerFileName)
        };
    }

    /// <summary>构造 Unity Editor 项目配置使用的稳定字符串值。</summary>
    internal static IReadOnlyList<KeyValuePair<string, string>> CreateEditorStringValues(
        WorkbenchLogKitSettings settings)
    {
        EnsureValid(settings);
        return new KeyValuePair<string, string>[]
        {
            new("saveLogInEditor", BooleanText(settings.SaveLogInEditor)),
            new("editorFileName", settings.EditorFileName)
        };
    }

    /// <summary>构造 System.Text.Json 节点，不允许 UI 手写协议 JSON。</summary>
    private static JsonObject CreateJsonObject(WorkbenchLogKitSettings settings)
    {
        return new JsonObject
        {
            ["enabled"] = settings.Enabled,
            ["minimumLevel"] = NormalizeLevel(settings.MinimumLevel),
            ["saveLogInEditor"] = settings.SaveLogInEditor,
            ["saveLogInPlayer"] = settings.SaveLogInPlayer,
            ["enableIMGUIInPlayer"] = settings.EnableIMGUIInPlayer,
            ["enableEncryption"] = settings.EnableEncryption,
            ["maxQueueSize"] = settings.MaxQueueSize,
            ["maxSameLogCount"] = settings.MaxSameLogCount,
            ["maxRetentionDays"] = settings.MaxRetentionDays,
            ["maxFileSizeMB"] = settings.MaxFileSizeMB,
            ["imguiMaxLogCount"] = settings.ImguiMaxLogCount,
            ["logDirectory"] = settings.LogDirectory,
            ["editorFileName"] = settings.EditorFileName,
            ["playerFileName"] = settings.PlayerFileName
        };
    }

    /// <summary>验证全部数值下限。</summary>
    private static bool ValidateNumbers(WorkbenchLogKitSettings settings, out string errorMessage)
    {
        if (settings.MaxQueueSize < 1
            || settings.MaxSameLogCount < 0
            || settings.MaxRetentionDays < 1
            || settings.MaxFileSizeMB < 1
            || settings.ImguiMaxLogCount < 1)
        {
            errorMessage = "LogKit numeric settings are outside their supported ranges.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>按 Core 同一规则验证可选日志目录，空字符串表示当前宿主默认目录。</summary>
    private static bool ValidateDirectory(string value, out string errorMessage)
    {
        if (value == null
            || value.Length > MAX_DIRECTORY_LENGTH
            || value.IndexOfAny(sInvalidPathChars) >= 0)
        {
            errorMessage = "LogKit logDirectory is invalid.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>按 Core 同一规则验证不包含目录的单段文件名。</summary>
    private static bool ValidateFileName(string value, string name, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MAX_FILE_NAME_LENGTH
            || value is "." or ".."
            || !string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal)
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            errorMessage = "LogKit " + name + " must be a plain file name.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>验证设置并在调用契约错误时抛出参数异常。</summary>
    private static void EnsureValid(WorkbenchLogKitSettings settings)
    {
        if (!TryValidate(settings, out var errorMessage))
        {
            throw new ArgumentException(errorMessage, nameof(settings));
        }
    }

    /// <summary>按 Core 忽略大小写规则归一化日志等级。</summary>
    internal static string? NormalizeLevel(string value)
    {
        if (string.Equals(value, "Debug", StringComparison.OrdinalIgnoreCase)) return "Debug";
        if (string.Equals(value, "Info", StringComparison.OrdinalIgnoreCase)) return "Info";
        if (string.Equals(value, "Warning", StringComparison.OrdinalIgnoreCase)) return "Warning";
        return string.Equals(value, "Error", StringComparison.OrdinalIgnoreCase) ? "Error" : null;
    }

    /// <summary>转换稳定小写布尔文本。</summary>
    private static string BooleanText(bool value) => value ? "true" : "false";

    /// <summary>转换区域无关整数文本。</summary>
    private static string IntegerText(int value) => value.ToString(CultureInfo.InvariantCulture);
}
