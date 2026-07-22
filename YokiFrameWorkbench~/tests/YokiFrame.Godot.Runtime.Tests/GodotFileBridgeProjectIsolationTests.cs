using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 验证 Godot Runtime FastChannel 的跨项目隔离与路径隐私。
/// </summary>
public sealed partial class GodotFileBridgeHostTests
{
    /// <summary>
    /// 验证 Godot FastChannel endpoint 显式包含统一项目作用域，且不会泄露项目目录名称。
    /// </summary>
    [Fact]
    public void FastChannelEndpointUsesHashedProjectScope()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();

        var address = ReadEndpointAddress(fixture);
        var projectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(fixture.ProjectRoot);

        Assert.Contains(projectScopeId, address, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFileName(fixture.ProjectRoot), address, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证两个项目的 Godot Host 把各自作用域写入本机 endpoint，避免同机会话名称跨项目碰撞。
    /// </summary>
    [Fact]
    public void DifferentProjectsPublishDifferentScopedFastChannelEndpoints()
    {
        using GodotFileBridgeHostFixture firstFixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHostFixture secondFixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost firstHost = new(firstFixture.ProjectRoot, "4.7.0");
        using GodotFileBridgeHost secondHost = new(secondFixture.ProjectRoot, "4.7.0");
        firstHost.Start();
        secondHost.Start();

        var firstAddress = ReadEndpointAddress(firstFixture);
        var secondAddress = ReadEndpointAddress(secondFixture);
        var firstScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(firstFixture.ProjectRoot);
        var secondScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(secondFixture.ProjectRoot);

        Assert.NotEqual(firstScopeId, secondScopeId);
        Assert.Contains(firstScopeId, firstAddress, StringComparison.Ordinal);
        Assert.Contains(secondScopeId, secondAddress, StringComparison.Ordinal);
        Assert.NotEqual(firstAddress, secondAddress);
    }

    /// <summary>
    /// 读取 fixture 当前发布的 FastChannel 地址，缺失时返回空字符串供断言形成明确失败。
    /// </summary>
    /// <param name="fixture">已经启动 Host 的项目 fixture。</param>
    /// <returns>Named Pipe 名称或 Unix Domain Socket 绝对路径。</returns>
    private static string ReadEndpointAddress(GodotFileBridgeHostFixture fixture)
    {
        return fixture.ReadFastChannelEndpoint()["endpoint"]?.GetValue<string>() ?? string.Empty;
    }
}
