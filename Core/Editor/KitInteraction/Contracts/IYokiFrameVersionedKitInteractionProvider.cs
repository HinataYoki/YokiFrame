#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// 标记 state 可以同步到 Shared Memory Telemetry 的版本化 Provider。
    /// 本接口不增加成员；与 <see cref="IYokiFrameSnapshotVersionedKitInteractionProvider"/> 的唯一区别
    /// 是宿主是否把 state 写入 Shared Memory。只走 FileBridge 快照的 Kit（例如 SaveKit）不得实现本接口。
    /// </summary>
    public interface IYokiFrameVersionedKitInteractionProvider : IYokiFrameSnapshotVersionedKitInteractionProvider
    {
    }
}
#endif
