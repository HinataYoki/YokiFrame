using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>验证两个 Unity 只读诊断命令的路由、payload 和宿主身份门禁。</summary>
    public sealed class YokiFrameUnityHarnessObservationCommandHandlerTests
    {
        /// <summary>验证两个诊断 action 均被描述为只读命令。</summary>
        [Test]
        public void CreateCommandDescriptorsContainsOnlyValidationActions()
        {
            var descriptors = YokiFrameUnityHarnessObservationCommandHandler.CreateCommandDescriptors();

            Assert.AreEqual(2, descriptors.Length);
            Assert.AreEqual("Validation", descriptors[0].Kit);
            Assert.AreEqual("inspect_status", descriptors[0].Action);
            Assert.AreEqual(YokiFrameCommandKind.ReadOnly, descriptors[0].Kind);
            Assert.AreEqual("Validation", descriptors[1].Kit);
            Assert.AreEqual("get_console_errors", descriptors[1].Action);
            Assert.AreEqual(YokiFrameCommandKind.ReadOnly, descriptors[1].Kind);
        }

        /// <summary>验证非对象 payload 被拒绝，不会触发 Unity 状态查询。</summary>
        [Test]
        public void HandleRejectsNonObjectPayload()
        {
            var handler = new YokiFrameUnityHarnessObservationCommandHandler(CreateContext);
            var result = handler.Handle(CreateRequest("Validation", "inspect_status", "[]"));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("InvalidPayload", result.ErrorCode);
        }

        /// <summary>验证花括号包裹但结构损坏的 JSON 也会被拒绝。</summary>
        [Test]
        public void HandleRejectsMalformedObjectPayload()
        {
            var handler = new YokiFrameUnityHarnessObservationCommandHandler(CreateContext);
            var result = handler.Handle(CreateRequest("Validation", "get_console_errors", "{\"invalid\":}"));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("InvalidPayload", result.ErrorCode);
        }

        /// <summary>验证 Console 返回明细不能突破协议固定上限。</summary>
        [Test]
        public void HandleRejectsConsoleCountAboveLimit()
        {
            var handler = new YokiFrameUnityHarnessObservationCommandHandler(CreateContext);
            var result = handler.Handle(CreateRequest(
                "Validation",
                "get_console_errors",
                "{\"maxCount\":101}"));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("InvalidPayload", result.ErrorCode);
        }

        /// <summary>验证缺失 session/generation 时拒绝生成无法关联的结果。</summary>
        [Test]
        public void HandleRejectsUnavailableIdentity()
        {
            var handler = new YokiFrameUnityHarnessObservationCommandHandler(
                () => new YokiFrameUnityHarnessContext { sessionId = string.Empty, generation = 0L });
            var result = handler.Handle(CreateRequest("Validation", "inspect_status", "{}"));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("ValidationIdentityUnavailable", result.ErrorCode);
        }

        /// <summary>创建有效测试上下文。</summary>
        /// <returns>宿主上下文。</returns>
        private static YokiFrameUnityHarnessContext CreateContext()
        {
            return new YokiFrameUnityHarnessContext
            {
                engineId = "unity-editor",
                mode = "EditMode",
                sessionId = "test-session",
                generation = 1L,
                sequence = 1L
            };
        }

        /// <summary>创建最小命令请求。</summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="action">action 标识。</param>
        /// <param name="payload">payload JSON。</param>
        /// <returns>命令请求。</returns>
        private static YokiFrameCommandRequest CreateRequest(string kit, string action, string payload)
        {
            return new YokiFrameCommandRequest("test", kit, action, payload, YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS, 0L);
        }
    }
}
