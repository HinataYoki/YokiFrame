using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 LogKit 项目设置草稿、并发指纹和显式保存流程。</summary>
public sealed partial class LogKitPageViewModel
{
    private WorkbenchLogKitSettings mBaselineSettings = WorkbenchLogKitSettings.CreateDefault();
    private string mProjectSettingsEngineId = string.Empty;
    private string mProjectSettingsPath = string.Empty;
    private string mProjectSettingsFingerprint = string.Empty;
    private string mSettingsStatusText = WorkbenchI18nService.Instance.GetString("String.LogKit.WaitingProjectConfig", "等待项目配置");
    private long mProjectSettingsIdentityVersion;
    private bool mProjectSettingsLoaded;
    private bool mProjectCanPersist;
    private bool mIsSettingsDirty;
    private bool mIsSavingSettings;

    /// <summary>获取项目配置文件路径。</summary>
    public string ProjectSettingsPath { get => mProjectSettingsPath; private set => SetProperty(ref mProjectSettingsPath, value); }
    /// <summary>获取配置草稿与保存结果状态。</summary>
    public string SettingsStatusText { get => mSettingsStatusText; private set => SetProperty(ref mSettingsStatusText, value); }
    /// <summary>获取当前项目是否允许持久化配置。</summary>
    public bool ProjectCanPersist { get => mProjectCanPersist; private set => SetProjectCanPersist(value); }
    /// <summary>获取配置草稿是否偏离权威项目设置。</summary>
    public bool IsSettingsDirty { get => mIsSettingsDirty; private set => SetSettingsDirty(value); }
    /// <summary>获取是否正在保存配置。</summary>
    public bool IsSavingSettings { get => mIsSavingSettings; private set => SetSavingSettings(value); }
    /// <summary>获取保存按钮显示文本。</summary>
    public string SaveSettingsButtonText => IsSavingSettings
        ? GetString("String.LogKit.SavingText", "保存中")
        : GetString("String.LogKit.SaveText", "保存");

    /// <summary>页面激活后按项目根读取一次小型配置，不依赖 Runtime 在线，也不参与周期 telemetry。</summary>
    private void EnsureProjectSettingsLoaded()
    {
        if (mIsDisposed
            || mProjectSettingsLoaded
            || mLoadProjectSettings == null)
        {
            return;
        }

        try
        {
            ApplyProjectSettings(mLoadProjectSettings(EngineId), true);
        }
        catch (Exception exception)
        {
            // 读取失败不能标记为已完成，否则页面会永久停留在只读状态，后续 Runtime 帧也无法触发重试。
            mProjectSettingsLoaded = false;
            ProjectCanPersist = false;
            SetSettingsStatus(string.Format(
                GetString("String.LogKit.LoadConfigFailedTemplate", "项目配置读取失败: {0}"), exception.Message));
        }
    }

    /// <summary>应用项目设置文档，并按调用场景决定是否替换用户草稿。</summary>
    private void ApplyProjectSettings(WorkbenchLogKitProjectSettings projectSettings, bool replaceDraft)
    {
        mProjectSettingsIdentityVersion++;
        mProjectSettingsLoaded = true;
        mProjectSettingsEngineId = projectSettings.EngineId;
        mProjectSettingsFingerprint = projectSettings.Fingerprint;
        mBaselineSettings = projectSettings.Settings;
        ProjectSettingsPath = projectSettings.Path;
        ProjectCanPersist = projectSettings.CanPersist;
        if (replaceDraft)
        {
            SettingsDraft.Apply(projectSettings.Settings);
        }

        UpdateSettingsDirty();
        SettingsStatusText = projectSettings.StatusMessage;
    }

    /// <summary>草稿变化后只更新内存 dirty 状态，不触发文件写入或 Runtime 命令。</summary>
    private void OnSettingsDraftChanged(object? sender, EventArgs eventArgs)
    {
        OnPropertyChanged(nameof(EncryptionToggleValue));
        UpdateSettingsDirty();
    }

    /// <summary>比较不可变设置值并刷新保存命令。</summary>
    private void UpdateSettingsDirty()
    {
        IsSettingsDirty = SettingsDraft.ToSettings() != mBaselineSettings;
    }

    /// <summary>把草稿重置为 Core 默认值，仍需用户显式保存才会生效。</summary>
    private void ResetSettingsToDefaults()
    {
        SettingsDraft.Apply(WorkbenchLogKitSettings.CreateDefault());
        UpdateSettingsDirty();
        SetSettingsStatus(GetString(
            "String.LogKit.ResetDefaultsMessage",
            "已恢复默认草稿，保存后写入项目并尝试应用到当前 Runtime"));
    }

    /// <summary>保存项目设置并独立呈现 Runtime 应用结果。</summary>
    private async Task SaveSettingsAsync()
    {
        if (mSaveSettingsAsync == null || !CanSaveSettings())
        {
            return;
        }

        var context = CaptureSettingsSaveContext();
        var token = mLifetimeCancellation.Token;
        IsSavingSettings = true;
        SetSettingsStatus(GetString("String.LogKit.SavingConfig", "正在保存项目配置..."));
        try
        {
            var result = await mSaveSettingsAsync(
                context.ProjectEngineId,
                context.SubmittedSettings,
                context.ProjectFingerprint,
                token);
            ApplySettingsSaveResult(context, result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (MatchesProjectSettingsIdentity(context))
            {
                SetSettingsStatus(string.Format(
                    GetString("String.LogKit.SaveFailedTemplate", "保存失败: {0}"), exception.Message));
            }
        }
        finally
        {
            IsSavingSettings = false;
        }
    }

    /// <summary>捕获保存期间保持稳定的项目身份、提交值和发起时 Runtime 身份。</summary>
    private SettingsSaveContext CaptureSettingsSaveContext()
    {
        return new SettingsSaveContext(
            mProjectSettingsIdentityVersion,
            mProjectSettingsEngineId,
            ProjectSettingsPath,
            mProjectSettingsFingerprint,
            SettingsDraft.ToSettings(),
            CaptureIdentity());
    }

    /// <summary>只接受仍属于当前项目代的保存结果，并独立判断 Runtime 返回状态是否仍可应用。</summary>
    private void ApplySettingsSaveResult(SettingsSaveContext context, WorkbenchLogKitSettingsSaveResult result)
    {
        if (!MatchesProjectSettingsIdentity(context))
        {
            return;
        }

        if (result.ConflictDetected)
        {
            ApplyProjectSettings(result.ProjectSettings, false);
            SetSettingsStatus(GetString(
                "String.LogKit.ConfigConflictMessage",
                "配置已被其它进程修改，当前草稿已保留；再次保存将基于最新版本。"));
            return;
        }

        if (result.ProjectSaved)
        {
            var draftUnchanged = SettingsDraft.ToSettings() == context.SubmittedSettings;
            ApplyProjectSettings(result.ProjectSettings, draftUnchanged);
        }

        var appliedToCurrentRuntime = TryApplySavedRuntimeState(context.RuntimeIdentity, result.AppliedState);
        SetSettingsStatus(CreateSaveResultText(result, appliedToCurrentRuntime));
    }

    /// <summary>仅在发起身份和返回状态都仍属于当前 Runtime 且未落后时应用命令结果。</summary>
    private bool TryApplySavedRuntimeState(HostIdentity requestIdentity, WorkbenchLogKitState? state)
    {
        if (state == null
            || !MatchesIdentity(requestIdentity.EngineId, requestIdentity.SessionId, requestIdentity.Generation)
            || !MatchesIdentity(state.EngineId, state.SessionId, state.Generation)
            || IsOlderSameHostState(state))
        {
            return false;
        }

        ApplyState(state, false);
        return true;
    }

    /// <summary>判断异步结果是否仍属于发起保存时加载的同一项目配置代。</summary>
    private bool MatchesProjectSettingsIdentity(SettingsSaveContext context)
    {
        return mProjectSettingsLoaded
            && mProjectSettingsIdentityVersion == context.ProjectIdentityVersion
            && string.Equals(ProjectSettingsPath, context.ProjectSettingsPath, StringComparison.Ordinal);
    }

    /// <summary>把项目保存和 Runtime 应用两个结果压缩成清晰的一行状态。</summary>
    private static string CreateSaveResultText(
        WorkbenchLogKitSettingsSaveResult result,
        bool appliedToCurrentRuntime)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            return result.ErrorMessage;
        }

        if (result.ProjectSaved && appliedToCurrentRuntime)
        {
            return GetString("String.LogKit.SavedApplied", "项目配置已保存，并已应用到当前 Runtime");
        }

        if (result.ProjectSaved && result.RuntimeApplied)
        {
            return GetString("String.LogKit.SavedRuntimeSwitched", "项目配置已保存；Runtime 已切换，已忽略旧实例返回状态");
        }

        return result.ProjectSaved
            ? GetString("String.LogKit.SavedNotApplied", "项目配置已保存；当前 Runtime 未应用，重连或初始化时会加载")
            : GetString("String.LogKit.SavedNotSaved", "项目配置未保存");
    }

    /// <summary>判断当前草稿是否允许写入项目。</summary>
    private bool CanSaveSettings()
    {
        return !mIsDisposed
            && !IsSavingSettings
            && mSaveSettingsAsync != null
            && ProjectCanPersist
            && IsSettingsDirty;
    }

    /// <summary>Runtime 首次连入时只更新应用目标，保留同一项目下已加载的配置与未保存草稿。</summary>
    private void RebindProjectSettingsEngine(string engineId)
    {
        mProjectSettingsEngineId = engineId;
        SaveSettingsCommand.RaiseCanExecuteChanged();
    }

    /// <summary>宿主身份变化时清空旧项目的设置边界，避免跨项目或跨 engine 串写。</summary>
    private void ResetProjectSettingsIdentity()
    {
        mProjectSettingsIdentityVersion++;
        mProjectSettingsLoaded = false;
        mProjectSettingsEngineId = string.Empty;
        mProjectSettingsFingerprint = string.Empty;
        mBaselineSettings = WorkbenchLogKitSettings.CreateDefault();
        ProjectSettingsPath = string.Empty;
        ProjectCanPersist = false;
        SettingsDraft.Apply(mBaselineSettings);
        UpdateSettingsDirty();
        SetSettingsStatus(GetString(WaitingProjectConfigKey, "等待项目配置"), isWaitingStatus: true);
    }

    /// <summary>更新项目持久化能力并刷新保存命令。</summary>
    private void SetProjectCanPersist(bool value)
    {
        if (SetProperty(ref mProjectCanPersist, value, nameof(ProjectCanPersist)))
        {
            SaveSettingsCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>更新 dirty 状态并刷新保存命令。</summary>
    private void SetSettingsDirty(bool value)
    {
        if (SetProperty(ref mIsSettingsDirty, value, nameof(IsSettingsDirty)))
        {
            SaveSettingsCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>更新保存状态及按钮文本。</summary>
    private void SetSavingSettings(bool value)
    {
        if (!SetProperty(ref mIsSavingSettings, value, nameof(IsSavingSettings)))
        {
            return;
        }

        OnPropertyChanged(nameof(SaveSettingsButtonText));
        SaveSettingsCommand.RaiseCanExecuteChanged();
    }

    /// <summary>保存操作开始时冻结项目配置代、提交值以及用于校验返回状态的 Runtime 身份。</summary>
    private readonly record struct SettingsSaveContext(
        long ProjectIdentityVersion,
        string ProjectEngineId,
        string ProjectSettingsPath,
        string ProjectFingerprint,
        WorkbenchLogKitSettings SubmittedSettings,
        HostIdentity RuntimeIdentity);

    /// <summary>写入设置状态文本并维护“等待项目配置”占位标记；仅占位随语言切换重投影。</summary>
    /// <param name="text">新的状态文本。</param>
    /// <param name="isWaitingStatus">是否为等待项目配置的占位状态。</param>
    private void SetSettingsStatus(string text, bool isWaitingStatus = false)
    {
        mIsWaitingProjectConfigStatus = isWaitingStatus;
        SettingsStatusText = text;
    }
}
