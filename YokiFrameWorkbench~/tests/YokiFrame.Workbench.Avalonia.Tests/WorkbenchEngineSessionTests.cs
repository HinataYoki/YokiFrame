using YokiFrame.Tooling.Application.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench 对可恢复 engine session 的窗口、ViewModel 与标题栏契约。
/// </summary>
public sealed class WorkbenchEngineSessionTests
{
    /// <summary>
    /// 验证 Workbench 窗口启动时不再预设 Unity engine，让 Application 统一执行在线选择。
    /// </summary>
    [Fact]
    public void WorkbenchWindowStartsWithoutConcreteEngineDefault()
    {
        var source = ReadProjectFile(
            "src",
            "YokiFrame.Workbench.Avalonia",
            "WorkbenchWindow.cs");

        Assert.Contains("private string mSelectedEngineId = string.Empty;", source);
        Assert.DoesNotContain("private string mSelectedEngineId = \"unity-editor\";", source);
    }

    /// <summary>
    /// 验证刷新进行中发生 engine 切换时会保留一次后续刷新，并阻止旧结果覆盖新选择。
    /// </summary>
    [Fact]
    public void WorkbenchWindowCoalescesEngineChangesDuringDashboardRefresh()
    {
        var source = ReadProjectFile(
            "src",
            "YokiFrame.Workbench.Avalonia",
            "WorkbenchWindow.cs");

        Assert.Contains("mDashboardRefreshPending", source);
        Assert.Contains("mDashboardRefreshPending = true;", source);
        Assert.Contains("string.Equals(engineId, mSelectedEngineId, StringComparison.Ordinal)", source);
        Assert.Contains("QueuePendingDashboardRefresh", source);
    }

    /// <summary>
    /// 验证等待宿主上线时 selector 只显示真实 registry 条目，不插入空选项或触发切换回调。
    /// </summary>
    [Fact]
    public void EngineSelectorExcludesEmptyPendingSelection()
    {
        var state = CreatePendingDashboardState();
        List<string> changes = new();
        var viewModel = new WorkbenchShellViewModel(
            () => { },
            changes.Add,
            (_, _) => Task.CompletedTask);

        viewModel.UpdateDashboard(state);

        Assert.Equal(string.Empty, viewModel.SelectedEngineId);
        Assert.Equal(new[] { "unity-editor" }, viewModel.EngineIds);
        Assert.DoesNotContain(string.Empty, viewModel.EngineIds);
        Assert.Empty(changes);
    }

    /// <summary>
    /// 验证已加载但尚未选择 engine 时，Header 和总览卡使用明确中性文本而不是空字符串。
    /// </summary>
    [Fact]
    public void PendingEngineSessionUsesExplicitNeutralLabels()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask);

        viewModel.UpdateDashboard(CreatePendingDashboardState());

        var engineCard = Assert.Single(viewModel.SummaryCards, static card => card.Title == "引擎");
        Assert.Equal("等待选择", engineCard.Value);
        Assert.Contains("Engine: not selected", viewModel.HeaderText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证标题栏不再暴露无效的 engine selector，当前引擎继续由会话自动选择逻辑维护。
    /// </summary>
    [Fact]
    public void AppTitleBarDoesNotExposeEngineSelector()
    {
        var xaml = ReadProjectFile(
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Components",
            "AppTitleBar.axaml");

        Assert.DoesNotContain("ItemsSource=\"{CompiledBinding EngineIds}\"", xaml);
        Assert.DoesNotContain("SelectedItem=\"{CompiledBinding SelectedEngineId, Mode=TwoWay}\"", xaml);
        Assert.DoesNotContain("选择 engine", xaml);
    }

    /// <summary>
    /// 验证自动命令目录读取等待有效选择，并且每个新 engine 只自动触发一次。
    /// </summary>
    [Fact]
    public void CommandCatalogRefreshRunsOncePerSelectedEngine()
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

        viewModel.UpdateDashboard(CreatePendingDashboardState());
        Assert.Empty(commands);

        var unityState = CreateOnlineDashboardState("unity-editor");
        viewModel.UpdateDashboard(unityState);
        viewModel.UpdateDashboard(unityState);
        Assert.Equal(new[] { "System/list_commands" }, commands);

        viewModel.UpdateDashboard(CreateOnlineDashboardState("godot-editor"));
        Assert.Equal(new[] { "System/list_commands", "System/list_commands" }, commands);
    }

    /// <summary>
    /// 验证 dashboard 尚未加载时总览使用中性占位，不向用户显示并不存在的 Unity session。
    /// </summary>
    [Fact]
    public void FrameworkOverviewDoesNotAssumeUnityEngine()
    {
        var source = ReadProjectFile(
            "src",
            "YokiFrame.Workbench.Avalonia",
            "ViewModels",
            "WorkbenchShellViewModel.Overview.cs");

        Assert.DoesNotContain("\"unity-editor\"", source);
        Assert.Contains("等待发现", source);
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

    /// <summary>
    /// 从测试输出目录向上查找 Workbench 源码树，并读取指定项目文件。
    /// </summary>
    /// <param name="segments">相对 `YokiFrameWorkbench~` 根目录的路径段。</param>
    /// <returns>目标文件文本。</returns>
    private static string ReadProjectFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var directCandidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(directCandidate))
            {
                return File.ReadAllText(directCandidate);
            }

            var workspaceCandidate = Path.Combine(
                new[] { directory.FullName, "Assets", "YokiFrame", "YokiFrameWorkbench~" }
                    .Concat(segments)
                    .ToArray());
            if (File.Exists(workspaceCandidate))
            {
                return File.ReadAllText(workspaceCandidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 Workbench 项目文件：" + string.Join("/", segments));
    }
}
