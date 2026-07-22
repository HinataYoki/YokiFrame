using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Protocol.Tests;

/// <summary>
/// 验证项目级 Telemetry notification 名称的隔离和稳定性。
/// </summary>
public sealed class SharedMemoryTelemetryNotificationNameTests
{
    /// <summary>
    /// 验证不同项目即使使用相同 engine 也不会共享通知名称。
    /// </summary>
    [Fact]
    public void DifferentProjectsGetDifferentNotificationNames()
    {
        string firstScope = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(
            Path.Combine(Path.GetTempPath(), "YokiFrame-project-a"));
        string secondScope = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(
            Path.Combine(Path.GetTempPath(), "YokiFrame-project-b"));

        string firstName = YokiFrameSharedMemoryTelemetryNotificationName.Create(firstScope, "unity-editor");
        string secondName = YokiFrameSharedMemoryTelemetryNotificationName.Create(secondScope, "unity-editor");

        Assert.NotEqual(firstName, secondName);
    }

    /// <summary>
    /// 验证同一项目和 engine 的通知名称可稳定重建，支持 Workbench 重连。
    /// </summary>
    [Fact]
    public void SameProjectGetsStableNotificationName()
    {
        string scope = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(
            Path.Combine(Path.GetTempPath(), "YokiFrame-project-stable"));

        Assert.Equal(
            YokiFrameSharedMemoryTelemetryNotificationName.Create(scope, "unity-editor"),
            YokiFrameSharedMemoryTelemetryNotificationName.Create(scope, "unity-editor"));
    }
}
