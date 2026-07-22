#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// 为命名 Telemetry 提供实例级单调版本，使宿主只重建发生变化的 latest frame。
    /// </summary>
    public interface IYokiFrameVersionedNamedTelemetryProvider : IYokiFrameNamedTelemetryProvider
    {
        /// <summary>
        /// 获取指定命名 Telemetry 的当前版本；活动名称的版本从一开始单调递增。
        /// </summary>
        /// <param name="name">已经由 <see cref="IYokiFrameNamedTelemetryProvider.TelemetryNames"/> 声明的安全名称。</param>
        /// <returns>活动名称的当前版本；名称已经失效或不存在时返回零。</returns>
        long GetTelemetryVersion(string name);
    }
}
#endif
