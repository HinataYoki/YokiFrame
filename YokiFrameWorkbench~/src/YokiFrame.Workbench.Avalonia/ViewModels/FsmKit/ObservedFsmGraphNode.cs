namespace YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

/// <summary>
/// 描述观测状态图中的一个只读节点；坐标由页面 ViewModel 预先计算，View 只负责呈现。
/// </summary>
public sealed class ObservedFsmGraphNode : IEquatable<ObservedFsmGraphNode>
{
    /// <summary>
    /// 创建状态图节点。
    /// </summary>
    /// <param name="name">状态显示名称。</param>
    /// <param name="isCurrent">节点是否为当前状态。</param>
    /// <param name="isComposite">节点是否代表复合状态机。</param>
    /// <param name="entryCount">Runtime 自本次记录清理后累计进入该状态的次数。</param>
    /// <param name="orderIndex">Runtime 声明的稳定排序索引。</param>
    /// <param name="enumId">Runtime 状态整数标识。</param>
    /// <param name="left">节点在图画布中的左侧坐标。</param>
    /// <param name="top">节点在图画布中的顶部坐标。</param>
    /// <param name="centerX">节点中心横坐标。</param>
    /// <param name="centerY">节点中心纵坐标。</param>
    /// <param name="angle">节点在圆环上的弧度角。</param>
    public ObservedFsmGraphNode(
        string name,
        bool isCurrent,
        bool isComposite,
        long entryCount,
        int orderIndex,
        int enumId,
        double left,
        double top,
        double centerX,
        double centerY,
        double angle)
    {
        Name = name;
        IsCurrent = isCurrent;
        IsComposite = isComposite;
        EntryCount = entryCount;
        OrderIndex = orderIndex;
        EnumId = enumId;
        Left = left;
        Top = top;
        CenterX = centerX;
        CenterY = centerY;
        Angle = angle;
    }

    /// <summary>获取状态显示名称。</summary>
    public string Name { get; }

    /// <summary>获取该节点是否为当前状态。</summary>
    public bool IsCurrent { get; }

    /// <summary>获取该节点是否为复合状态。</summary>
    public bool IsComposite { get; }

    /// <summary>获取 Runtime 自本次记录清理后累计进入该状态的次数。</summary>
    public long EntryCount { get; }

    /// <summary>获取 Runtime 声明的稳定排序索引。</summary>
    public int OrderIndex { get; }

    /// <summary>获取 Runtime 状态整数标识。</summary>
    public int EnumId { get; }

    /// <summary>获取节点在图画布中的左侧坐标。</summary>
    public double Left { get; }

    /// <summary>获取节点在图画布中的顶部坐标。</summary>
    public double Top { get; }

    /// <summary>获取节点中心横坐标。</summary>
    public double CenterX { get; }

    /// <summary>获取节点中心纵坐标。</summary>
    public double CenterY { get; }

    /// <summary>获取节点在圆环上的弧度角。</summary>
    public double Angle { get; }

    /// <summary>
    /// 比较节点的业务状态和全部布局坐标，确保等价快照不会触发无意义重绘。
    /// </summary>
    /// <param name="other">待比较的节点。</param>
    /// <returns>节点的内容和几何均相同时返回 <see langword="true"/>。</returns>
    public bool Equals(ObservedFsmGraphNode? other)
    {
        return other != null
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && IsCurrent == other.IsCurrent
            && IsComposite == other.IsComposite
            && EntryCount == other.EntryCount
            && OrderIndex == other.OrderIndex
            && EnumId == other.EnumId
            && Left.Equals(other.Left)
            && Top.Equals(other.Top)
            && CenterX.Equals(other.CenterX)
            && CenterY.Equals(other.CenterY)
            && Angle.Equals(other.Angle);
    }

    /// <summary>
    /// 将对象比较转发到强类型节点比较，保持集合语义判断与单节点判断一致。
    /// </summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>对象是内容相同的节点时返回 <see langword="true"/>。</returns>
    public override bool Equals(object? obj) => obj is ObservedFsmGraphNode other && Equals(other);

    /// <summary>
    /// 计算覆盖节点业务状态和布局坐标的组合哈希值。
    /// </summary>
    /// <returns>当前节点的组合哈希值。</returns>
    public override int GetHashCode()
    {
        HashCode hashCode = new();
        hashCode.Add(Name, StringComparer.Ordinal);
        hashCode.Add(IsCurrent);
        hashCode.Add(IsComposite);
        hashCode.Add(EntryCount);
        hashCode.Add(OrderIndex);
        hashCode.Add(EnumId);
        hashCode.Add(Left);
        hashCode.Add(Top);
        hashCode.Add(CenterX);
        hashCode.Add(CenterY);
        hashCode.Add(Angle);
        return hashCode.ToHashCode();
    }
}
