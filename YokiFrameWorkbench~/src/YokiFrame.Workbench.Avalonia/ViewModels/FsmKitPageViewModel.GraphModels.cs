using YokiFrame.Tooling.Application.Models.FsmKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 提供 FsmKit 图布局使用的内部种子、坐标和边几何模型。
/// </summary>
public sealed partial class FsmKitPageViewModel
{
    /// <summary>保存待布局状态的业务属性。</summary>
    private sealed class GraphNodeSeed
    {
        /// <summary>创建状态种子。</summary>
        public GraphNodeSeed(
            string key,
            string name,
            bool isCurrent,
            bool isComposite,
            long entryCount,
            int orderIndex,
            int enumId,
            int ordinal)
        {
            Key = key;
            Name = name;
            IsCurrent = isCurrent;
            IsComposite = isComposite;
            EntryCount = entryCount;
            OrderIndex = orderIndex;
            EnumId = enumId;
            Ordinal = ordinal;
        }

        /// <summary>获取由所属机器路径和状态标识组成的内部唯一键。</summary>
        public string Key { get; }
        /// <summary>获取状态名称。</summary>
        public string Name { get; }
        /// <summary>获取或设置当前状态标记。</summary>
        public bool IsCurrent { get; set; }
        /// <summary>获取或设置复合状态标记。</summary>
        public bool IsComposite { get; set; }
        /// <summary>获取 Runtime 累计的状态进入次数。</summary>
        public long EntryCount { get; }
        /// <summary>获取或设置稳定排序索引。</summary>
        public int OrderIndex { get; set; }
        /// <summary>获取或设置状态整数标识。</summary>
        public int EnumId { get; set; }
        /// <summary>获取首次出现顺序。</summary>
        public int Ordinal { get; }
    }

    /// <summary>由机器递归路径、状态标识和同层顺序建立稳定且不会受显示名影响的节点键。</summary>
    private static string CreateStateNodeKey(
        string machineKey,
        WorkbenchFsmStateNode state,
        int siblingIndex)
    {
        return FormattableString.Invariant(
            $"{machineKey}/state:{state.Id}:{state.OrderIndex}:{siblingIndex}");
    }

    /// <summary>将根机器状态按显示名分组，使名称历史只在根层唯一时直接绑定。</summary>
    private static void IndexRootStateSeed(
        IDictionary<string, List<GraphNodeSeed>> seedsByName,
        GraphNodeSeed seed)
    {
        if (!seedsByName.TryGetValue(seed.Name, out var matches))
        {
            matches = new List<GraphNodeSeed>();
            seedsByName.Add(seed.Name, matches);
        }

        matches.Add(seed);
    }

    /// <summary>保存一个二维坐标，避免布局计算依赖 UI 类型。</summary>
    private readonly struct PointValue
    {
        /// <summary>创建坐标值。</summary>
        public PointValue(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>获取横坐标。</summary>
        public double X { get; }
        /// <summary>获取纵坐标。</summary>
        public double Y { get; }
    }

    /// <summary>保存一条转换边的绘制几何。</summary>
    private readonly struct GraphEdgeGeometry
    {
        /// <summary>创建边几何。</summary>
        public GraphEdgeGeometry(double startX, double startY, double endX, double endY, double labelX, double labelY,
            bool isCurved, bool isSelfLoop, double controlX, double controlY, double control2X, double control2Y,
            double arrowAngle)
        {
            StartX = startX;
            StartY = startY;
            EndX = endX;
            EndY = endY;
            LabelX = labelX;
            LabelY = labelY;
            IsCurved = isCurved;
            IsSelfLoop = isSelfLoop;
            ControlX = controlX;
            ControlY = controlY;
            Control2X = control2X;
            Control2Y = control2Y;
            ArrowAngle = arrowAngle;
        }

        /// <summary>起点横坐标。</summary>
        public double StartX { get; }
        /// <summary>起点纵坐标。</summary>
        public double StartY { get; }
        /// <summary>终点横坐标。</summary>
        public double EndX { get; }
        /// <summary>终点纵坐标。</summary>
        public double EndY { get; }
        /// <summary>标签横坐标。</summary>
        public double LabelX { get; }
        /// <summary>标签纵坐标。</summary>
        public double LabelY { get; }
        /// <summary>是否为曲线。</summary>
        public bool IsCurved { get; }
        /// <summary>是否为自环。</summary>
        public bool IsSelfLoop { get; }
        /// <summary>第一个控制点横坐标。</summary>
        public double ControlX { get; }
        /// <summary>第一个控制点纵坐标。</summary>
        public double ControlY { get; }
        /// <summary>第二个控制点横坐标。</summary>
        public double Control2X { get; }
        /// <summary>第二个控制点纵坐标。</summary>
        public double Control2Y { get; }
        /// <summary>箭头方向。</summary>
        public double ArrowAngle { get; }
    }

    /// <summary>保存聚合前的同向边计数。</summary>
    private sealed class GraphEdgeSeed
    {
        /// <summary>创建一条观测转换边种子。</summary>
        public GraphEdgeSeed(string fromKey, string toKey, string from, string to)
        {
            FromKey = fromKey;
            ToKey = toKey;
            From = from;
            To = to;
            Count = 1;
        }

        /// <summary>获取起始节点内部唯一键。</summary>
        public string FromKey { get; }
        /// <summary>获取目标节点内部唯一键。</summary>
        public string ToKey { get; }
        /// <summary>获取起始状态名称。</summary>
        public string From { get; }
        /// <summary>获取目标状态名称。</summary>
        public string To { get; }
        /// <summary>获取或设置同向转换计数。</summary>
        public int Count { get; set; }
        /// <summary>获取或设置该聚合边是否包含最新转换。</summary>
        public bool IsLatest { get; set; }
    }
}
