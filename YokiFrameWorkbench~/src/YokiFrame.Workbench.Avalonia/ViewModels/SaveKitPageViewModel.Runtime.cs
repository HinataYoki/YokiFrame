using System.Globalization;
using YokiFrame.Tooling.Application.Models.SaveKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 SaveKit Runtime Interaction 状态，并与项目配置编辑状态保持分离。</summary>
public sealed partial class SaveKitPageViewModel
{
    private string mRuntimeEngineId = string.Empty;
    private string mRuntimeSessionId = string.Empty;
    private long mRuntimeGeneration;
    private long mRuntimeVersion;
    private bool mHasRuntimeState;
    /// <summary>缓存最近一次 Runtime 状态；语言切换时用它重投影派生文本。</summary>
    private WorkbenchSaveKitState? mLastRuntimeState;
    private string mRuntimeStatusText = GetString(NotConnectedKey, "未连接");
    private string mRuntimeStorageText = GetString(NotConfiguredKey, "未配置");
    private string mRuntimeSerializerText = GetString(NotConfiguredKey, "未配置");
    private string mRuntimeEncryptorText = GetString(NotEnabledKey, "未启用");
    private string mRuntimeAutoSaveText = GetString(NotEnabledKey, "未启用");
    private string mRuntimeMetadataText = GetString(NotReadKey, "未读取");
    private string mRuntimeWarningText = string.Empty;

    /// <summary>获取当前页面是否已收到可信的 SaveKit Runtime state。</summary>
    public bool HasRuntimeState
    {
        get => mHasRuntimeState;
        private set => SetProperty(ref mHasRuntimeState, value);
    }

    /// <summary>获取 Runtime 后端就绪或等待初始化状态。</summary>
    public string RuntimeStatusText
    {
        get => mRuntimeStatusText;
        private set => SetProperty(ref mRuntimeStatusText, value);
    }

    /// <summary>获取已存在 Storage 类型或未配置状态。</summary>
    public string RuntimeStorageText
    {
        get => mRuntimeStorageText;
        private set => SetProperty(ref mRuntimeStorageText, value);
    }

    /// <summary>获取已存在 Serializer 标识或未配置状态。</summary>
    public string RuntimeSerializerText
    {
        get => mRuntimeSerializerText;
        private set => SetProperty(ref mRuntimeSerializerText, value);
    }

    /// <summary>获取当前 Encryptor 标识或未启用状态。</summary>
    public string RuntimeEncryptorText
    {
        get => mRuntimeEncryptorText;
        private set => SetProperty(ref mRuntimeEncryptorText, value);
    }

    /// <summary>获取当前自动保存目标与间隔摘要。</summary>
    public string RuntimeAutoSaveText
    {
        get => mRuntimeAutoSaveText;
        private set => SetProperty(ref mRuntimeAutoSaveText, value);
    }

    /// <summary>获取 Runtime 已发现的 Slot 和 Global 容器头数量。</summary>
    public string RuntimeMetadataText
    {
        get => mRuntimeMetadataText;
        private set => SetProperty(ref mRuntimeMetadataText, value);
    }

    /// <summary>获取当前 Runtime snapshot 的 stale 或容器头读取警告。</summary>
    public string RuntimeWarningText
    {
        get => mRuntimeWarningText;
        private set
        {
            if (SetProperty(ref mRuntimeWarningText, value))
            {
                OnPropertyChanged(nameof(HasRuntimeWarning));
            }
        }
    }

    /// <summary>获取当前 Runtime 是否存在可显示的连接、读取或身份警告。</summary>
    public bool HasRuntimeWarning => !string.IsNullOrWhiteSpace(RuntimeWarningText);

    /// <summary>应用 Dashboard 周期状态，并拒绝同一宿主会话的旧版本。</summary>
    /// <param name="state">当前已由 Application 验证和解析的 SaveKit 状态。</param>
    public void ApplyPeriodicState(WorkbenchSaveKitState? state)
    {
        if (state == null)
        {
            ResetRuntimeState();
            return;
        }

        if (MatchesRuntimeIdentity(state) && state.Version < mRuntimeVersion)
        {
            RuntimeWarningText = state.StaleReason;
            return;
        }

        mRuntimeEngineId = state.EngineId;
        mRuntimeSessionId = state.SessionId;
        mRuntimeGeneration = state.Generation;
        mRuntimeVersion = state.Version;
        mLastRuntimeState = state;
        HasRuntimeState = true;
        ApplyRuntimeTexts(state);
    }

    /// <summary>按当前语言把 Runtime 状态投影为界面文本；供状态应用与语言切换复用。</summary>
    /// <param name="state">最近一次接受的 Runtime 状态。</param>
    private void ApplyRuntimeTexts(WorkbenchSaveKitState state)
    {
        RuntimeStatusText = state.Backend.Ready
            ? GetString("String.SaveKit.RuntimeReady", "已就绪")
            : GetString("String.SaveKit.RuntimeWaitingInit", "等待业务初始化");
        RuntimeStorageText = FormatConfiguredValue(
            state.Backend.StorageConfigured,
            state.Backend.StorageType,
            GetString("String.SaveKit.Configured", "已配置"));
        RuntimeSerializerText = FormatConfiguredValue(
            state.Backend.SerializerConfigured,
            state.Backend.SerializerId,
            GetString("String.SaveKit.Configured", "已配置"));
        RuntimeEncryptorText = string.IsNullOrWhiteSpace(state.Backend.EncryptorId)
            ? GetString(NotEnabledKey, "未启用")
            : state.Backend.EncryptorId;
        RuntimeAutoSaveText = FormatAutoSave(state.AutoSave);
        RuntimeMetadataText = FormatMetadata(state);
        RuntimeWarningText = CreateRuntimeWarning(state);
    }

    /// <summary>判断新状态是否来自当前已经显示的 Runtime 身份。</summary>
    private bool MatchesRuntimeIdentity(WorkbenchSaveKitState state)
    {
        return string.Equals(mRuntimeEngineId, state.EngineId, StringComparison.Ordinal)
            && string.Equals(mRuntimeSessionId, state.SessionId, StringComparison.Ordinal)
            && mRuntimeGeneration == state.Generation;
    }

    /// <summary>清空切换 engine 或断线后的 Runtime 事实，保留用户未保存的项目配置草稿。</summary>
    private void ResetRuntimeState()
    {
        mRuntimeEngineId = string.Empty;
        mRuntimeSessionId = string.Empty;
        mRuntimeGeneration = 0L;
        mRuntimeVersion = 0L;
        HasRuntimeState = false;
        mLastRuntimeState = null;
        RuntimeStatusText = GetString(NotConnectedKey, "未连接");
        RuntimeStorageText = GetString(NotConfiguredKey, "未配置");
        RuntimeSerializerText = GetString(NotConfiguredKey, "未配置");
        RuntimeEncryptorText = GetString(NotEnabledKey, "未启用");
        RuntimeAutoSaveText = GetString(NotEnabledKey, "未启用");
        RuntimeMetadataText = GetString(NotReadKey, "未读取");
        RuntimeWarningText = string.Empty;
    }

    /// <summary>把已配置状态和可选稳定名称格式化为紧凑界面文本。</summary>
    private static string FormatConfiguredValue(bool configured, string value, string configuredFallback)
    {
        if (!configured)
        {
            return GetString(NotConfiguredKey, "未配置");
        }

        return string.IsNullOrWhiteSpace(value) ? configuredFallback : value;
    }

    /// <summary>把自动保存目标、间隔和累计时间格式化为单行摘要。</summary>
    private static string FormatAutoSave(WorkbenchSaveKitAutoSave autoSave)
    {
        if (!autoSave.Enabled || autoSave.Target == null)
        {
            return GetString(NotEnabledKey, "未启用");
        }

        string target = autoSave.Target.Kind == "Slot"
            ? "Slot " + autoSave.Target.SlotId.ToString(CultureInfo.InvariantCulture)
            : autoSave.Target.Name;
        return target + " · " + FormatSeconds(autoSave.IntervalSeconds) + " / " + FormatSeconds(autoSave.ElapsedSeconds);
    }

    /// <summary>生成 Slot/Global 容器头覆盖率，并标记 Provider 对大列表的裁剪。</summary>
    private static string FormatMetadata(WorkbenchSaveKitState state)
    {
        string slots = state.SlotCount + " / " + state.SlotTotal + " Slot";
        string globals = state.GlobalCount + " / " + state.GlobalTotal + " Global";
        return state.SlotsTruncated || state.GlobalsTruncated
            ? slots + " · " + globals + GetString("String.SaveKit.MetadataTruncatedSuffix", " · 已裁剪")
            : slots + " · " + globals;
    }

    /// <summary>优先显示传输 stale 原因，再显示 Storage 容器头读取失败。</summary>
    private static string CreateRuntimeWarning(WorkbenchSaveKitState state)
    {
        if (!string.IsNullOrWhiteSpace(state.StaleReason))
        {
            return state.StaleReason;
        }

        return state.MetadataReadFailed
            ? GetString("String.SaveKit.MetadataReadFailed", "部分存档容器头读取失败")
            : string.Empty;
    }

    /// <summary>按当前语言重投影 Runtime 派生文本；有缓存状态时重算，否则回到占位。</summary>
    private void OnRuntimeCultureChanged()
    {
        if (mLastRuntimeState != null && HasRuntimeState)
        {
            ApplyRuntimeTexts(mLastRuntimeState);
            return;
        }

        if (!HasRuntimeState)
        {
            RuntimeStatusText = GetString(NotConnectedKey, "未连接");
            RuntimeStorageText = GetString(NotConfiguredKey, "未配置");
            RuntimeSerializerText = GetString(NotConfiguredKey, "未配置");
            RuntimeEncryptorText = GetString(NotEnabledKey, "未启用");
            RuntimeAutoSaveText = GetString(NotEnabledKey, "未启用");
            RuntimeMetadataText = GetString(NotReadKey, "未读取");
        }
    }

    /// <summary>未配置占位资源 key。</summary>
    private const string NotConfiguredKey = "String.SaveKit.NotConfigured";
    /// <summary>未启用占位资源 key。</summary>
    private const string NotEnabledKey = "String.SaveKit.NotEnabled";
    /// <summary>未读取占位资源 key。</summary>
    private const string NotReadKey = "String.SaveKit.NotRead";

    /// <summary>把有限秒数格式化为紧凑且稳定的小数文本。</summary>
    private static string FormatSeconds(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture) + " s";
    }
}
