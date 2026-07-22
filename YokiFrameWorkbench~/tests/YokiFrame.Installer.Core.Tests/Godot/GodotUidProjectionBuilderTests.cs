using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests.Godot;

/// <summary>
/// 锁定 UID sidecar 只跟随最终 Godot 包投影中的 C# 与 GDScript 资源。
/// </summary>
public sealed class GodotUidProjectionBuilderTests
{
    /// <summary>
    /// 验证只有基础投影内的 .cs 和 .gd 生成 sidecar，配置、普通文件与已有 .uid 不会继续派生。
    /// </summary>
    [Fact]
    public void BuildCreatesSidecarsOnlyForProjectedScriptResources()
    {
        using GodotUidProjectionFixture fixture = GodotUidProjectionFixture.Create();
        var projection = fixture.CreateProjection(
            "Core/Alpha.cs",
            "Core/Beta.GD",
            "Core/plugin.cfg",
            "Core/readme.txt",
            "Core/Alpha.cs.uid");

        var sidecars = new GodotUidProjectionBuilder().Build(projection, fixture.TargetPackageRoot);

        Assert.Equal(
            new[] { "Core/Alpha.cs.uid", "Core/Beta.GD.uid" },
            sidecars.Select(static sidecar => sidecar.RelativePath));
        Assert.All(sidecars, static sidecar => Assert.EndsWith("\n", sidecar.Content, StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证目标包中合法既有 UID 会逐字节保留，不因确定性算法重新写值。
    /// </summary>
    [Fact]
    public void BuildPreservesExistingValidUidContent()
    {
        using GodotUidProjectionFixture fixture = GodotUidProjectionFixture.Create();
        var projection = fixture.CreateProjection("Core/Alpha.cs");
        const string existing = "uid://abc123\n";
        fixture.WriteTargetSidecar("Core/Alpha.cs.uid", existing);

        var sidecar = Assert.Single(new GodotUidProjectionBuilder().Build(projection, fixture.TargetPackageRoot));

        Assert.Equal(existing, sidecar.Content);
    }

    /// <summary>
    /// 验证目标包中的无效 UID 会按资源 res 路径重新生成合法确定性内容。
    /// </summary>
    [Fact]
    public void BuildRepairsInvalidExistingUidContent()
    {
        using GodotUidProjectionFixture fixture = GodotUidProjectionFixture.Create();
        var projection = fixture.CreateProjection("Core/Alpha.cs");
        fixture.WriteTargetSidecar("Core/Alpha.cs.uid", "uid://invalid-z\n");
        GodotUidGenerator generator = new();

        var sidecar = Assert.Single(new GodotUidProjectionBuilder().Build(projection, fixture.TargetPackageRoot));

        Assert.Equal(
            generator.Generate("res://addons/yokiframe/package/YokiFrame/Core/Alpha.cs") + "\n",
            sidecar.Content);
        Assert.True(generator.IsValid(sidecar.Content));
    }
}
