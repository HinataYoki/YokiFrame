using System.Reflection;
using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 FsmKit 高频刷新期间的列表选择、精确详情和观测图对象身份稳定性。
/// </summary>
public sealed class FsmKitRefreshStabilityTests
{
    private const string DEFAULT_INSTANCE_ID = "fsm-default";
    private const string CHOSEN_INSTANCE_ID = "fsm-chosen";
    private static readonly DateTimeOffset sBaseTime =
        DateTimeOffset.Parse("2026-07-14T12:00:00.0000000Z");

    /// <summary>
    /// 验证全新 DTO 快照不会替换集合、同一实例列表项或当前选择对象。
    /// </summary>
    [Fact]
    public void EquivalentSnapshotsKeepMachineCollectionAndSelectionIdentity()
    {
        var firstState = CreateState(
            "snapshot", CHOSEN_INSTANCE_ID, "Y", "X", "Y", sBaseTime, "snapshot");
        var secondState = CreateState(
            "snapshot", CHOSEN_INSTANCE_ID, "Y", "X", "Y", sBaseTime, "snapshot");
        FsmKitPageViewModel viewModel = new();

        Assert.NotSame(firstState, secondState);
        Assert.NotSame(firstState.Machines, secondState.Machines);
        Assert.NotSame(firstState.Machines[1], secondState.Machines[1]);
        viewModel.ApplyPeriodicState(firstState);
        var machineCollection = viewModel.Machines;
        var selectedItem = Assert.Single(
            viewModel.Machines,
            static item => item.InstanceId == CHOSEN_INSTANCE_ID);

        viewModel.ApplyPeriodicState(secondState);

        Assert.Same(machineCollection, viewModel.Machines);
        Assert.Same(selectedItem, Assert.Single(
            viewModel.Machines,
            static item => item.InstanceId == CHOSEN_INSTANCE_ID));
        Assert.Same(selectedItem, viewModel.SelectedMachine);
        Assert.Equal(CHOSEN_INSTANCE_ID, viewModel.SelectedInstanceId);
    }

    /// <summary>
    /// 验证精确 telemetry 从 X 更新到 Y 后，低频默认总览只合并列表摘要，不回退详情。
    /// </summary>
    [Fact]
    public void DefaultOverviewDoesNotRollBackLatestSelectedTelemetryDetails()
    {
        FsmKitPageViewModel viewModel = CreateViewModelWithChosenSelection();
        viewModel.ApplyPeriodicState(CreateState(
            "telemetry", CHOSEN_INSTANCE_ID, "X", "Ready", "X", sBaseTime.AddSeconds(1), "chosen-x"));
        viewModel.ApplyPeriodicState(CreateState(
            "telemetry", CHOSEN_INSTANCE_ID, "Y", "X", "Y", sBaseTime.AddSeconds(2), "chosen-y"));
        var graphModel = viewModel.GraphModel;
        var transitions = viewModel.Transitions;
        var rawPayload = viewModel.RawPayload;
        var evidencePaths = viewModel.EvidencePaths;

        viewModel.ApplyPeriodicState(CreateState(
            "snapshot", DEFAULT_INSTANCE_ID, "X", "Idle", "Idle", sBaseTime.AddSeconds(3), "overview"));

        Assert.Equal(CHOSEN_INSTANCE_ID, viewModel.SelectedInstanceId);
        Assert.Equal("Y", viewModel.CurrentState);
        Assert.Equal("Y", viewModel.SelectedMachine?.CurrentState);
        Assert.Equal("telemetry", viewModel.Source);
        Assert.Equal("Shared Memory", viewModel.DataChannelText);
        Assert.Same(graphModel, viewModel.GraphModel);
        Assert.Same(transitions, viewModel.Transitions);
        Assert.Equal(rawPayload, viewModel.RawPayload);
        Assert.Same(evidencePaths, viewModel.EvidencePaths);
        var transition = Assert.Single(viewModel.Transitions);
        Assert.Equal("X", transition.From);
        Assert.Equal("Y", transition.To);
        AssertOnlyCurrentNode(viewModel, "Y");
    }

    /// <summary>
    /// 验证语义相同但 DTO 全新的详情帧复用单一观测图模型，避免控件重建闪烁。
    /// </summary>
    [Fact]
    public void EquivalentDetailsKeepGraphModelIdentity()
    {
        var firstState = CreateState(
            "telemetry", CHOSEN_INSTANCE_ID, "Y", "X", "Y", sBaseTime.AddSeconds(1), "chosen-y-1");
        var secondState = CreateState(
            "telemetry", CHOSEN_INSTANCE_ID, "Y", "X", "Y", sBaseTime.AddSeconds(2), "chosen-y-2");
        FsmKitPageViewModel viewModel = new();

        viewModel.ApplyPeriodicState(firstState);
        var graphModel = viewModel.GraphModel;
        viewModel.ApplyPeriodicState(secondState);

        Assert.Same(graphModel, viewModel.GraphModel);
        AssertOnlyCurrentNode(viewModel, "Y");
    }

    /// <summary>
    /// 验证 Selected.InstanceId 不匹配的 telemetry 帧不能覆盖当前实例的图、历史和来源。
    /// </summary>
    [Fact]
    public void MismatchedTelemetryInstanceDoesNotOverwriteSelectedDetails()
    {
        FsmKitPageViewModel viewModel = CreateViewModelWithChosenSelection();
        viewModel.ApplyPeriodicState(CreateState(
            "telemetry", CHOSEN_INSTANCE_ID, "Y", "X", "Y", sBaseTime.AddSeconds(1), "chosen-y"));
        var graphModel = viewModel.GraphModel;
        var transitions = viewModel.Transitions;
        var rawPayload = viewModel.RawPayload;

        viewModel.ApplyPeriodicState(CreateState(
            "telemetry", DEFAULT_INSTANCE_ID, "X", "Idle", "Idle", sBaseTime.AddSeconds(2), "wrong-instance"));

        Assert.Equal(CHOSEN_INSTANCE_ID, viewModel.SelectedInstanceId);
        Assert.Equal("Y", viewModel.CurrentState);
        Assert.Same(graphModel, viewModel.GraphModel);
        Assert.Same(transitions, viewModel.Transitions);
        Assert.Equal(rawPayload, viewModel.RawPayload);
        Assert.Equal("Shared Memory", viewModel.DataChannelText);
        Assert.Equal("Y", Assert.Single(viewModel.Transitions).To);
        AssertOnlyCurrentNode(viewModel, "Y");
    }

    /// <summary>验证真实 ListBox 在新 DTO 刷新和搜索筛选后仍保留当前项及选中状态。</summary>
    [Fact]
    public async Task ListBoxSelectionSurvivesRefreshAndSearchFiltering()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            FsmKitPageViewModel viewModel = CreateViewModelWithChosenSelection();
            FsmKitPageView view = new() { DataContext = viewModel };
            Window window = new() { Width = 1200, Height = 760, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var listBox = FindInstanceList(view);
                var selectedItem = viewModel.SelectedMachine;

                viewModel.ApplyPeriodicState(CreateState(
                    "snapshot", DEFAULT_INSTANCE_ID, "X", "Idle", "Idle", sBaseTime.AddSeconds(1), "refresh"));
                viewModel.SearchText = "DefaultFSM";
                Dispatcher.UIThread.RunJobs();

                Assert.Same(selectedItem, listBox.SelectedItem);
                Assert.Contains(selectedItem, viewModel.Machines);
                Assert.True(listBox.SelectedIndex >= 0);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>定位 FsmKit 左侧实例列表，确保测试验证真实选择模型而不是只检查 ViewModel。</summary>
    /// <param name="view">已经附着视觉树的 FsmKit 页面。</param>
    /// <returns>带稳定 AutomationId 的实例列表。</returns>
    private static ListBox FindInstanceList(FsmKitPageView view)
    {
        return Assert.Single(view.GetVisualDescendants().OfType<ListBox>(), static listBox =>
            string.Equals(
                AutomationProperties.GetAutomationId(listBox),
                "workbench.fsm.instances",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// 创建已从默认实例切换到 chosen 实例、但尚未收到精确详情的页面状态。
    /// </summary>
    /// <returns>当前选择为 chosen 的 FsmKit 页面 ViewModel。</returns>
    private static FsmKitPageViewModel CreateViewModelWithChosenSelection()
    {
        FsmKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(
            "snapshot", DEFAULT_INSTANCE_ID, "X", "Idle", "Idle", sBaseTime, "initial-overview"));
        viewModel.SelectedMachine = Assert.Single(
            viewModel.Machines,
            static item => item.InstanceId == CHOSEN_INSTANCE_ID);
        Assert.Equal(CHOSEN_INSTANCE_ID, viewModel.SelectedInstanceId);
        return viewModel;
    }

    /// <summary>
    /// 断言图中只有指定节点携带当前状态标记。
    /// </summary>
    /// <param name="viewModel">已经应用精确详情的页面状态。</param>
    /// <param name="expectedName">预期唯一当前节点名称。</param>
    private static void AssertOnlyCurrentNode(FsmKitPageViewModel viewModel, string expectedName)
    {
        var currentNodes = viewModel.GraphModel.Nodes.Where(static node => node.IsCurrent).ToArray();
        Assert.Equal(expectedName, Assert.Single(currentNodes).Name);
    }

    /// <summary>
    /// 使用真实 Application parser 从测试 JSON 创建一帧强类型状态。
    /// </summary>
    /// <param name="source">snapshot 或 telemetry 来源。</param>
    /// <param name="selectedInstanceId">payload 精确描述的实例。</param>
    /// <param name="chosenState">chosen 实例摘要中的当前状态。</param>
    /// <param name="historyFrom">转换历史起点。</param>
    /// <param name="historyTo">转换历史终点。</param>
    /// <param name="updatedAtUtc">帧更新时间。</param>
    /// <param name="evidenceName">用于区分帧的测试证据名称。</param>
    /// <returns>由生产 parser 创建的强类型状态。</returns>
    private static WorkbenchFsmKitState CreateState(
        string source,
        string selectedInstanceId,
        string chosenState,
        string historyFrom,
        string historyTo,
        DateTimeOffset updatedAtUtc,
        string evidenceName)
    {
        var payloadJson = CreatePayload(
            selectedInstanceId,
            chosenState,
            historyFrom,
            historyTo);
        var dataSource = CreateInternal<WorkbenchFsmKitDataSource>(
            "unity-editor",
            "session-refresh",
            9L,
            "PlayMode",
            updatedAtUtc,
            source,
            string.Empty,
            new[] { "test://" + evidenceName },
            string.Empty,
            payloadJson);
        return ParseState(dataSource);
    }

    /// <summary>
    /// 调用 Application 内部 parser，确保测试状态遵循真实 JSON 校验和投影规则。
    /// </summary>
    /// <param name="dataSource">携带测试 JSON 和来源元数据的数据源。</param>
    /// <returns>生产 parser 返回的 FsmKit 状态。</returns>
    private static WorkbenchFsmKitState ParseState(WorkbenchFsmKitDataSource dataSource)
    {
        var parserType = typeof(WorkbenchFsmKitState).Assembly.GetType(
            "YokiFrame.Tooling.Application.Models.FsmKit.WorkbenchFsmKitStateParser");
        Assert.NotNull(parserType);
        var parseMethod = parserType.GetMethod(
            "Parse",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(parseMethod);
        var state = parseMethod.Invoke(null, new object[] { dataSource });
        return Assert.IsType<WorkbenchFsmKitState>(state);
    }

    /// <summary>
    /// 创建包含默认实例、chosen 实例、精确状态树和单条转换历史的合法 payload。
    /// </summary>
    /// <param name="selectedInstanceId">selected 对象所属实例。</param>
    /// <param name="chosenState">chosen 摘要和详情的当前状态。</param>
    /// <param name="historyFrom">历史起点。</param>
    /// <param name="historyTo">历史终点。</param>
    /// <returns>可由 Application parser 读取的 JSON。</returns>
    private static string CreatePayload(
        string selectedInstanceId,
        string chosenState,
        string historyFrom,
        string historyTo)
    {
        var isChosen = selectedInstanceId == CHOSEN_INSTANCE_ID;
        var selectedName = isChosen ? "ChosenFSM" : "DefaultFSM";
        var selectedState = isChosen ? chosenState : "Idle";
        JsonObject payload = new()
        {
            ["fsmName"] = selectedName,
            ["instanceId"] = selectedInstanceId,
            ["fsms"] = CreateMachineSummaries(chosenState),
            ["count"] = 2,
            ["selected"] = CreateSelectedDetails(
                selectedInstanceId, selectedName, selectedState, isChosen),
            ["history"] = CreateHistory(historyFrom, historyTo),
            ["stateEvents"] = new JsonObject
            {
                ["events"] = new JsonArray(),
                ["count"] = 0
            }
        };
        return payload.ToJsonString();
    }

    /// <summary>
    /// 创建两项实例摘要，chosen 摘要可模拟低频总览落后于精确帧。
    /// </summary>
    /// <param name="chosenState">chosen 摘要当前状态。</param>
    /// <returns>协议要求的 fsms 数组。</returns>
    private static JsonArray CreateMachineSummaries(string chosenState)
    {
        return new JsonArray(
            CreateMachineSummary(DEFAULT_INSTANCE_ID, "DefaultFSM", "Idle", 0),
            CreateMachineSummary(CHOSEN_INSTANCE_ID, "ChosenFSM", chosenState, chosenState == "Y" ? 2 : 1));
    }

    /// <summary>
    /// 创建一个符合 FsmKit 摘要契约的 JSON 对象。
    /// </summary>
    /// <param name="instanceId">摘要所属稳定实例标识。</param>
    /// <param name="name">用户可见状态机名称。</param>
    /// <param name="currentState">摘要当前状态。</param>
    /// <param name="currentStateId">摘要当前状态整数标识。</param>
    /// <returns>可放入 fsms 数组的摘要对象。</returns>
    private static JsonObject CreateMachineSummary(
        string instanceId,
        string name,
        string currentState,
        int currentStateId)
    {
        return new JsonObject
        {
            ["instanceId"] = instanceId,
            ["name"] = name,
            ["machineState"] = "Running",
            ["currentState"] = currentState,
            ["currentStateId"] = currentStateId,
            ["stateCount"] = instanceId == CHOSEN_INSTANCE_ID ? 2 : 1
        };
    }

    /// <summary>
    /// 创建 selected 详情，并让 X、Y 节点的 current 标记与详情状态严格一致。
    /// </summary>
    /// <param name="instanceId">详情所属稳定实例标识。</param>
    /// <param name="name">状态机名称。</param>
    /// <param name="currentState">详情当前状态。</param>
    /// <param name="isChosen">是否创建包含 X、Y 的 chosen 状态树。</param>
    /// <returns>符合 selected 契约的详情对象。</returns>
    private static JsonObject CreateSelectedDetails(
        string instanceId,
        string name,
        string currentState,
        bool isChosen)
    {
        return new JsonObject
        {
            ["fsmName"] = name,
            ["instanceId"] = instanceId,
            ["machineState"] = "Running",
            ["currentState"] = currentState,
            ["currentStateId"] = currentState == "Y" ? 2 : currentState == "X" ? 1 : 0,
            ["stateCount"] = isChosen ? 2 : 1,
            ["states"] = isChosen
                ? new JsonArray(
                    CreateStateNode(1, "X", currentState == "X"),
                    CreateStateNode(2, "Y", currentState == "Y"))
                : new JsonArray(CreateStateNode(0, "Idle", true))
        };
    }

    /// <summary>
    /// 创建一个普通状态节点；测试不引入嵌套 FSM 子层级以聚焦刷新身份。
    /// </summary>
    /// <param name="id">节点整数标识和稳定排序。</param>
    /// <param name="name">节点名称。</param>
    /// <param name="isCurrent">节点是否为当前状态。</param>
    /// <returns>符合状态树契约的普通节点。</returns>
    private static JsonObject CreateStateNode(int id, string name, bool isCurrent)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["orderIndex"] = id,
            ["name"] = name,
            ["entryCount"] = (long)id + 1L,
            ["stateType"] = name + "State",
            ["isCurrent"] = isCurrent,
            ["isComposite"] = false
        };
    }

    /// <summary>
    /// 创建单条转换历史，便于明确识别详情是否被旧总览回退。
    /// </summary>
    /// <param name="from">转换起始状态。</param>
    /// <param name="to">转换目标状态。</param>
    /// <returns>包含一条记录的 history 容器。</returns>
    private static JsonObject CreateHistory(string from, string to)
    {
        return new JsonObject
        {
            ["history"] = new JsonArray(new JsonObject
            {
                ["from"] = from,
                ["to"] = to,
                ["time"] = "12:00:00.000"
            }),
            ["count"] = 1
        };
    }

    /// <summary>
    /// 调用 Application 模型受控内部构造方法，仅用于为真实 parser 注入数据源。
    /// </summary>
    /// <typeparam name="T">需要构造的内部模型类型。</typeparam>
    /// <param name="arguments">与内部构造签名顺序一致的参数。</param>
    /// <returns>构造成功且类型精确匹配的实例。</returns>
    private static T CreateInternal<T>(params object[] arguments)
    {
        var instance = Activator.CreateInstance(
            typeof(T),
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            arguments,
            null);
        return Assert.IsType<T>(instance);
    }
}
