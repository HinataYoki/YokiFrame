#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 为单个 Kit 提供 action allowlist 和执行入口的基础 handler。
    /// </summary>
    public abstract class YokiFrameKitCommandHandler : IYokiFrameCommandHandler
    {
        private readonly string mKit;
        private readonly string[] mActions;

        /// <summary>
        /// 创建 Kit handler；调用方传入该 handler 支持的 action 集合。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="actions">该 Kit 下允许当前 handler 处理的 action。</param>
        protected YokiFrameKitCommandHandler(string kit, string[] actions)
        {
            mKit = kit ?? throw new ArgumentNullException(nameof(kit));
            mActions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        /// <summary>
        /// 判断请求是否属于当前 Kit 且 action 在 handler 支持范围内。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>匹配时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            if (request == null || !string.Equals(request.Kit, mKit, StringComparison.Ordinal))
            {
                return false;
            }

            return ContainsAction(request.Action);
        }

        /// <summary>
        /// 执行当前 Kit 的命令；不匹配的命令返回终态错误，避免调用侧超时。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令终态结果。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            if (!CanHandle(request))
            {
                return YokiFrameCommandResult.Error("HandlerMismatch", "Command handler does not support this kit or action.");
            }

            return HandleAction(request);
        }

        /// <summary>
        /// 执行已匹配当前 Kit/action 的命令。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令终态结果。</returns>
        protected abstract YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request);

        /// <summary>
        /// 判断 action 是否在当前 handler 支持列表中。
        /// </summary>
        /// <param name="action">action 标识。</param>
        /// <returns>命中时返回 true。</returns>
        private bool ContainsAction(string action)
        {
            for (var index = 0; index < mActions.Length; index++)
            {
                if (string.Equals(action, mActions[index], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
