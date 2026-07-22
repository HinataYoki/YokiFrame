using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Runtime CommandBridge dispatcher 的策略门控和 handler 路由行为。
    /// </summary>
    public sealed class YokiFrameCommandDispatcherTests
    {
        /// <summary>
        /// 验证来源被策略拒绝时不会调用 handler，避免绕过 CommandPolicy。
        /// </summary>
        [Test]
        public void DispatchRejectsDisallowedSourceWithoutInvokingHandler()
        {
            RecordingHandler handler = new RecordingHandler("System", "ping");
            YokiFrameCommandDispatcher dispatcher = new YokiFrameCommandDispatcher(
                YokiFrameCommandPolicy.CreateDefault(),
                new IYokiFrameCommandHandler[] { handler });

            YokiFrameCommandResult result = dispatcher.Dispatch(new YokiFrameCommandRequest(
                "external",
                "System",
                "ping",
                "{}",
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("PolicyRejected", result.ErrorCode);
            Assert.IsFalse(handler.WasInvoked);
        }

        /// <summary>
        /// 验证允许的命令会按 Kit/action 路由到匹配 handler，并保留业务 JSON。
        /// </summary>
        [Test]
        public void DispatchRoutesAllowedCommandToMatchingHandler()
        {
            RecordingHandler handler = new RecordingHandler("System", "ping");
            YokiFrameCommandDispatcher dispatcher = new YokiFrameCommandDispatcher(
                YokiFrameCommandPolicy.CreateDefault(),
                new IYokiFrameCommandHandler[] { handler });

            YokiFrameCommandResult result = dispatcher.Dispatch(new YokiFrameCommandRequest(
                "cli",
                "System",
                "ping",
                "{}",
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("{\"ok\":true}", result.ResultJson);
            Assert.IsTrue(handler.WasInvoked);
        }

        /// <summary>
        /// 验证策略允许但宿主未注册 handler 时返回终态错误，避免 CLI 等待超时。
        /// </summary>
        [Test]
        public void DispatchReturnsHandlerMissingWhenAllowedCommandHasNoHandler()
        {
            YokiFrameCommandDispatcher dispatcher = new YokiFrameCommandDispatcher(
                YokiFrameCommandPolicy.CreateDefault(),
                new IYokiFrameCommandHandler[0]);

            YokiFrameCommandResult result = dispatcher.Dispatch(new YokiFrameCommandRequest(
                "cli",
                "System",
                "ping",
                "{}",
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("HandlerMissing", result.ErrorCode);
        }

        /// <summary>
        /// 记录 dispatcher 是否真正调用了匹配 handler，并返回固定成功结果。
        /// </summary>
        private sealed class RecordingHandler : IYokiFrameCommandHandler
        {
            private readonly string mKit;
            private readonly string mAction;

            /// <summary>
            /// 创建测试 handler；只匹配指定 Kit/action。
            /// </summary>
            /// <param name="kit">测试 Kit 标识。</param>
            /// <param name="action">测试 action 标识。</param>
            public RecordingHandler(string kit, string action)
            {
                mKit = kit;
                mAction = action;
            }

            /// <summary>
            /// 获取 handler 是否被 dispatcher 调用过。
            /// </summary>
            public bool WasInvoked { get; private set; }

            /// <summary>
            /// 判断当前 handler 是否处理指定命令。
            /// </summary>
            /// <param name="request">命令请求。</param>
            /// <returns>匹配测试 Kit/action 时返回 true。</returns>
            public bool CanHandle(YokiFrameCommandRequest request)
            {
                return request.Kit == mKit && request.Action == mAction;
            }

            /// <summary>
            /// 记录调用并返回固定成功 JSON。
            /// </summary>
            /// <param name="request">命令请求。</param>
            /// <returns>固定成功结果。</returns>
            public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
            {
                WasInvoked = true;
                return YokiFrameCommandResult.Success("{\"ok\":true}");
            }
        }
    }
}
