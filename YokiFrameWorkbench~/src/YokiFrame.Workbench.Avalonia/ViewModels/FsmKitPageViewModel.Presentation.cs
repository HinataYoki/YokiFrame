using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 维护 FsmKit 工作区的空状态、摘要与已观测转换图 read model。
/// </summary>
public sealed partial class FsmKitPageViewModel
{
    private const double GRAPH_MIN_RADIUS = 155.0;
    private const double GRAPH_NODE_ARC_GAP = 64.0;
    private const double GRAPH_PADDING_X = 96.0;
    private const double GRAPH_PADDING_Y = 96.0;
    private const string ROOT_MACHINE_KEY = "machine:root";
    private ObservedFsmGraphModel mGraphModel = ObservedFsmGraphModel.Empty;
    private bool mHasMachines;
    private bool mIsEmptyWorkspaceVisible = true;
    private bool mIsGraphEmpty = true;
    private string mEmptyStateTitle = "等待 FsmKit 状态";
    private string mEmptyStateDescription = "正在读取宿主发布的 FsmKit 快照。";
    private string mInstanceCountText = "0 个实例";
    private string mStateCountText = "0 个状态";
    private string mHistoryCountText = "0 条转换";
    private string mGraphEmptyHint = "选择一个实例以读取完整状态树和已观测转换。";

    /// <summary>获取当前快照是否含有至少一个活动 FSM 实例。</summary>
    public bool HasMachines { get => mHasMachines; private set => SetProperty(ref mHasMachines, value); }

    /// <summary>获取主工作区是否应显示无实例空状态。</summary>
    public bool IsEmptyWorkspaceVisible { get => mIsEmptyWorkspaceVisible; private set => SetProperty(ref mIsEmptyWorkspaceVisible, value); }

    /// <summary>获取观测图当前是否没有可绘制的节点。</summary>
    public bool IsGraphEmpty { get => mIsGraphEmpty; private set => SetProperty(ref mIsGraphEmpty, value); }

    /// <summary>获取无实例工作区的标题。</summary>
    public string EmptyStateTitle { get => mEmptyStateTitle; private set => SetProperty(ref mEmptyStateTitle, value); }

    /// <summary>获取无实例工作区的恢复说明。</summary>
    public string EmptyStateDescription { get => mEmptyStateDescription; private set => SetProperty(ref mEmptyStateDescription, value); }

    /// <summary>获取节点、边和画布尺寸属于同一代的不可变观测图模型。</summary>
    public ObservedFsmGraphModel GraphModel { get => mGraphModel; private set => SetProperty(ref mGraphModel, value); }

    /// <summary>获取当前已发现的 FSM 实例数文本。</summary>
    public string InstanceCountText { get => mInstanceCountText; private set => SetProperty(ref mInstanceCountText, value); }

    /// <summary>获取当前选中 FSM 的状态数文本。</summary>
    public string StateCountText { get => mStateCountText; private set => SetProperty(ref mStateCountText, value); }

    /// <summary>获取当前选中 FSM 的观测转换数文本。</summary>
    public string HistoryCountText { get => mHistoryCountText; private set => SetProperty(ref mHistoryCountText, value); }

    /// <summary>获取观测图没有节点时的上下文提示。</summary>
    public string GraphEmptyHint { get => mGraphEmptyHint; private set => SetProperty(ref mGraphEmptyHint, value); }

    /// <summary>
    /// 在应用一帧 FsmKit 状态后更新工作区摘要和图模型。
    /// </summary>
    /// <param name="details">当前选中实例的完整详情。</param>
    /// <param name="summary">当前选中实例的摘要。</param>
    private void UpdateWorkspacePresentation(
        WorkbenchFsmMachineDetails? details,
        FsmMachineListItemViewModel? summary)
    {
        HasMachines = mAllMachines.Count > 0;
        IsEmptyWorkspaceVisible = !HasMachines;
        InstanceCountText = mAllMachines.Count + " 个实例";
        StateCountText = (details?.StateCount ?? summary?.StateCount ?? 0) + " 个状态";
        HistoryCountText = Transitions.Count + " 条转换";
        if (!HasMachines)
        {
            EmptyStateTitle = "未发现活动状态机";
            EmptyStateDescription = "已读取 FsmKit 快照，但当前宿主没有注册 FSM。启动或注册状态机后，工作台会自动刷新。";
            GraphModel = ObservedFsmGraphModel.Empty;
            IsGraphEmpty = true;
            GraphEmptyHint = "注册一个 FSM 后，工作台会在这里显示运行时状态图和转换历史。";
            return;
        }

        EmptyStateTitle = string.Empty;
        EmptyStateDescription = string.Empty;
        BuildObservedGraph(details?.States ?? Array.Empty<WorkbenchFsmStateNode>());
    }

    /// <summary>
    /// 重置工作区摘要与图模型，避免宿主切换后继续显示上一帧节点。
    /// </summary>
    private void ResetWorkspacePresentation()
    {
        HasMachines = false;
        IsEmptyWorkspaceVisible = true;
        EmptyStateTitle = "等待 FsmKit 状态";
        EmptyStateDescription = "正在读取宿主发布的 FsmKit 快照。";
        InstanceCountText = "0 个实例";
        StateCountText = "0 个状态";
        HistoryCountText = "0 条转换";
        GraphModel = ObservedFsmGraphModel.Empty;
        IsGraphEmpty = true;
        GraphEmptyHint = "选择一个实例以读取完整状态树和已观测转换。";
    }

    /// <summary>
    /// 从状态树、当前状态和运行历史建立确定性的环形图布局。
    /// </summary>
    /// <param name="stateTree">当前实例的递归状态树。</param>
    private void BuildObservedGraph(IReadOnlyList<WorkbenchFsmStateNode> stateTree)
    {
        List<GraphNodeSeed> seeds = new();
        Dictionary<string, GraphNodeSeed> seedsByKey = new(StringComparer.Ordinal);
        Dictionary<string, List<GraphNodeSeed>> rootSeedsByName = new(StringComparer.Ordinal);
        Dictionary<string, GraphNodeSeed> observedRootSeeds = new(StringComparer.Ordinal);
        AddStateTreeSeeds(
            stateTree, ROOT_MACHINE_KEY, true, seeds, seedsByKey, rootSeedsByName);
        var currentSeed = ResolveRootStateSeed(
            CurrentState, seeds, seedsByKey, rootSeedsByName, observedRootSeeds);
        if (currentSeed != null)
        {
            currentSeed.IsCurrent = true;
        }

        var edgeSeeds = CreateGraphEdgeSeeds(
            seeds, seedsByKey, rootSeedsByName, observedRootSeeds);

        if (seeds.Count == 0)
        {
            GraphModel = ObservedFsmGraphModel.Empty;
            IsGraphEmpty = true;
            GraphEmptyHint = "当前实例尚未返回完整状态树；选择列表项会触发只读详情查询。";
            return;
        }

        var canvasSize = CalculateGraphCanvasSize(seeds.Count);
        Dictionary<string, ObservedFsmGraphNode> nodesByKey = new(StringComparer.Ordinal);
        var nodes = CreateGraphNodes(
            seeds, nodesByKey, canvasSize.Width, canvasSize.Height);
        GraphModel = new ObservedFsmGraphModel(
            nodes,
            CreateGraphEdges(nodesByKey, edgeSeeds),
            canvasSize.Width,
            canvasSize.Height);
        IsGraphEmpty = false;
        GraphEmptyHint = string.Empty;
    }

    /// <summary>
    /// 递归加入 Runtime 提供的状态树节点，保留首次出现顺序。
    /// </summary>
    /// <param name="states">当前机器的状态节点。</param>
    /// <param name="machineKey">所属机器在递归树中的稳定路径。</param>
    /// <param name="isRootMachine">当前层是否为选中实例的根机器。</param>
    /// <param name="seeds">待布局的节点种子。</param>
    /// <param name="seedsByKey">按内部唯一键索引的节点种子。</param>
    /// <param name="rootSeedsByName">仅按根机器状态名建立的历史解析索引。</param>
    private static void AddStateTreeSeeds(
        IReadOnlyList<WorkbenchFsmStateNode> states,
        string machineKey,
        bool isRootMachine,
        ICollection<GraphNodeSeed> seeds,
        IDictionary<string, GraphNodeSeed> seedsByKey,
        IDictionary<string, List<GraphNodeSeed>> rootSeedsByName)
    {
        for (var index = 0; index < states.Count; index++)
        {
            var state = states[index];
            var key = CreateStateNodeKey(machineKey, state, index);
            GraphNodeSeed seed = new(
                key,
                state.Name,
                state.IsCurrent,
                state.IsComposite,
                state.EntryCount,
                state.OrderIndex,
                state.Id,
                seeds.Count);
            seeds.Add(seed);
            seedsByKey.Add(key, seed);
            if (isRootMachine)
            {
                IndexRootStateSeed(rootSeedsByName, seed);
            }

            AddStateTreeSeeds(
                state.Children, key + "/machine", false, seeds, seedsByKey, rootSeedsByName);
        }
    }

    /// <summary>
    /// 将只有名称的根机器状态解析为唯一节点；根层重名或缺失时使用独立观测节点，绝不猜测子机器。
    /// </summary>
    private static GraphNodeSeed? ResolveRootStateSeed(
        string name,
        ICollection<GraphNodeSeed> seeds,
        IDictionary<string, GraphNodeSeed> seedsByKey,
        IReadOnlyDictionary<string, List<GraphNodeSeed>> rootSeedsByName,
        IDictionary<string, GraphNodeSeed> observedRootSeeds)
    {
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "未选择", StringComparison.Ordinal))
        {
            return null;
        }

        if (rootSeedsByName.TryGetValue(name, out var matches) && matches.Count == 1)
        {
            return matches[0];
        }

        if (observedRootSeeds.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var key = ROOT_MACHINE_KEY + "/observed:" + name.Length + ":" + name;
        GraphNodeSeed seed = new(key, name, false, false, 0L, int.MaxValue, int.MaxValue, seeds.Count);
        seeds.Add(seed);
        seedsByKey.Add(key, seed);
        observedRootSeeds.Add(name, seed);
        return seed;
    }

    /// <summary>
    /// 按环形布局创建节点，保证状态数量变化时画面仍稳定且不会相互覆盖。
    /// </summary>
    /// <param name="seeds">待布局节点种子。</param>
    /// <param name="canvasWidth">本代图模型的画布宽度。</param>
    /// <param name="canvasHeight">本代图模型的画布高度。</param>
    /// <returns>可直接渲染的节点列表。</returns>
    private IReadOnlyList<ObservedFsmGraphNode> CreateGraphNodes(
        IReadOnlyList<GraphNodeSeed> seeds,
        IDictionary<string, ObservedFsmGraphNode> nodesByKey,
        double canvasWidth,
        double canvasHeight)
    {
        var orderedSeeds = seeds
            .OrderBy(seed => seed.OrderIndex)
            .ThenBy(seed => seed.EnumId)
            .ThenBy(seed => seed.Ordinal)
            .ToArray();
        var radius = CalculateGraphRadius(orderedSeeds.Length);
        List<ObservedFsmGraphNode> nodes = new(orderedSeeds.Length);
        var centerX = canvasWidth / 2.0;
        var centerY = canvasHeight / 2.0;
        for (var index = 0; index < orderedSeeds.Length; index++)
        {
            var angle = orderedSeeds.Length <= 1
                ? 0.0
                : -Math.PI / 2.0 + Math.PI * 2.0 * index / orderedSeeds.Length;
            var nodeCenterX = orderedSeeds.Length <= 1
                ? centerX
                : centerX + Math.Cos(angle) * radius;
            var nodeCenterY = orderedSeeds.Length <= 1
                ? centerY
                : centerY + Math.Sin(angle) * radius;
            var left = nodeCenterX - ObservedFsmGraphMetrics.NODE_WIDTH / 2.0;
            var top = nodeCenterY - ObservedFsmGraphMetrics.NODE_HEIGHT / 2.0;
            var seed = orderedSeeds[index];
            ObservedFsmGraphNode node = new(
                seed.Name,
                seed.IsCurrent,
                seed.IsComposite,
                seed.EntryCount,
                seed.OrderIndex,
                seed.EnumId,
                left,
                top,
                nodeCenterX,
                nodeCenterY,
                angle);
            nodes.Add(node);
            nodesByKey.Add(seed.Key, node);
        }

        return nodes;
    }

    /// <summary>按状态数量计算环形布局所需画布尺寸，并保证小图仍填满基础视口。</summary>
    /// <param name="nodeCount">待布局节点数量。</param>
    /// <returns>本代图模型的确定性宽高。</returns>
    private static (double Width, double Height) CalculateGraphCanvasSize(int nodeCount)
    {
        var radius = CalculateGraphRadius(nodeCount);
        var width = Math.Max(
            ObservedFsmGraphMetrics.DEFAULT_CANVAS_WIDTH,
            Math.Ceiling((radius + ObservedFsmGraphMetrics.NODE_WIDTH / 2.0 + GRAPH_PADDING_X) * 2.0));
        var height = Math.Max(
            ObservedFsmGraphMetrics.DEFAULT_CANVAS_HEIGHT,
            Math.Ceiling((radius + ObservedFsmGraphMetrics.NODE_HEIGHT / 2.0 + GRAPH_PADDING_Y) * 2.0));
        return (width, height);
    }

    /// <summary>计算环形节点中心半径，使布局和画布尺寸始终使用同一规则。</summary>
    /// <param name="nodeCount">待布局节点数量。</param>
    /// <returns>零或单节点返回 0，多节点返回不会重叠的最小半径。</returns>
    private static double CalculateGraphRadius(int nodeCount)
    {
        if (nodeCount <= 1)
        {
            return 0.0;
        }

        var circumferenceRadius = (ObservedFsmGraphMetrics.NODE_WIDTH + GRAPH_NODE_ARC_GAP)
            * nodeCount / (Math.PI * 2.0);
        return Math.Max(GRAPH_MIN_RADIUS, circumferenceRadius);
    }

    /// <summary>
    /// 聚合同向运行历史并使用节点中心点计算连线与计数标签位置。
    /// </summary>
    /// <param name="nodesByKey">按内部唯一键索引的已布局节点。</param>
    /// <param name="seeds">按首次观测顺序聚合的边种子。</param>
    /// <returns>可直接渲染的观测转换边。</returns>
    private static IReadOnlyList<ObservedFsmGraphEdge> CreateGraphEdges(
        IReadOnlyDictionary<string, ObservedFsmGraphNode> nodesByKey,
        IReadOnlyList<GraphEdgeSeed> seeds)
    {
        HashSet<(string From, string To)> edgeKeys = new(
            seeds.Select(static seed => (seed.FromKey, seed.ToKey)));
        List<ObservedFsmGraphEdge> edges = new(seeds.Count);
        foreach (var seed in seeds)
        {
            var from = nodesByKey[seed.FromKey];
            var to = nodesByKey[seed.ToKey];
            var isSelfLoop = string.Equals(seed.FromKey, seed.ToKey, StringComparison.Ordinal);
            var hasReverse = !isSelfLoop && edgeKeys.Contains((seed.ToKey, seed.FromKey));
            var geometry = CreateEdgeGeometry(
                from, to, seed.FromKey, seed.ToKey, isSelfLoop, hasReverse);
            edges.Add(new ObservedFsmGraphEdge(
                seed.From,
                seed.To,
                seed.Count,
                geometry.StartX,
                geometry.StartY,
                geometry.EndX,
                geometry.EndY,
                geometry.LabelX,
                geometry.LabelY,
                geometry.IsCurved,
                geometry.IsSelfLoop,
                geometry.ControlX,
                geometry.ControlY,
                geometry.Control2X,
                geometry.Control2Y,
                geometry.ArrowAngle,
                seed.IsLatest));
        }

        return edges;
    }

    /// <summary>按首次观测顺序聚合同向转换，并将名称端点限定解析到选中实例的根机器。</summary>
    /// <returns>按首次出现顺序排列的聚合边种子。</returns>
    private List<GraphEdgeSeed> CreateGraphEdgeSeeds(
        ICollection<GraphNodeSeed> nodes,
        IDictionary<string, GraphNodeSeed> nodesByKey,
        IReadOnlyDictionary<string, List<GraphNodeSeed>> rootNodesByName,
        IDictionary<string, GraphNodeSeed> observedRootNodes)
    {
        List<GraphEdgeSeed> seeds = new();
        Dictionary<(string From, string To), GraphEdgeSeed> seedsByKey = new();
        for (var index = 0; index < Transitions.Count; index++)
        {
            var transition = Transitions[index];
            var from = ResolveRootStateSeed(
                transition.From, nodes, nodesByKey, rootNodesByName, observedRootNodes);
            var to = ResolveRootStateSeed(
                transition.To, nodes, nodesByKey, rootNodesByName, observedRootNodes);
            if (from == null || to == null)
            {
                continue;
            }

            var key = (from.Key, to.Key);
            if (!seedsByKey.TryGetValue(key, out var existing))
            {
                GraphEdgeSeed seed = new(from.Key, to.Key, transition.From, transition.To);
                seeds.Add(seed);
                seedsByKey.Add(key, seed);
                existing = seed;
            }
            else
            {
                existing.Count++;
            }

            existing.IsLatest = index == Transitions.Count - 1;
        }

        return seeds;
    }

    /// <summary>
    /// 计算节点边界到边界的直线、双向曲线或自环几何，避免连线穿过状态节点。
    /// </summary>
    private static GraphEdgeGeometry CreateEdgeGeometry(
        ObservedFsmGraphNode from,
        ObservedFsmGraphNode to,
        string fromKey,
        string toKey,
        bool isSelfLoop,
        bool hasReverse)
    {
        return isSelfLoop
            ? CreateSelfLoopGeometry(from)
            : CreateDirectedEdgeGeometry(from, to, fromKey, toKey, hasReverse);
    }

    /// <summary>沿节点在圆环外侧生成自环曲线。</summary>
    private static GraphEdgeGeometry CreateSelfLoopGeometry(ObservedFsmGraphNode node)
    {
        var radialX = Math.Cos(node.Angle);
        var radialY = Math.Sin(node.Angle);
        var tangentX = -radialY;
        var tangentY = radialX;
        var start = GetNodeBoundaryPoint(node, radialX * 0.5 + tangentX, radialY * 0.5 + tangentY, 3.0);
        var end = GetNodeBoundaryPoint(node, radialX * 0.5 - tangentX, radialY * 0.5 - tangentY, 3.0);
        var control = new PointValue(
            start.X + radialX * 44.0 + tangentX * 16.0,
            start.Y + radialY * 44.0 + tangentY * 16.0);
        var control2 = new PointValue(
            end.X + radialX * 44.0 - tangentX * 16.0,
            end.Y + radialY * 44.0 - tangentY * 16.0);
        var label = new PointValue(node.CenterX + radialX * 54.0, node.CenterY + radialY * 54.0);
        return new GraphEdgeGeometry(
            start.X, start.Y, end.X, end.Y, label.X, label.Y, true, true,
            control.X, control.Y, control2.X, control2.Y,
            Math.Atan2(end.Y - control2.Y, end.X - control2.X));
    }

    /// <summary>生成普通有向边；存在反向边时使用稳定的法线偏移。</summary>
    private static GraphEdgeGeometry CreateDirectedEdgeGeometry(
        ObservedFsmGraphNode from,
        ObservedFsmGraphNode to,
        string fromKey,
        string toKey,
        bool hasReverse)
    {
        var directionX = to.CenterX - from.CenterX;
        var directionY = to.CenterY - from.CenterY;
        var startPoint = GetNodeBoundaryPoint(from, directionX, directionY, 3.0);
        var endPoint = GetNodeBoundaryPoint(to, -directionX, -directionY, 3.0);
        var midpointX = (startPoint.X + endPoint.X) / 2.0;
        var midpointY = (startPoint.Y + endPoint.Y) / 2.0;
        var controlX = midpointX;
        var controlY = midpointY;
        if (hasReverse)
        {
            var length = Math.Max(1.0, Math.Sqrt(directionX * directionX + directionY * directionY));
            var sign = string.CompareOrdinal(fromKey, toKey) <= 0 ? 1.0 : -1.0;
            var curveOffset = Math.Clamp(length * 0.2, 32.0, 72.0);
            controlX += -directionY / length * curveOffset * sign;
            controlY += directionX / length * curveOffset * sign;
        }

        return new GraphEdgeGeometry(
            startPoint.X, startPoint.Y, endPoint.X, endPoint.Y,
            hasReverse ? controlX : midpointX, hasReverse ? controlY : midpointY,
            hasReverse, false, controlX, controlY, controlX, controlY,
            Math.Atan2(endPoint.Y - controlY, endPoint.X - controlX));
    }

    /// <summary>
    /// 取从节点中心指向目标方向的矩形边界交点。
    /// </summary>
    private static PointValue GetNodeBoundaryPoint(
        ObservedFsmGraphNode node,
        double directionX,
        double directionY,
        double padding)
    {
        var absoluteX = Math.Abs(directionX);
        var absoluteY = Math.Abs(directionY);
        if (absoluteX < 0.001 && absoluteY < 0.001)
        {
            return new PointValue(
                node.CenterX,
                node.CenterY - ObservedFsmGraphMetrics.NODE_HEIGHT / 2.0 - padding);
        }

        var scaleX = absoluteX > 0.001
            ? (ObservedFsmGraphMetrics.NODE_WIDTH / 2.0 + padding) / absoluteX
            : double.PositiveInfinity;
        var scaleY = absoluteY > 0.001
            ? (ObservedFsmGraphMetrics.NODE_HEIGHT / 2.0 + padding) / absoluteY
            : double.PositiveInfinity;
        var scale = Math.Min(scaleX, scaleY);
        return new PointValue(
            node.CenterX + directionX * scale,
            node.CenterY + directionY * scale);
    }

}
