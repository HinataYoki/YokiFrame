#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 自动化运行状态；取消与超时请求不代表用户代码已停止。
    /// 只服务 Editor/Tools，不进入 Player。
    /// </summary>
    public enum RunStatus
    {
        /// <summary>已入队，尚未被调度器认领。</summary>
        Queued,

        /// <summary>正在运行。</summary>
        Running,

        /// <summary>已收到取消请求，等待用户代码退出。</summary>
        CancelRequested,

        /// <summary>已收到超时请求，等待用户代码退出。</summary>
        TimeoutRequested,

        /// <summary>终态：全部断言通过。</summary>
        Passed,

        /// <summary>终态：存在断言失败。</summary>
        Failed,

        /// <summary>终态：用户代码抛出未捕获异常。</summary>
        Errored,

        /// <summary>终态：观察到用户代码在宽限期内退出（取消）。</summary>
        Cancelled,

        /// <summary>终态：观察到用户代码在宽限期内退出（超时）。</summary>
        Timeout,

        /// <summary>终态：宽限期外仍未退出，框架已放弃等待；用户代码可能仍在运行。</summary>
        Detached,

        /// <summary>终态：跨域重载后无终态记录，禁止自动重跑。</summary>
        Unknown,

        /// <summary>正在编译用户源码，尚未进入运行。</summary>
        Compiling,

        /// <summary>终态：编译失败，用户代码没有运行。</summary>
        CompileFailed
    }
}
#endif
