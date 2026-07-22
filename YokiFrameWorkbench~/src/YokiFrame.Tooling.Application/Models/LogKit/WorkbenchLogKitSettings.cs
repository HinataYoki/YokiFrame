namespace YokiFrame.Tooling.Application.Models.LogKit;

/// <summary>
/// 描述 LogKit 可持久化并可应用到当前 Runtime 的完整设置。
/// </summary>
public sealed record WorkbenchLogKitSettings
{
    /// <summary>获取是否启用 LogKit。</summary>
    public bool Enabled { get; init; } = true;
    /// <summary>获取最低日志等级。</summary>
    public string MinimumLevel { get; init; } = "Debug";
    /// <summary>获取 Editor 是否写入日志文件。</summary>
    public bool SaveLogInEditor { get; init; }
    /// <summary>获取 Player 是否写入日志文件。</summary>
    public bool SaveLogInPlayer { get; init; } = true;
    /// <summary>获取 Player 是否启用 IMGUI 控制台。</summary>
    public bool EnableIMGUIInPlayer { get; init; }
    /// <summary>获取宿主是否请求旧日志加密能力。</summary>
    public bool EnableEncryption { get; init; } = true;
    /// <summary>获取文件写入队列上限。</summary>
    public int MaxQueueSize { get; init; } = 20000;
    /// <summary>获取连续相同日志允许次数。</summary>
    public int MaxSameLogCount { get; init; } = 50;
    /// <summary>获取日志文件保留天数。</summary>
    public int MaxRetentionDays { get; init; } = 15;
    /// <summary>获取单文件大小上限，单位 MB。</summary>
    public int MaxFileSizeMB { get; init; } = 100;
    /// <summary>获取 Player IMGUI 最大日志条数。</summary>
    public int ImguiMaxLogCount { get; init; } = 200;
    /// <summary>获取自定义日志目录；空值由宿主解析默认目录。</summary>
    public string LogDirectory { get; init; } = string.Empty;
    /// <summary>获取 Editor 日志文件名。</summary>
    public string EditorFileName { get; init; } = "yoki_editor.log";
    /// <summary>获取 Player 日志文件名。</summary>
    public string PlayerFileName { get; init; } = "yoki_player.log";

    /// <summary>创建与 Core 契约一致的默认设置。</summary>
    /// <returns>新的默认设置。</returns>
    public static WorkbenchLogKitSettings CreateDefault()
    {
        return new WorkbenchLogKitSettings();
    }
}
