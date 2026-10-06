#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 每个引擎实现一份：声明本宿主的身份、可承载的目标、可用操作，并给出当前引擎状态。
    /// </summary>
    /// <remarks>
    /// 该接口是胶水层的引擎侧接缝：新增引擎只需实现它并注册 Provider，CommandBridge 与工具侧无需改动。
    /// </remarks>
    public interface IYokiFrameEngineOperationProvider
    {
        /// <summary>获取引擎类型，例如 Unity。</summary>
        string EngineKind { get; }

        /// <summary>获取引擎版本文本。</summary>
        string EngineVersion { get; }

        /// <summary>获取当前宿主能够承载的执行目标。</summary>
        YokiFrameEngineExecutionTarget HostTargets { get; }

        /// <summary>获取当前宿主实际支持的操作；capabilities 与命令面都由它聚合。</summary>
        IReadOnlyList<IYokiFrameEngineOperation> Operations { get; }

        /// <summary>读取当前引擎状态；必须运行在宿主主线程且不得阻塞。</summary>
        /// <returns>引擎状态。</returns>
        YokiFrameEngineDomainState ReadDomainState();

        /// <summary>获取异步运行调度器；未接入时返回 null。</summary>
        IYokiFrameEngineRunScheduler Runs { get; }
    }
}
#endif
