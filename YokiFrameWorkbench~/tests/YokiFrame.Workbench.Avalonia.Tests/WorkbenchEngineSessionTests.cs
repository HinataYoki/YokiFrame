using Avalonia.Threading;
using YokiFrame.Tooling.Application.Services;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench 对可恢复 engine session 的窗口、ViewModel 与标题栏契约。
/// </summary>
public sealed class WorkbenchEngineSessionTests
{
    /// <summary>
    /// 验证等待宿主上线时 selector 只显示真实 registry 条目，不插入空选项或触发切换回调。
    /// </summary>
    [Fact]
    public void UpdateDashboard_等待上线_选择器不含空项且不触发切换()
    {
        var state = CreatePendingDashboardState();
        List<string> changes = new();
        var viewModel = new WorkbenchShellViewModel(
            () => { },
            changes.Add,
            (_, _) => Task.CompletedTask);
        try
        {
            viewModel.UpdateDashboard(state);

            Assert.Equal(string.Empty, viewModel.SelectedEngineId);
            Assert.Equal(new[] { "unity-editor" }, viewModel.EngineIds);
            Assert.DoesNotContain(string.Empty, viewModel.EngineIds);
            Assert.Empty(changes);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    /// <summary>
    /// 验证已加载但尚未选择 engine 时，Header 和总览卡使用明确中性文本而不是空字符串。
    /// </summary>
    [Fact]
    public void UpdateDashboard_尚未选择引擎_使用明确中性文本()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        Dispatcher.UIThread.Invoke(static () => WorkbenchI18nService.Instance.SetCulture("zh-CN"));
        WorkbenchShellViewModel? viewModel = null;
        try
        {
            viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask);
            viewModel.UpdateDashboard(CreatePendingDashboardState());

            var engineCard = Assert.Single(viewModel.SummaryCards, static card => card.Title == "引擎");
            Assert.Equal("等待选择", engineCard.Value);
            Assert.Contains("Engine: not selected", viewModel.HeaderText, StringComparison.Ordinal);
        }
        finally
        {
            viewModel?.Dispose();
            Dispatcher.UIThread.Invoke(static () => WorkbenchI18nService.Instance.SetCulture("zh-CN"));
        }
    }

    /// <summary>
    /// 验证自动命令目录读取等待有效选择，并且每个新 engine 只自动触发一次。
    /// </summary>
    [Fact]
    public void UpdateDashboard_每个新引擎_只自动刷新一次命令目录()
    {
        List<string> commands = new();
        var viewModel = new WorkbenchShellViewModel(
            () => { },
            _ => { },
            (kit, action) =>
            {
                commands.Add(kit + "/" + action);
                return Task.CompletedTask;
            });
        try
        {
            viewModel.UpdateDashboard(CreatePendingDashboardState());
            Assert.Empty(commands);

            var unityState = CreateOnlineDashboardState("unity-editor");
            viewModel.UpdateDashboard(unityState);
            viewModel.UpdateDashboard(unityState);
            Assert.Equal(new[] { "System/list_commands" }, commands);

            viewModel.UpdateDashboard(CreateOnlineDashboardState("godot-editor"));
            Assert.Equal(new[] { "System/list_commands", "System/list_commands" }, commands);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    /// <summary>
    /// 验证 dashboard 尚未加载时，总览引擎卡显示等待发现，而不是预设 Unity session。
    /// </summary>
    [Fact]
    public void CreateSummaryCards_未加载Dashboard_引擎卡显示等待发现()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        Dispatcher.UIThread.Invoke(static () => WorkbenchI18nService.Instance.SetCulture("zh-CN"));
        WorkbenchShellViewModel? viewModel = null;
        try
        {
            viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask);
            var engineCard = Assert.Single(viewModel.SummaryCards, static card => card.Title == "引擎");

            Assert.Equal("等待发现", engineCard.Value);
            Assert.Equal("0 registry", engineCard.Detail);
        }
        finally
        {
            viewModel?.Dispose();
            Dispatcher.UIThread.Invoke(static () => WorkbenchI18nService.Instance.SetCulture("zh-CN"));
        }
    }

    /// <summary>
    /// 创建 registry 已存在但 heartbeat 未上线的真实 dashboard 状态，验证完整 Client/Application 路径。
    /// </summary>
    /// <returns>等待 engine 上线的 dashboard 状态。</returns>
    private static YokiFrame.Tooling.Application.Models.WorkbenchDashboardState CreatePendingDashboardState()
    {
        return CreateDashboardState("unity-editor", writeHeartbeat: false);
    }

    /// <summary>
    /// 创建唯一在线 engine 的真实 dashboard 状态，供自动命令目录刷新测试使用。
    /// </summary>
    /// <param name="engineId">需要上线的 engine 标识。</param>
    /// <returns>已选择指定 engine 的 dashboard 状态。</returns>
    private static YokiFrame.Tooling.Application.Models.WorkbenchDashboardState CreateOnlineDashboardState(string engineId)
    {
        return CreateDashboardState(engineId, writeHeartbeat: true);
    }

    /// <summary>
    /// 写入最小 registry 和可选 heartbeat，并通过真实 Client/Application 路径读取 dashboard。
    /// </summary>
    /// <param name="engineId">测试 engine 标识。</param>
    /// <param name="writeHeartbeat">是否写入当前 heartbeat。</param>
    /// <returns>测试 dashboard 状态。</returns>
    private static YokiFrame.Tooling.Application.Models.WorkbenchDashboardState CreateDashboardState(
        string engineId,
        bool writeHeartbeat)
    {
        var projectRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-workbench-engine-session-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRoot);
        try
        {
            var engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", engineId);
            Directory.CreateDirectory(engineRoot);
            File.WriteAllText(
                Path.Combine(engineRoot, "engine.json"),
                "{\"protocolVersion\":2,\"engineId\":\"" + engineId + "\",\"engine\":\"Test\"}");
            if (writeHeartbeat)
            {
                var statusRoot = Path.Combine(engineRoot, "status");
                Directory.CreateDirectory(statusRoot);
                File.WriteAllText(
                    Path.Combine(statusRoot, "heartbeat.json"),
                    "{\"protocolVersion\":2,\"engineId\":\"" + engineId
                    + "\",\"sessionId\":\"test\",\"generation\":1,\"mode\":\"Test\",\"sequence\":1,\"createdAtUtc\":\""
                    + DateTimeOffset.UtcNow.ToString("O") + "\"}");
            }

            return new WorkbenchDashboardService(projectRoot).LoadDashboard(string.Empty);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }
}
