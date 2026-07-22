#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 定义 Runtime CommandBridge 的最小命令 handler 契约。
    /// </summary>
    public interface IYokiFrameCommandHandler
    {
        /// <summary>
        /// 判断当前 handler 是否能处理指定命令。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>可处理时返回 true。</returns>
        bool CanHandle(YokiFrameCommandRequest request);

        /// <summary>
        /// 执行命令并返回终态结果；实现必须避免吞掉需要写入 response 的错误。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令终态结果。</returns>
        YokiFrameCommandResult Handle(YokiFrameCommandRequest request);
    }
}
#endif
