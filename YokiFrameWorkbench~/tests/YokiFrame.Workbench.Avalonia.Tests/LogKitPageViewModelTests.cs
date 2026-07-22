using System.Reflection;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 LogKit 高频刷新、筛选、选择和配置草稿稳定性。</summary>
public sealed class LogKitPageViewModelTests
{
    /// <summary>验证等价帧复用历史行和当前选择，不清空 ObservableCollection。</summary>
    [Fact]
    public void EquivalentFramesPreserveRowsAndSelection()
    {
        WorkbenchLogKitHistoryEntry[] firstEntries =
        {
            LogKitContractTestData.CreateEntry("Warning", "slow frame", "2026-07-15T08:00:00.010Z"),
            LogKitContractTestData.CreateEntry("Info", "ready", "2026-07-15T08:00:00.000Z")
        };
        var viewModel = new LogKitPageViewModel();
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(firstEntries));
        viewModel.SelectedHistoryRow = viewModel.HistoryRows[1];
        var firstRow = viewModel.HistoryRows[0];
        var selectedRow = viewModel.SelectedHistoryRow;

        WorkbenchLogKitHistoryEntry[] equivalentEntries = firstEntries
            .Select(static item => item with { })
            .ToArray();
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(equivalentEntries));

        Assert.Same(firstRow, viewModel.HistoryRows[0]);
        Assert.Same(selectedRow, viewModel.SelectedHistoryRow);
        Assert.Equal("Shared Memory", viewModel.DataChannelText);
    }

    /// <summary>验证新增日志不会破坏旧行引用、搜索文本和等级筛选。</summary>
    [Fact]
    public void NewFramePreservesFilterAndRetainedRowIdentity()
    {
        var retainedEntry = LogKitContractTestData.CreateEntry(
            "Warning",
            "asset missing",
            "2026-07-15T08:00:00.010Z",
            "AssetLoader");
        var viewModel = new LogKitPageViewModel
        {
            HistorySearchText = "asset",
            SelectedHistoryLevel = "Warning"
        };
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(new[] { retainedEntry }));
        var retainedRow = Assert.Single(viewModel.HistoryRows);

        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(new[]
        {
            LogKitContractTestData.CreateEntry("Error", "network failed", "2026-07-15T08:00:00.020Z"),
            retainedEntry with { }
        }));

        Assert.Same(retainedRow, Assert.Single(viewModel.HistoryRows));
        Assert.Equal("asset", viewModel.HistorySearchText);
        Assert.Equal("Warning", viewModel.SelectedHistoryLevel);
    }

    /// <summary>验证旧 FileBridge 诊断版本不会覆盖已经接受的高频 telemetry。</summary>
    [Fact]
    public void OlderPeriodicVersionCannotRollBackTelemetryHistory()
    {
        var current = LogKitContractTestData.CreateEntry(
            "Info",
            "current telemetry",
            "2026-07-15T08:00:00.020Z");
        var stale = LogKitContractTestData.CreateEntry(
            "Info",
            "stale snapshot",
            "2026-07-15T08:00:00.010Z");
        var viewModel = new LogKitPageViewModel();
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(
            new[] { current },
            diagnosticVersion: 5L));

        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(
            new[] { stale },
            source: "snapshot",
            diagnosticVersion: 4L));

        Assert.Equal("current telemetry", Assert.Single(viewModel.HistoryRows).MessageText);
        Assert.Equal(5L, viewModel.DiagnosticVersion);
        Assert.Equal("Shared Memory", viewModel.DataChannelText);
    }

    /// <summary>验证周期状态不会覆盖用户尚未保存的配置草稿。</summary>
    [Fact]
    public void DirtyDraftIsNotOverwrittenByPeriodicState()
    {
        var projectSettings = WorkbenchLogKitSettings.CreateDefault() with { MaxQueueSize = 1000 };
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: _ => LogKitContractTestData.CreateProjectSettings(projectSettings));
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(settings: projectSettings));
        viewModel.SettingsDraft.MaxQueueSize = 4321;

        var runtimeSettings = projectSettings with { MaxQueueSize = 2000, MinimumLevel = "Error" };
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(settings: runtimeSettings));

        Assert.Equal(4321m, viewModel.SettingsDraft.MaxQueueSize);
        Assert.True(viewModel.IsSettingsDirty);
        Assert.Equal("Debug", viewModel.SettingsDraft.MinimumLevel);
    }

    /// <summary>验证同一 engine 的 PlayMode/domain reload 不会丢弃尚未保存的项目草稿。</summary>
    [Fact]
    public void DirtyDraftSurvivesSessionAndGenerationChangeForSameEngine()
    {
        var baseline = WorkbenchLogKitSettings.CreateDefault();
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: _ => LogKitContractTestData.CreateProjectSettings(baseline));
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(settings: baseline));
        viewModel.SettingsDraft.MaxRetentionDays = 90;

        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(
            settings: baseline,
            sessionId: "reloaded-session",
            generation: 8L));

        Assert.Equal(90m, viewModel.SettingsDraft.MaxRetentionDays);
        Assert.True(viewModel.IsSettingsDirty);
    }

    /// <summary>验证 Runtime 离线时按项目加载配置，首次连入后仍保留草稿并更新保存目标。</summary>
    [Fact]
    public async Task OfflineProjectDraftSurvivesFirstRuntimeConnection()
    {
        var baseline = WorkbenchLogKitSettings.CreateDefault();
        string? loadEngineId = null;
        string? saveEngineId = null;
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: engineId =>
            {
                loadEngineId = engineId;
                return LogKitContractTestData.CreateProjectSettings(baseline, engineId: engineId);
            },
            save: (engineId, settings, _, _) =>
            {
                saveEngineId = engineId;
                var saved = LogKitContractTestData.CreateProjectSettings(
                    settings,
                    "fingerprint-2",
                    engineId);
                return Task.FromResult(LogKitContractTestData.CreateSaveResult(saved, null));
            });

        LogKitContractTestData.SetPageActive(viewModel, true);
        Assert.Equal(string.Empty, loadEngineId);
        Assert.True(viewModel.ProjectCanPersist);
        viewModel.SettingsDraft.MaxRetentionDays = 90;

        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(settings: baseline));

        Assert.Equal(90m, viewModel.SettingsDraft.MaxRetentionDays);
        Assert.True(viewModel.IsSettingsDirty);
        await viewModel.SaveSettingsCommand.ExecuteAsync();
        Assert.Equal("unity-editor", saveEngineId);
        Assert.False(viewModel.IsSettingsDirty);
    }

    /// <summary>验证一次临时配置读取失败后，Runtime 状态到来会重新读取并恢复可写能力。</summary>
    [Fact]
    public void FailedProjectSettingsReadRetriesAfterRuntimeStateArrives()
    {
        var baseline = WorkbenchLogKitSettings.CreateDefault();
        var loadCount = 0;
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: engineId =>
            {
                loadCount++;
                if (loadCount == 1)
                {
                    throw new IOException("temporary project settings read failure");
                }

                return LogKitContractTestData.CreateProjectSettings(baseline, engineId: engineId);
            });

        LogKitContractTestData.SetPageActive(viewModel, true);
        Assert.False(viewModel.ProjectCanPersist);

        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(settings: baseline));

        Assert.Equal(2, loadCount);
        Assert.True(viewModel.ProjectCanPersist);
    }

    /// <summary>验证首次只读投影不会阻止已确认 Unity Runtime 到来后重新加载项目配置。</summary>
    [Fact]
    public void ReadOnlyOfflineProjectionReloadsForFirstUnityRuntime()
    {
        var baseline = WorkbenchLogKitSettings.CreateDefault();
        var loadEngineIds = new List<string>();
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: engineId =>
            {
                loadEngineIds.Add(engineId);
                return LogKitContractTestData.CreateProjectSettings(
                    baseline,
                    engineId: engineId,
                    canPersist: string.Equals(engineId, "unity-editor", StringComparison.Ordinal));
            });

        LogKitContractTestData.SetPageActive(viewModel, true);
        Assert.False(viewModel.ProjectCanPersist);

        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(settings: baseline));

        Assert.Equal(new[] { string.Empty, "unity-editor" }, loadEngineIds);
        Assert.True(viewModel.ProjectCanPersist);
    }

    /// <summary>验证保存成功后使用 Application 返回文档更新权威基线。</summary>
    [Fact]
    public async Task SuccessfulSaveUpdatesAuthoritativeBaseline()
    {
        var baseline = WorkbenchLogKitSettings.CreateDefault();
        WorkbenchLogKitSettings? submitted = null;
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: _ => LogKitContractTestData.CreateProjectSettings(baseline),
            save: (_, settings, _, _) =>
            {
                submitted = settings;
                var project = LogKitContractTestData.CreateProjectSettings(settings, "fingerprint-2");
                var state = LogKitContractTestData.CreateState(settings: settings, source: "command");
                return Task.FromResult(LogKitContractTestData.CreateSaveResult(project, state));
            });
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(settings: baseline));
        viewModel.SettingsDraft.MinimumLevel = "Error";

        await viewModel.SaveSettingsCommand.ExecuteAsync();

        Assert.NotNull(submitted);
        Assert.Equal("Error", submitted!.MinimumLevel);
        Assert.False(viewModel.IsSettingsDirty);
        Assert.Contains("已应用", viewModel.SettingsStatusText, StringComparison.Ordinal);
    }

    /// <summary>验证同 engine 的 Domain Reload 不取消项目保存，并拒绝旧 Runtime 状态回滚新身份。</summary>
    [Fact]
    public async Task SaveSurvivesSameEngineReloadAndIgnoresOldRuntimeState()
    {
        var baseline = WorkbenchLogKitSettings.CreateDefault();
        var completion = new TaskCompletionSource<WorkbenchLogKitSettingsSaveResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken saveToken = default;
        WorkbenchLogKitSettings? submitted = null;
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: _ => LogKitContractTestData.CreateProjectSettings(baseline),
            save: (_, settings, _, token) =>
            {
                submitted = settings;
                saveToken = token;
                return completion.Task;
            });
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(settings: baseline));
        viewModel.SettingsDraft.MinimumLevel = "Error";

        var saveTask = viewModel.SaveSettingsCommand.ExecuteAsync();
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(
            settings: baseline,
            sessionId: "reloaded-session",
            generation: 8L,
            diagnosticVersion: 10L));

        Assert.False(saveToken.IsCancellationRequested);
        Assert.NotNull(submitted);
        var savedProject = LogKitContractTestData.CreateProjectSettings(submitted!, "fingerprint-2");
        var oldRuntimeState = LogKitContractTestData.CreateState(
            settings: submitted,
            source: "command",
            diagnosticVersion: 2L);
        completion.SetResult(LogKitContractTestData.CreateSaveResult(savedProject, oldRuntimeState));
        await saveTask;

        Assert.False(viewModel.IsSettingsDirty);
        Assert.Equal("reloaded-session", viewModel.SessionId);
        Assert.Equal(8L, viewModel.Generation);
        Assert.Equal(10L, viewModel.DiagnosticVersion);
        Assert.Contains("已忽略旧实例", viewModel.SettingsStatusText, StringComparison.Ordinal);
    }

    /// <summary>验证保存期间的新编辑保留在草稿中，同时成功结果仍更新权威基线。</summary>
    [Fact]
    public async Task EditDuringSaveIsPreservedAgainstReturnedBaseline()
    {
        var baseline = WorkbenchLogKitSettings.CreateDefault();
        var completion = new TaskCompletionSource<WorkbenchLogKitSettingsSaveResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        WorkbenchLogKitSettings? submitted = null;
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: _ => LogKitContractTestData.CreateProjectSettings(baseline),
            save: (_, settings, _, _) =>
            {
                submitted = settings;
                return completion.Task;
            });
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(settings: baseline));
        viewModel.SettingsDraft.MinimumLevel = "Error";

        var saveTask = viewModel.SaveSettingsCommand.ExecuteAsync();
        viewModel.SettingsDraft.MinimumLevel = "Warning";
        Assert.NotNull(submitted);
        var savedProject = LogKitContractTestData.CreateProjectSettings(submitted!, "fingerprint-2");
        var appliedState = LogKitContractTestData.CreateState(settings: submitted, source: "command");
        completion.SetResult(LogKitContractTestData.CreateSaveResult(savedProject, appliedState));
        await saveTask;

        Assert.Equal("Warning", viewModel.SettingsDraft.MinimumLevel);
        Assert.True(viewModel.IsSettingsDirty);

        viewModel.SettingsDraft.MinimumLevel = "Error";
        Assert.False(viewModel.IsSettingsDirty);
    }

    /// <summary>验证默认草稿保留 Core 的加密默认值且不会自动保存。</summary>
    [Fact]
    public void ResetDefaultsKeepsEncryptionDefaultAndOnlyMarksDraftDirty()
    {
        var baseline = WorkbenchLogKitSettings.CreateDefault() with { EnableEncryption = false };
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: _ => LogKitContractTestData.CreateProjectSettings(baseline));
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(settings: baseline));

        viewModel.ResetSettingsCommand.Execute(null);

        Assert.True(viewModel.SettingsDraft.EnableEncryption);
        Assert.True(viewModel.IsSettingsDirty);
        Assert.Contains("保存后", viewModel.SettingsStatusText, StringComparison.Ordinal);
    }

    /// <summary>验证标题栏打开命令使用 Runtime 解析出的实际日志目录。</summary>
    [Fact]
    public async Task OpenDirectoryCommandUsesRuntimeDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "yokiframe-logkit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string? openedPath = null;
            using var viewModel = new LogKitPageViewModel();
            var setter = typeof(LogKitPageViewModel).GetMethod(
                "SetOpenDirectoryHandler",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(setter);
            setter.Invoke(viewModel, new object?[] { (Func<string, Task>)(path =>
            {
                openedPath = path;
                return Task.CompletedTask;
            }) });

            viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(directory: directory));

            Assert.True(viewModel.OpenDirectoryCommand.CanExecute(null));
            await viewModel.OpenDirectoryCommand.ExecuteAsync();
            Assert.Equal(directory, openedPath);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>验证当前未实现的可信加密会明确显示加密、解密和算法状态。</summary>
    [Fact]
    public void UnsupportedEncryptionStateIsExplicit()
    {
        var viewModel = new LogKitPageViewModel();

        Assert.Equal("当前未实现", viewModel.EncryptionStatusText);
        Assert.Equal("当前不可用", viewModel.DecryptionStatusText);
        Assert.False(viewModel.EncryptionToggleValue);
        Assert.Contains("未定义", viewModel.EncryptionMethodText, StringComparison.Ordinal);
        Assert.Contains("不会使用固定 Key/IV", viewModel.EncryptionMethodText, StringComparison.Ordinal);
    }
}
