namespace YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

/// <summary>
/// 描述一次可原子提交的 FsmKit 观测图快照，节点、边与画布尺寸始终属于同一代数据。
/// </summary>
public sealed class ObservedFsmGraphModel : IEquatable<ObservedFsmGraphModel>
{
    /// <summary>获取使用默认画布尺寸且不含节点和边的共享空模型。</summary>
    public static ObservedFsmGraphModel Empty { get; } = new(
        Array.Empty<ObservedFsmGraphNode>(),
        Array.Empty<ObservedFsmGraphEdge>(),
        ObservedFsmGraphMetrics.DEFAULT_CANVAS_WIDTH,
        ObservedFsmGraphMetrics.DEFAULT_CANVAS_HEIGHT);

    /// <summary>
    /// 创建不可变观测图快照；构造过程会复制集合，调用方后续修改原集合不会影响该模型。
    /// </summary>
    /// <param name="nodes">已完成布局的节点集合，顺序同时决定节点绘制顺序。</param>
    /// <param name="edges">已完成布局的边集合，顺序同时决定边绘制顺序。</param>
    /// <param name="canvasWidth">未缩放画布宽度，必须是有限正数。</param>
    /// <param name="canvasHeight">未缩放画布高度，必须是有限正数。</param>
    public ObservedFsmGraphModel(
        IReadOnlyList<ObservedFsmGraphNode> nodes,
        IReadOnlyList<ObservedFsmGraphEdge> edges,
        double canvasWidth = ObservedFsmGraphMetrics.DEFAULT_CANVAS_WIDTH,
        double canvasHeight = ObservedFsmGraphMetrics.DEFAULT_CANVAS_HEIGHT)
    {
        Nodes = CopyItems(nodes, nameof(nodes));
        Edges = CopyItems(edges, nameof(edges));
        CanvasWidth = ValidateCanvasDimension(canvasWidth, nameof(canvasWidth));
        CanvasHeight = ValidateCanvasDimension(canvasHeight, nameof(canvasHeight));
    }

    /// <summary>获取该代图快照包含的只读节点。</summary>
    public IReadOnlyList<ObservedFsmGraphNode> Nodes { get; }

    /// <summary>获取该代图快照包含的只读转换边。</summary>
    public IReadOnlyList<ObservedFsmGraphEdge> Edges { get; }

    /// <summary>获取该代图快照的未缩放画布宽度。</summary>
    public double CanvasWidth { get; }

    /// <summary>获取该代图快照的未缩放画布高度。</summary>
    public double CanvasHeight { get; }

    /// <summary>
    /// 判断另一个模型是否包含完全相同的节点、边、绘制顺序与画布尺寸。
    /// </summary>
    /// <param name="other">待比较的图模型。</param>
    /// <returns>两份模型在视觉语义上相同时返回 <see langword="true"/>。</returns>
    public bool SemanticallyEquals(ObservedFsmGraphModel? other) => Equals(other);

    /// <summary>
    /// 按视觉语义比较另一份模型，使 Avalonia 属性系统能够跳过等价快照通知。
    /// </summary>
    /// <param name="other">待比较的图模型。</param>
    /// <returns>节点、边和尺寸均相同时返回 <see langword="true"/>。</returns>
    public bool Equals(ObservedFsmGraphModel? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other != null
            && CanvasWidth.Equals(other.CanvasWidth)
            && CanvasHeight.Equals(other.CanvasHeight)
            && AreItemsEqual(Nodes, other.Nodes)
            && AreItemsEqual(Edges, other.Edges);
    }

    /// <summary>
    /// 将对象比较转发到强类型视觉语义比较，避免引用不同但内容相同的快照触发重绘。
    /// </summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>对象是语义相同的图模型时返回 <see langword="true"/>。</returns>
    public override bool Equals(object? obj) => obj is ObservedFsmGraphModel other && Equals(other);

    /// <summary>
    /// 计算覆盖画布尺寸、节点和边的稳定进程内哈希值，并保持与语义相等规则一致。
    /// </summary>
    /// <returns>当前图模型的组合哈希值。</returns>
    public override int GetHashCode()
    {
        HashCode hashCode = new();
        hashCode.Add(CanvasWidth);
        hashCode.Add(CanvasHeight);
        AddItemsToHash(ref hashCode, Nodes);
        AddItemsToHash(ref hashCode, Edges);
        return hashCode.ToHashCode();
    }

    /// <summary>
    /// 复制引用类型集合并拒绝空元素，保证模型创建后不会被调用方从外部改写。
    /// </summary>
    /// <typeparam name="T">不可变图元素类型。</typeparam>
    /// <param name="items">待复制集合。</param>
    /// <param name="parameterName">异常中使用的参数名称。</param>
    /// <returns>不暴露底层数组写入口的只读副本。</returns>
    private static IReadOnlyList<T> CopyItems<T>(IReadOnlyList<T> items, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(items, parameterName);
        if (items.Count == 0)
        {
            return Array.Empty<T>();
        }

        T[] copy = new T[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            copy[index] = items[index]
                ?? throw new ArgumentException("图元素不能为 null。", parameterName);
        }

        return Array.AsReadOnly(copy);
    }

    /// <summary>
    /// 验证画布维度可用于布局和缩放，防止无穷值或非正数污染视觉树尺寸。
    /// </summary>
    /// <param name="value">候选画布维度。</param>
    /// <param name="parameterName">异常中使用的参数名称。</param>
    /// <returns>通过验证的原始维度。</returns>
    private static double ValidateCanvasDimension(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "画布尺寸必须是有限正数。");
        }

        return value;
    }

    /// <summary>
    /// 按稳定顺序比较两组不可变元素，绘制顺序变化也视为模型变化。
    /// </summary>
    /// <typeparam name="T">支持强类型相等比较的图元素类型。</typeparam>
    /// <param name="left">左侧集合。</param>
    /// <param name="right">右侧集合。</param>
    /// <returns>数量、顺序和元素均相同时返回 <see langword="true"/>。</returns>
    private static bool AreItemsEqual<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : IEquatable<T>
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 将有序元素加入组合哈希，确保绘制顺序参与模型身份判定。
    /// </summary>
    /// <typeparam name="T">图元素类型。</typeparam>
    /// <param name="hashCode">正在构建的组合哈希。</param>
    /// <param name="items">待加入的有序元素。</param>
    private static void AddItemsToHash<T>(ref HashCode hashCode, IReadOnlyList<T> items)
    {
        hashCode.Add(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            hashCode.Add(items[index]);
        }
    }
}
