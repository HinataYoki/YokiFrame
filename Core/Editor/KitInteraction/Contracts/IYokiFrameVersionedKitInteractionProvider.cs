#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// 标记 state 可以同步到 Shared Memory Telemetry 的版本化 Provider。
    /// </summary>
    public interface IYokiFrameVersionedKitInteractionProvider : IYokiFrameSnapshotVersionedKitInteractionProvider
    {
    }
}
#endif
