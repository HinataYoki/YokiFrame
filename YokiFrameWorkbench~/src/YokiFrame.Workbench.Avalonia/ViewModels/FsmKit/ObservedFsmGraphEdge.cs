namespace YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

/// <summary>
/// 描述观测状态图中的一条聚合转换边；边仅来自运行历史，不表示状态机静态规则。
/// </summary>
public sealed class ObservedFsmGraphEdge : IEquatable<ObservedFsmGraphEdge>
{
    /// <summary>
    /// 创建聚合转换边。
    /// </summary>
    /// <param name="from">起始状态名称。</param>
    /// <param name="to">目标状态名称。</param>
    /// <param name="count">同向转换的观测次数。</param>
    /// <param name="startX">线段起点横坐标。</param>
    /// <param name="startY">线段起点纵坐标。</param>
    /// <param name="endX">线段终点横坐标。</param>
    /// <param name="endY">线段终点纵坐标。</param>
    /// <param name="labelLeft">计数标签左侧坐标。</param>
    /// <param name="labelTop">计数标签顶部坐标。</param>
    /// <param name="isCurved">是否使用二次曲线。</param>
    /// <param name="isSelfLoop">是否为自环。</param>
    /// <param name="controlPointX">曲线第一个控制点横坐标。</param>
    /// <param name="controlPointY">曲线第一个控制点纵坐标。</param>
    /// <param name="controlPoint2X">曲线第二个控制点横坐标。</param>
    /// <param name="controlPoint2Y">曲线第二个控制点纵坐标。</param>
    /// <param name="arrowAngle">终点箭头方向。</param>
    /// <param name="isLatest">是否为最近一次观测转换。</param>
    public ObservedFsmGraphEdge(
        string from,
        string to,
        int count,
        double startX,
        double startY,
        double endX,
        double endY,
        double labelLeft,
        double labelTop,
        bool isCurved,
        bool isSelfLoop,
        double controlPointX,
        double controlPointY,
        double controlPoint2X,
        double controlPoint2Y,
        double arrowAngle,
        bool isLatest)
    {
        From = from;
        To = to;
        Count = count;
        StartX = startX;
        StartY = startY;
        EndX = endX;
        EndY = endY;
        LabelLeft = labelLeft;
        LabelTop = labelTop;
        IsCurved = isCurved;
        IsSelfLoop = isSelfLoop;
        ControlPointX = controlPointX;
        ControlPointY = controlPointY;
        ControlPoint2X = controlPoint2X;
        ControlPoint2Y = controlPoint2Y;
        ArrowAngle = arrowAngle;
        IsLatest = isLatest;
    }

    /// <summary>获取起始状态名称。</summary>
    public string From { get; }

    /// <summary>获取目标状态名称。</summary>
    public string To { get; }

    /// <summary>获取同向转换的观测次数。</summary>
    public int Count { get; }

    /// <summary>获取线段起点横坐标。</summary>
    public double StartX { get; }

    /// <summary>获取线段起点纵坐标。</summary>
    public double StartY { get; }

    /// <summary>获取线段终点横坐标。</summary>
    public double EndX { get; }

    /// <summary>获取线段终点纵坐标。</summary>
    public double EndY { get; }

    /// <summary>获取计数标签左侧坐标。</summary>
    public double LabelLeft { get; }

    /// <summary>获取计数标签顶部坐标。</summary>
    public double LabelTop { get; }

    /// <summary>获取该边是否使用曲线绘制。</summary>
    public bool IsCurved { get; }

    /// <summary>获取该边是否为自环。</summary>
    public bool IsSelfLoop { get; }

    /// <summary>获取曲线第一个控制点横坐标。</summary>
    public double ControlPointX { get; }

    /// <summary>获取曲线第一个控制点纵坐标。</summary>
    public double ControlPointY { get; }

    /// <summary>获取曲线第二个控制点横坐标。</summary>
    public double ControlPoint2X { get; }

    /// <summary>获取曲线第二个控制点纵坐标。</summary>
    public double ControlPoint2Y { get; }

    /// <summary>获取终点箭头方向。</summary>
    public double ArrowAngle { get; }

    /// <summary>获取该边是否为最近一次观测转换。</summary>
    public bool IsLatest { get; }

    /// <summary>
    /// 比较转换边的聚合信息、全部曲线几何和高亮状态。
    /// </summary>
    /// <param name="other">待比较的转换边。</param>
    /// <returns>边的内容和绘制几何均相同时返回 <see langword="true"/>。</returns>
    public bool Equals(ObservedFsmGraphEdge? other)
    {
        return other != null
            && string.Equals(From, other.From, StringComparison.Ordinal)
            && string.Equals(To, other.To, StringComparison.Ordinal)
            && Count == other.Count
            && StartX.Equals(other.StartX)
            && StartY.Equals(other.StartY)
            && EndX.Equals(other.EndX)
            && EndY.Equals(other.EndY)
            && LabelLeft.Equals(other.LabelLeft)
            && LabelTop.Equals(other.LabelTop)
            && IsCurved == other.IsCurved
            && IsSelfLoop == other.IsSelfLoop
            && ControlPointX.Equals(other.ControlPointX)
            && ControlPointY.Equals(other.ControlPointY)
            && ControlPoint2X.Equals(other.ControlPoint2X)
            && ControlPoint2Y.Equals(other.ControlPoint2Y)
            && ArrowAngle.Equals(other.ArrowAngle)
            && IsLatest == other.IsLatest;
    }

    /// <summary>
    /// 将对象比较转发到强类型边比较，保持模型集合比较规则一致。
    /// </summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>对象是内容相同的转换边时返回 <see langword="true"/>。</returns>
    public override bool Equals(object? obj) => obj is ObservedFsmGraphEdge other && Equals(other);

    /// <summary>
    /// 计算覆盖聚合信息、曲线几何和高亮状态的组合哈希值。
    /// </summary>
    /// <returns>当前转换边的组合哈希值。</returns>
    public override int GetHashCode()
    {
        HashCode hashCode = new();
        hashCode.Add(From, StringComparer.Ordinal);
        hashCode.Add(To, StringComparer.Ordinal);
        hashCode.Add(Count);
        hashCode.Add(StartX);
        hashCode.Add(StartY);
        hashCode.Add(EndX);
        hashCode.Add(EndY);
        hashCode.Add(LabelLeft);
        hashCode.Add(LabelTop);
        hashCode.Add(IsCurved);
        hashCode.Add(IsSelfLoop);
        hashCode.Add(ControlPointX);
        hashCode.Add(ControlPointY);
        hashCode.Add(ControlPoint2X);
        hashCode.Add(ControlPoint2Y);
        hashCode.Add(ArrowAngle);
        hashCode.Add(IsLatest);
        return hashCode.ToHashCode();
    }
}
