using YokiFrame;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Validation;

namespace YokiFrame.Protocol.Telemetry.SharedMemory;

/// <summary>
/// 生成 Shared Memory telemetry v1 的标准 segment 名称。
/// 长度上限的权威校验在 <see cref="YokiFrameSharedMemoryTelemetrySegmentName"/>；此处只负责把超限翻译为协议错误。
/// </summary>
public static class SharedMemoryTelemetrySegmentName
{
    /// <summary>
    /// 跨平台共享内存名称的保守长度上限；避免 Windows kernel object 和 Unix fallback 超出平台限制。
    /// </summary>
    public const int MAX_SEGMENT_NAME_LENGTH = 240;

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
        try
        {
            // 权威长度校验由 Core 契约完成；超限时按 name 参数抛出，可在此翻译为稳定协议错误。
            return YokiFrameSharedMemoryTelemetrySegmentName.Create(projectScopeId, safeEngineId, safeKit, safeName);
        }
        catch (ArgumentException exception) when (exception.ParamName == nameof(name))
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "TelemetrySegmentNameTooLong",
                $"Telemetry segment name must not exceed {MAX_SEGMENT_NAME_LENGTH} characters.",
                "Use shorter engine, Kit and telemetry names.",
                Array.Empty<string>()));
        }
    }
}
