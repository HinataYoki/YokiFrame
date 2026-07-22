#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 定义单个 Kit 向宿主公开 Snapshot 与 Command 的最小交互契约。
    /// </summary>
    public interface IYokiFrameKitInteractionProvider : IYokiFrameCommandHandler
    {
        /// <summary>获取当前 Provider 负责的稳定 Kit 标识。</summary>
        string Kit { get; }

        /// <summary>获取当前 Runtime 实际可以创建的 Snapshot 名称。</summary>
        IReadOnlyList<string> SnapshotNames { get; }

        /// <summary>获取当前 Runtime 实际可以执行的 Command 描述。</summary>
        IReadOnlyList<YokiFrameCommandDescriptor> Commands { get; }

        /// <summary>
        /// 创建指定 Snapshot 的业务 payload；宿主负责补充 session、generation 和传输信封。
        /// </summary>
        /// <param name="snapshotName">已在 <see cref="SnapshotNames"/> 声明的名称。</param>
        /// <returns>Kit 自己定义 schema 的 JSON payload。</returns>
        string CreateSnapshot(string snapshotName);
    }
}
#endif
