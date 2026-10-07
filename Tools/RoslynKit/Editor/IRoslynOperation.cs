#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 定义一个引擎操作的执行入口；实现必须运行在宿主主线程，并保持短同步预算。
    /// </summary>
    public interface IRoslynOperation
    {
        /// <summary>获取操作描述。</summary>
        RoslynOperationDescriptor Descriptor { get; }

        /// <summary>
        /// 执行操作；已通过 Policy 与 Gate 的命令才会到达这里。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令终态结果。</returns>
        YokiFrameCommandResult Execute(YokiFrameCommandRequest request);
    }
}
#endif
