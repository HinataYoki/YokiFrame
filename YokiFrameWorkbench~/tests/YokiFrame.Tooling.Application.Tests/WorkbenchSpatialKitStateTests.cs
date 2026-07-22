using YokiFrame.Tooling.Application.Models.SpatialKit;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>验证 SpatialKit Workbench 状态模型对密度网格和坏数据的边界处理。</summary>
public sealed class WorkbenchSpatialKitStateTests
{
    /// <summary>验证 state 中的索引、密度 bin 和热点均能解析。</summary>
    [Fact]
    public void ParseState_ReadsIndexDensityAndHotspots()
    {
        WorkbenchSpatialKitDataSource source = CreateSource(
            "{\"schemaVersion\":1,\"version\":7,\"stats\":{\"activeIndexCount\":1,\"entityCount\":3,\"partitionCount\":2,\"hashGridCount\":1},\"indexes\":[{\"diagnosticsId\":\"hash-grid-1\",\"indexKind\":\"HashGrid\",\"entityTypeName\":\"Enemy\",\"count\":3,\"plane\":\"XZ\",\"cellSize\":2,\"partitionCount\":2,\"createdAtUtc\":\"2026-07-18T00:00:00Z\",\"density\":{\"diagnosticsId\":\"hash-grid-1\",\"indexKind\":\"HashGrid\",\"plane\":\"XZ\",\"resolution\":2,\"minA\":0,\"minB\":0,\"maxA\":4,\"maxB\":4,\"totalBins\":4,\"occupiedBins\":2,\"minCount\":1,\"meanCount\":1,\"p95Count\":2,\"maxCount\":2,\"bins\":[1,0,0,2],\"hotspots\":[{\"x\":1,\"y\":1,\"count\":2}]}}],\"indexesTruncated\":false}");

        WorkbenchSpatialKitState state = WorkbenchSpatialKitStateParser.Parse(source);

        Assert.Equal(7L, state.Version);
        Assert.Equal(1, state.ActiveIndexCount);
        Assert.Equal(3, state.EntityCount);
        WorkbenchSpatialDensity density = Assert.Single(state.Indexes).Density!;
        Assert.Equal(new[] { 1, 0, 0, 2 }, density.Bins);
        Assert.Equal(2, density.MaxCount);
        Assert.Equal(new WorkbenchSpatialHotspot(1, 1, 2), Assert.Single(density.Hotspots));
    }

    /// <summary>验证 Octree 保持三维索引语义，并明确标注密度图的 XZ 投影与 Y 轴聚合。</summary>
    [Fact]
    public void ParseState_OctreeExposesExplicitProjectionLabels()
    {
        WorkbenchSpatialKitDataSource source = CreateSource(
            "{\"schemaVersion\":1,\"version\":8,\"stats\":{\"activeIndexCount\":1},\"indexes\":[{\"diagnosticsId\":\"octree-1\",\"indexKind\":\"Octree\",\"entityTypeName\":\"Enemy\",\"count\":2,\"plane\":\"\",\"partitionCount\":9,\"density\":{\"diagnosticsId\":\"octree-1\",\"indexKind\":\"Octree\",\"plane\":\"XZ\",\"resolution\":2,\"totalBins\":4,\"occupiedBins\":1,\"bins\":[0,0,0,2]}}]}");

        WorkbenchSpatialIndex index = Assert.Single(WorkbenchSpatialKitStateParser.Parse(source).Indexes);

        Assert.Equal("XZ投影", index.PlaneBadge);
        Assert.Equal("XZ 投影 · Y 轴聚合", index.ProjectionDescription);
        Assert.Equal(string.Empty, index.Plane);
    }

    /// <summary>验证 schema 或 JSON 损坏时不会把缺少观测误报成零实体。</summary>
    [Fact]
    public void ParseState_InvalidPayload_PreservesStaleReason()
    {
        WorkbenchSpatialKitState state = WorkbenchSpatialKitStateParser.Parse(
            CreateSource("{\"schemaVersion\":99}"));

        Assert.False(string.IsNullOrWhiteSpace(state.StaleReason));
        Assert.Contains("schemaVersion", state.StaleReason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.Indexes);
    }

    /// <summary>创建带有固定来源信息的解析输入。</summary>
    private static WorkbenchSpatialKitDataSource CreateSource(string payload)
    {
        return new WorkbenchSpatialKitDataSource(
            "unity", "session-1", 3L, "Editor", DateTimeOffset.UtcNow,
            "snapshot", string.Empty, Array.Empty<string>(), string.Empty, payload);
    }
}
