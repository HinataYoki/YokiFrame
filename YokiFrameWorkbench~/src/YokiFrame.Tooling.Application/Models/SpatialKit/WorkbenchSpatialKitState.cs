namespace YokiFrame.Tooling.Application.Models.SpatialKit;

/// <summary>提供 Workbench 可绑定的 SpatialKit 实例、密度和健康状态。</summary>
public sealed class WorkbenchSpatialKitState
{
    /// <summary>创建完整 SpatialKit 页面状态。</summary>
    internal WorkbenchSpatialKitState(
        WorkbenchSpatialKitDataSource source,
        long version,
        int activeIndexCount,
        int entityCount,
        int partitionCount,
        int hashGridCount,
        int quadtreeCount,
        int octreeCount,
        IReadOnlyList<WorkbenchSpatialIndex> indexes,
        bool indexesTruncated)
    {
        DataSource = source;
        Version = version;
        ActiveIndexCount = activeIndexCount;
        EntityCount = entityCount;
        PartitionCount = partitionCount;
        HashGridCount = hashGridCount;
        QuadtreeCount = quadtreeCount;
        OctreeCount = octreeCount;
        Indexes = indexes;
        IndexesTruncated = indexesTruncated;
    }

    private WorkbenchSpatialKitDataSource DataSource { get; }

    /// <summary>获取目标 engine。</summary>
    public string EngineId => DataSource.EngineId;
    /// <summary>获取宿主 session。</summary>
    public string SessionId => DataSource.SessionId;
    /// <summary>获取宿主 generation。</summary>
    public long Generation => DataSource.Generation;
    /// <summary>获取宿主模式。</summary>
    public string Mode => DataSource.Mode;
    /// <summary>获取 telemetry、snapshot 或 command 来源。</summary>
    public string Source => DataSource.Source;
    /// <summary>获取实际命令传输方式；周期状态为空。</summary>
    public string Transport => DataSource.Transport;
    /// <summary>获取本地观察更新时间。</summary>
    public DateTimeOffset UpdatedAtUtc => DataSource.UpdatedAtUtc;
    /// <summary>获取 stale 或解析失败原因。</summary>
    public string StaleReason => DataSource.StaleReason;
    /// <summary>获取来源证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths => DataSource.EvidencePaths;
    /// <summary>获取 Runtime 诊断版本。</summary>
    public long Version { get; }
    /// <summary>获取活跃索引数量。</summary>
    public int ActiveIndexCount { get; }
    /// <summary>获取全部实体数量。</summary>
    public int EntityCount { get; }
    /// <summary>获取全部分区/节点数量。</summary>
    public int PartitionCount { get; }
    /// <summary>获取 HashGrid 数量。</summary>
    public int HashGridCount { get; }
    /// <summary>获取 Quadtree 数量。</summary>
    public int QuadtreeCount { get; }
    /// <summary>获取 Octree 数量。</summary>
    public int OctreeCount { get; }
    /// <summary>获取当前实例列表。</summary>
    public IReadOnlyList<WorkbenchSpatialIndex> Indexes { get; }
    /// <summary>获取实例列表是否被有界裁剪。</summary>
    public bool IndexesTruncated { get; }
}

/// <summary>描述一个运行中的 SpatialKit 索引实例。</summary>
public sealed record WorkbenchSpatialIndex(
    string DiagnosticsId,
    string IndexKind,
    string EntityTypeName,
    int Count,
    string Plane,
    float CellSize,
    int MaxDepth,
    int MaxEntitiesPerNode,
    int PartitionCount,
    DateTimeOffset CreatedAtUtc,
    WorkbenchSpatialBounds2D? Bounds2D,
    WorkbenchSpatialBounds3D? Bounds3D,
    WorkbenchSpatialDensity? Density)
{
    /// <summary>获取列表使用的紧凑平面或投影标签。</summary>
    public string PlaneBadge => !string.IsNullOrWhiteSpace(Plane)
        ? Plane
        : Density == null || string.IsNullOrWhiteSpace(Density.Plane)
            ? "3D"
            : Density.Plane + "投影";

    /// <summary>获取详情使用的投影说明，明确 Octree 热力图沿 Y 轴聚合。</summary>
    public string ProjectionDescription => string.Equals(IndexKind, "Octree", StringComparison.OrdinalIgnoreCase)
        && Density != null
        && !string.IsNullOrWhiteSpace(Density.Plane)
            ? Density.Plane + " 投影 · Y 轴聚合"
            : PlaneBadge;
}

/// <summary>描述二维索引的根边界。</summary>
public sealed record WorkbenchSpatialBounds2D(float X, float Y, float Width, float Height);

/// <summary>描述三维索引的根边界。</summary>
public sealed record WorkbenchSpatialBounds3D(
    WorkbenchSpatialVector3 Center,
    WorkbenchSpatialVector3 Size);

/// <summary>描述三维坐标。</summary>
public sealed record WorkbenchSpatialVector3(float X, float Y, float Z);

/// <summary>描述二维密度聚合和固定大小热力图。</summary>
public sealed record WorkbenchSpatialDensity(
    string DiagnosticsId,
    string IndexKind,
    string Plane,
    int Resolution,
    float MinA,
    float MinB,
    float MaxA,
    float MaxB,
    int TotalBins,
    int OccupiedBins,
    int MinCount,
    int MeanCount,
    int P95Count,
    int MaxCount,
    IReadOnlyList<int> Bins,
    IReadOnlyList<WorkbenchSpatialHotspot> Hotspots);

/// <summary>描述一个密度热点 bin。</summary>
public sealed record WorkbenchSpatialHotspot(int X, int Y, int Count);
