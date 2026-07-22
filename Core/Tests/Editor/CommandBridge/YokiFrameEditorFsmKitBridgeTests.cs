using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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

        /// <summary>
        /// 验证 FsmKit/state payload 直接包含已注册状态机的 Workbench 诊断列表。
        /// </summary>
        [Test]
        public void CreateStatePayloadJsonForFsmKitContainsRegisteredMachine()
        {
            FSM<PayloadStateId> fsm = new FSM<PayloadStateId>("UnityPayload");
            fsm.Add(PayloadStateId.Idle, new EmptyState());

            string payloadJson = InvokeStatePayloadJson("FsmKit");
            FsmSnapshotEnvelope payload = JsonUtility.FromJson<FsmSnapshotEnvelope>(payloadJson);

            Assert.AreEqual(1, payload.fsms.Length);
            Assert.AreEqual("UnityPayload", payload.fsms[0].name);
        }

        /// <summary>
        /// 验证 Editor dispatcher 已注册 FsmKit handler，并能返回成功 terminal result。
        /// </summary>
        [Test]
        public void EditorDispatcherRoutesFsmKitReadOnlyCommand()
        {
            FSM<PayloadStateId> fsm = new FSM<PayloadStateId>("UnityCommand");
            fsm.Add(PayloadStateId.Idle, new EmptyState());
            YokiFrameCommandDispatcher dispatcher = GetEditorDispatcher();
            YokiFrameCommandRequest request = new YokiFrameCommandRequest(
                "cli",
                "FsmKit",
                "list_all",
                "{}",
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L);

            YokiFrameCommandResult result = dispatcher.Dispatch(request);

            Assert.IsTrue(result.IsSuccess, result.ErrorCode + ": " + result.ErrorMessage);
            StringAssert.Contains("UnityCommand", result.ResultJson);
        }

        /// <summary>
        /// 反射调用 Editor pump 的 payload JSON 构造方法，让缺失宿主接入表现为明确测试红灯。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <returns>指定 Kit 的 payload JSON。</returns>
        private static string InvokeStatePayloadJson(string kit)
        {
            MethodInfo method = typeof(YokiFrameEditorFileBridgePump).GetMethod(
                "CreateStatePayloadJson",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Unity Editor pump 尚未提供 FsmKit payload JSON 入口。");
            return (string)method.Invoke(null, new object[] { kit });
        }

        /// <summary>
        /// 读取 Editor pump 实际使用的 dispatcher，验证策略与 handler 的最终组合结果。
        /// </summary>
        /// <returns>Editor pump 的共享 dispatcher。</returns>
        private static YokiFrameCommandDispatcher GetEditorDispatcher()
        {
            FieldInfo field = typeof(YokiFrameEditorFileBridgePump).GetField(
                "sCommandDispatcher",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (YokiFrameCommandDispatcher)field.GetValue(null);
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

        [Serializable]
        private sealed class FsmSnapshotEnvelope
        {
            public FsmSummary[] fsms = Array.Empty<FsmSummary>();
        }

        [Serializable]
        private sealed class FsmSummary
        {
            public string name = string.Empty;
        }
    }
}
