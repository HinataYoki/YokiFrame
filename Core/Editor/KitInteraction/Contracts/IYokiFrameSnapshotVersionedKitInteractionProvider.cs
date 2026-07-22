#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// 为只走 FileBridge Snapshot 的低频 Kit 状态提供版本号，宿主只在状态变化时重写文件快照。
    /// </summary>
    public interface IYokiFrameSnapshotVersionedKitInteractionProvider : IYokiFrameKitInteractionProvider
    {
        /// <summary>
        /// 获取当前 state Snapshot 的单调变化版本；领域状态变化时必须递增。
        /// </summary>
        long StateVersion { get; }
    }
}
#endif
