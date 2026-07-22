using YokiFrame.Tooling.Application.Models.LogKit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 LogKit 文件预览的来源切换、宿主换代和晚到结果拒绝。</summary>
public sealed class LogKitAsyncLifecycleTests
{
    /// <summary>验证旧 Editor 请求不会覆盖后选中的 Player 文件。</summary>
    [Fact]
    public async Task OlderFileResultCannotOverwriteNewSource()
    {
        TaskCompletionSource<WorkbenchLogKitFilePreview> editor = CreatePreviewSource();
        TaskCompletionSource<WorkbenchLogKitFilePreview> player = CreatePreviewSource();
        using var viewModel = LogKitContractTestData.CreateViewModel(
            read: (_, kind, _) => kind == "editor" ? editor.Task : player.Task);
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState());
        LogKitContractTestData.SetPageActive(viewModel, true);

        viewModel.SelectedSource = "editor";
        viewModel.SelectedSource = "player";
        player.SetResult(LogKitContractTestData.CreatePreview("player", "player-new"));
        await LogKitContractTestData.WaitUntilAsync(() => viewModel.FilePreviewContent == "player-new");
        editor.SetResult(LogKitContractTestData.CreatePreview("editor", "editor-old"));
        await Task.Delay(30);

        Assert.True(viewModel.IsPlayerSource);
        Assert.Equal("player-new", viewModel.FilePreviewContent);
    }

    /// <summary>验证 engine/session/generation 换代后旧宿主文件结果失效。</summary>
    [Fact]
    public async Task HostIdentityChangeRejectsOldFileResult()
    {
        TaskCompletionSource<WorkbenchLogKitFilePreview> oldHost = CreatePreviewSource();
        TaskCompletionSource<WorkbenchLogKitFilePreview> newHost = CreatePreviewSource();
        using var viewModel = LogKitContractTestData.CreateViewModel(
            read: (engineId, _, _) => engineId == "unity-editor" ? oldHost.Task : newHost.Task);
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState());
        LogKitContractTestData.SetPageActive(viewModel, true);
        viewModel.SelectedSource = "editor";

        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(
            engineId: "unity-player",
            sessionId: "new-session",
            generation: 8L));
        newHost.SetResult(LogKitContractTestData.CreatePreview("editor", "new-host"));
        await LogKitContractTestData.WaitUntilAsync(() => viewModel.FilePreviewContent == "new-host");
        oldHost.SetResult(LogKitContractTestData.CreatePreview("editor", "old-host"));
        await Task.Delay(30);

        Assert.Equal("unity-player", viewModel.EngineId);
        Assert.Equal("new-host", viewModel.FilePreviewContent);
    }

    /// <summary>创建不会因页面取消而自动完成的异步源，用于模拟晚到 IO。</summary>
    private static TaskCompletionSource<WorkbenchLogKitFilePreview> CreatePreviewSource()
    {
        return new TaskCompletionSource<WorkbenchLogKitFilePreview>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

}
