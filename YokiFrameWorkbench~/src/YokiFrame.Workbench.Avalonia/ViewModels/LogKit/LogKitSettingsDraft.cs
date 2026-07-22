using YokiFrame.Tooling.Application.Models.LogKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels.LogKit;

/// <summary>
/// 承载 LogKit 显式保存前的可编辑草稿，避免输入变化直接触发磁盘写入或 Runtime 命令。
/// </summary>
public sealed class LogKitSettingsDraft : ViewModelBase
{
    private bool mSuppressChanged;
    private bool mEnabled = true;
    private bool mSaveLogInEditor;
    private bool mSaveLogInPlayer = true;
    private bool mEnableImGuiInPlayer;
    private bool mEnableEncryption = true;
    private string mMinimumLevel = "Debug";
    private decimal mMaxQueueSize = 20000;
    private decimal mMaxSameLogCount = 50;
    private decimal mMaxRetentionDays = 15;
    private decimal mMaxFileSizeMb = 100;
    private decimal mImGuiMaxLogCount = 200;
    private string mLogDirectory = string.Empty;
    private string mEditorFileName = "yoki_editor.log";
    private string mPlayerFileName = "yoki_player.log";

    /// <summary>草稿中任一设置发生真实变化时触发。</summary>
    public event EventHandler? Changed;

    /// <summary>获取或设置是否启用 LogKit。</summary>
    public bool Enabled { get => mEnabled; set => SetDraftProperty(ref mEnabled, value); }
    /// <summary>获取或设置 Editor 文件写入。</summary>
    public bool SaveLogInEditor { get => mSaveLogInEditor; set => SetDraftProperty(ref mSaveLogInEditor, value); }
    /// <summary>获取或设置 Player 文件写入。</summary>
    public bool SaveLogInPlayer { get => mSaveLogInPlayer; set => SetDraftProperty(ref mSaveLogInPlayer, value); }
    /// <summary>获取或设置 Player IMGUI 日志面板。</summary>
    public bool EnableImGuiInPlayer { get => mEnableImGuiInPlayer; set => SetDraftProperty(ref mEnableImGuiInPlayer, value); }
    /// <summary>获取或设置日志加密请求。</summary>
    public bool EnableEncryption { get => mEnableEncryption; set => SetDraftProperty(ref mEnableEncryption, value); }
    /// <summary>获取或设置最低日志等级。</summary>
    public string MinimumLevel { get => mMinimumLevel; set => SetDraftProperty(ref mMinimumLevel, value ?? "Debug"); }
    /// <summary>获取或设置文件队列上限。</summary>
    public decimal MaxQueueSize { get => mMaxQueueSize; set => SetDraftProperty(ref mMaxQueueSize, value); }
    /// <summary>获取或设置重复日志阈值。</summary>
    public decimal MaxSameLogCount { get => mMaxSameLogCount; set => SetDraftProperty(ref mMaxSameLogCount, value); }
    /// <summary>获取或设置文件保留天数。</summary>
    public decimal MaxRetentionDays { get => mMaxRetentionDays; set => SetDraftProperty(ref mMaxRetentionDays, value); }
    /// <summary>获取或设置单文件大小上限，单位 MB。</summary>
    public decimal MaxFileSizeMb { get => mMaxFileSizeMb; set => SetDraftProperty(ref mMaxFileSizeMb, value); }
    /// <summary>获取或设置 IMGUI 最大日志条数。</summary>
    public decimal ImGuiMaxLogCount { get => mImGuiMaxLogCount; set => SetDraftProperty(ref mImGuiMaxLogCount, value); }
    /// <summary>获取或设置自定义日志目录。</summary>
    public string LogDirectory { get => mLogDirectory; set => SetDraftProperty(ref mLogDirectory, value ?? string.Empty); }
    /// <summary>获取或设置 Editor 日志文件名。</summary>
    public string EditorFileName { get => mEditorFileName; set => SetDraftProperty(ref mEditorFileName, value ?? string.Empty); }
    /// <summary>获取或设置 Player 日志文件名。</summary>
    public string PlayerFileName { get => mPlayerFileName; set => SetDraftProperty(ref mPlayerFileName, value ?? string.Empty); }

    /// <summary>
    /// 使用权威设置整体替换草稿；批量赋值期间只发布属性通知，不产生 dirty 事件。
    /// </summary>
    /// <param name="settings">Application 返回的项目设置或默认设置。</param>
    public void Apply(WorkbenchLogKitSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        mSuppressChanged = true;
        try
        {
            Enabled = settings.Enabled;
            MinimumLevel = settings.MinimumLevel;
            SaveLogInEditor = settings.SaveLogInEditor;
            SaveLogInPlayer = settings.SaveLogInPlayer;
            EnableImGuiInPlayer = settings.EnableIMGUIInPlayer;
            EnableEncryption = settings.EnableEncryption;
            MaxQueueSize = settings.MaxQueueSize;
            MaxSameLogCount = settings.MaxSameLogCount;
            MaxRetentionDays = settings.MaxRetentionDays;
            MaxFileSizeMb = settings.MaxFileSizeMB;
            ImGuiMaxLogCount = settings.ImguiMaxLogCount;
            LogDirectory = settings.LogDirectory;
            EditorFileName = settings.EditorFileName;
            PlayerFileName = settings.PlayerFileName;
        }
        finally
        {
            mSuppressChanged = false;
        }
    }

    /// <summary>
    /// 创建经过整数边界归一化的强类型设置，交由 Application 做最终校验与保存。
    /// </summary>
    /// <returns>当前草稿对应的不可变设置。</returns>
    public WorkbenchLogKitSettings ToSettings()
    {
        return new WorkbenchLogKitSettings
        {
            Enabled = Enabled,
            MinimumLevel = MinimumLevel,
            SaveLogInEditor = SaveLogInEditor,
            SaveLogInPlayer = SaveLogInPlayer,
            EnableIMGUIInPlayer = EnableImGuiInPlayer,
            EnableEncryption = EnableEncryption,
            MaxQueueSize = ToBoundedInt(MaxQueueSize, 1),
            MaxSameLogCount = ToBoundedInt(MaxSameLogCount, 0),
            MaxRetentionDays = ToBoundedInt(MaxRetentionDays, 1),
            MaxFileSizeMB = ToBoundedInt(MaxFileSizeMb, 1),
            ImguiMaxLogCount = ToBoundedInt(ImGuiMaxLogCount, 1),
            LogDirectory = LogDirectory.Trim(),
            EditorFileName = EditorFileName.Trim(),
            PlayerFileName = PlayerFileName.Trim()
        };
    }

    /// <summary>设置单个草稿字段并在非批量阶段发布 dirty 信号。</summary>
    private void SetDraftProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        if (!SetProperty(ref storage, value, propertyName) || mSuppressChanged)
        {
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>把 NumericUpDown 的十进制值收敛到协议允许的非负整数范围。</summary>
    private static int ToBoundedInt(decimal value, int minimum)
    {
        var bounded = decimal.Clamp(decimal.Truncate(value), minimum, int.MaxValue);
        return decimal.ToInt32(bounded);
    }
}
