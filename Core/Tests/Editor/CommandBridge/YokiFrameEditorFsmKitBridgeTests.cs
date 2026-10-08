using System;
using NUnit.Framework;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity Editor FileBridge 对 FsmKit 使用真实 Core 诊断数据，而不是通用在线占位。
    /// </summary>
    public sealed class YokiFrameEditorFsmKitBridgeTests
    {
        /// <summary>测试使用的最小状态标识。</summary>
        private enum PayloadStateId
        {
            Idle
        }

        /// <summary>
        /// 每个用例前清空全局 FSM 诊断状态，避免其它测试实例影响数量断言。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            FsmKitCommandHandler.ClearAll();
        }

        /// <summary>
        /// 每个用例后释放全局 FSM 诊断状态，保证失败路径也不会污染后续测试。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            FsmKitCommandHandler.ClearAll();
        }

        /// <summary>验证 FileBridge state payload 直接包含已注册状态机。</summary>
        [Test]
        public void CreateStatePayloadJson_已注册状态机_state包含该实例()
        {
            FSM<PayloadStateId> fsm = new FSM<PayloadStateId>("UnityPayload");
            fsm.Add(PayloadStateId.Idle, new EmptyState());

            string payloadJson = YokiFrameEditorFileBridgePump.CreateStatePayloadJson("FsmKit");
            using (JsonDocument document = JsonDocument.Parse(payloadJson))
            {
                JsonElement fsms = document.RootElement.GetProperty("fsms");
                Assert.AreEqual(JsonValueKind.Array, fsms.ValueKind);
                Assert.AreEqual(1, fsms.GetArrayLength());
                Assert.AreEqual("UnityPayload", fsms[0].GetProperty("name").GetString());
            }

            GC.KeepAlive(fsm);
        }

        /// <summary>验证 Editor dispatcher 将 list_all 路由到真实 FsmKit 结果。</summary>
        [Test]
        public void Dispatch_list_all_返回已注册状态机()
        {
            FSM<PayloadStateId> fsm = new FSM<PayloadStateId>("UnityCommand");
            fsm.Add(PayloadStateId.Idle, new EmptyState());
            YokiFrameCommandDispatcher dispatcher = YokiFrameEditorFileBridgePump.CommandDispatcherForTests;
            YokiFrameCommandRequest request = new YokiFrameCommandRequest(
                "cli",
                "FsmKit",
                "list_all",
                "{}",
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L);

            YokiFrameCommandResult result = dispatcher.Dispatch(request);

            Assert.IsTrue(result.IsSuccess, result.ErrorCode + ": " + result.ErrorMessage);
            using (JsonDocument document = JsonDocument.Parse(result.ResultJson))
            {
                JsonElement fsms = document.RootElement.GetProperty("fsms");
                Assert.AreEqual(1, fsms.GetArrayLength());
                Assert.AreEqual("UnityCommand", fsms[0].GetProperty("name").GetString());
            }

            GC.KeepAlive(fsm);
        }

        /// <summary>
        /// 提供无副作用的最小状态，供宿主桥接测试注册真实 FSM。
        /// </summary>
        private sealed class EmptyState : IState
        {
            /// <summary>始终允许进入。</summary>
            /// <returns>始终返回 true。</returns>
            public bool Condition() => true;

            /// <summary>测试状态无需启动逻辑。</summary>
            public void Start() { }

            /// <summary>测试状态无需暂停逻辑。</summary>
            public void Suspend() { }

            /// <summary>测试状态无需普通更新逻辑。</summary>
            public void Update() { }

            /// <summary>测试状态无需固定更新逻辑。</summary>
            public void FixedUpdate() { }

            /// <summary>测试状态无需自定义更新逻辑。</summary>
            public void CustomUpdate() { }

            /// <summary>测试状态无需结束逻辑。</summary>
            public void End() { }

            /// <summary>测试状态无需释放逻辑。</summary>
            public void Dispose() { }

            /// <summary>测试状态忽略消息。</summary>
            /// <typeparam name="TMsg">消息类型。</typeparam>
            /// <param name="message">消息值。</param>
            public void SendMessage<TMsg>(TMsg message) { }
        }
    }
}
