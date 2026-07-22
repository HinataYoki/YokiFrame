#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 在 CommandPolicy 通过后，把命令分发给宿主注册的 Runtime handler。
    /// </summary>
    public sealed class YokiFrameCommandDispatcher
    {
        private readonly YokiFrameCommandPolicy mPolicy;
        private readonly IYokiFrameCommandHandler[] mHandlers;

        /// <summary>
        /// 创建命令分发器；调用方负责按宿主能力注册 handler。
        /// </summary>
        /// <param name="policy">命令策略。</param>
        /// <param name="handlers">宿主可执行的命令 handler。</param>
        public YokiFrameCommandDispatcher(YokiFrameCommandPolicy policy, IYokiFrameCommandHandler[] handlers)
        {
            mPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            mHandlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        }

        /// <summary>
        /// 评估策略并执行命令，始终返回可写入 terminal response 的结果。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令终态结果。</returns>
        public YokiFrameCommandResult Dispatch(YokiFrameCommandRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var decision = mPolicy.Evaluate(request.ToPolicyRequest());
            if (!decision.IsAllowed)
            {
                return YokiFrameCommandResult.Error(decision.ErrorCode, decision.ErrorMessage);
            }

            var handler = FindHandler(request);
            if (handler == null)
            {
                return YokiFrameCommandResult.Error("HandlerMissing", "No command handler is registered for this allowed command.");
            }

            return DispatchToHandler(handler, request);
        }

        /// <summary>
        /// 查找第一个声明可处理该请求的 handler。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>匹配 handler；未找到时返回 null。</returns>
        private IYokiFrameCommandHandler FindHandler(YokiFrameCommandRequest request)
        {
            for (var index = 0; index < mHandlers.Length; index++)
            {
                var handler = mHandlers[index];
                if (handler != null && handler.CanHandle(request))
                {
                    return handler;
                }
            }

            return null;
        }

        /// <summary>
        /// 调用 handler 并把异常转换成终态错误，保证 FileBridge 调用侧不会卡在处理中。
        /// </summary>
        /// <param name="handler">已匹配的 handler。</param>
        /// <param name="request">命令请求。</param>
        /// <returns>命令终态结果。</returns>
        private static YokiFrameCommandResult DispatchToHandler(
            IYokiFrameCommandHandler handler,
            YokiFrameCommandRequest request)
        {
            try
            {
                var result = handler.Handle(request);
                if (result == null)
                {
                    return YokiFrameCommandResult.Error("HandlerFailed", "Command handler returned no result.");
                }

                return result;
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("HandlerFailed", exception.Message);
            }
        }
    }
}
#endif
