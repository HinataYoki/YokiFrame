using YokiFrame;
using YokiFrame.Protocol.Validation;

namespace YokiFrame.Protocol.Telemetry.SharedMemory;

/// <summary>
/// 生成 Shared Memory telemetry v1 的标准 segment 名称。
/// </summary>
public static class SharedMemoryTelemetrySegmentName
{
    /// <summary>
    /// 根据项目根、engine、Kit 和 snapshot 名称创建标准 segment 名称。
    /// </summary>
    /// <param name="projectRoot">当前宿主项目绝对根目录。</param>
    /// <param name="engineId">安全 engine 标识。</param>
    /// <param name="kit">安全 Kit 标识。</param>
    /// <param name="name">安全 telemetry 名称。</param>
    /// <returns>标准 shared memory segment 名称。</returns>
    public static string Create(string projectRoot, string engineId, string kit, string name)
    {
        var safeEngineId = SafeIdValidator.EnsureSafeId(engineId, nameof(engineId));
        var safeKit = SafeIdValidator.EnsureSafeId(kit, nameof(kit));
        var safeName = SafeIdValidator.EnsureSafeId(name, nameof(name));
        var projectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(projectRoot);
        return YokiFrameSharedMemoryTelemetrySegmentName.Create(projectScopeId, safeEngineId, safeKit, safeName);
    }
}
