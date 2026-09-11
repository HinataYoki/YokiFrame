using System;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 守护 EventKit 跟踪开关可关闭：跟踪一旦开启必须能通过 set_tracking 关闭，
    /// 否则整个会话内每次派发都要付 GetInvocationList 分配、锁与时间戳开销。
    /// </summary>
    public sealed class YokiFrameEventKitTrackingToggleTests
    {
        /// <summary>测试用负载，确保 Type channel 能产生活动通知。</summary>
        private sealed class SamplePayload { }

        /// <summary>每个测试前清空总线与诊断历史。</summary>
        [SetUp]
        public void SetUp()
        {
            EasyEventEditorHook.SetTrackingEnabled(false);
            EventKit.Clear();
            EventKitDiagnosticRegistry.ResetForTests();
        }

        /// <summary>每个测试后释放监听器与历史。</summary>
        [TearDown]
        public void TearDown()
        {
            EasyEventEditorHook.SetTrackingEnabled(false);
            EventKit.Clear();
            EventKitDiagnosticRegistry.ResetForTests();
        }

        /// <summary>构造已匹配 set_tracking 的命令请求。</summary>
        /// <param name="payloadJson">命令 payload。</param>
        /// <returns>命令请求。</returns>
        private static YokiFrameCommandRequest CreateRequest(string payloadJson)
        {
            return new YokiFrameCommandRequest("eventkit-test", "EventKit", "set_tracking", payloadJson, 1000, 64);
        }

        /// <summary>创建 EventKit handler。</summary>
        /// <returns>命令处理器。</returns>
        private static EventKitCommandHandler CreateHandler()
        {
            return new EventKitCommandHandler();
        }

        /// <summary>核心回归：set_tracking(false) 必须真正关闭跟踪。</summary>
        [Test]
        public void SetTrackingFalseDisablesTracking()
        {
            EventKitDiagnosticRegistry.EnsureInitialized();
            Assert.IsTrue(EventKitDiagnosticRegistry.IsTrackingEnabled, "前置：初始化后应默认开启跟踪");

            YokiFrameCommandResult result = CreateHandler().Handle(CreateRequest("{\"trackingEnabled\":false}"));

            Assert.IsTrue(result.IsSuccess, "关闭跟踪的命令应成功");
            Assert.IsFalse(EventKitDiagnosticRegistry.IsTrackingEnabled, "跟踪必须被真正关闭");
            Assert.IsFalse(EasyEventEditorHook.IsTrackingEnabled, "Runtime hook 也必须同步关闭");
        }

        /// <summary>关闭跟踪后不得再积累活动历史。</summary>
        [Test]
        public void ActivityIsNotRecordedAfterTrackingDisabled()
        {
            EventKitDiagnosticRegistry.EnsureInitialized();
            _ = CreateHandler().Handle(CreateRequest("{\"trackingEnabled\":false}"));
            long versionBefore = EventKitDiagnosticRegistry.StateVersion;

            EventKit.Type.Send(new SamplePayload());

            Assert.AreEqual(versionBefore, EventKitDiagnosticRegistry.StateVersion, "关闭后不应再产生活动记录");
        }

        /// <summary>关闭后必须能重新开启，并恢复活动记录。</summary>
        [Test]
        public void SetTrackingTrueReenablesTracking()
        {
            EventKitDiagnosticRegistry.EnsureInitialized();
            EventKitCommandHandler handler = CreateHandler();
            _ = handler.Handle(CreateRequest("{\"trackingEnabled\":false}"));

            YokiFrameCommandResult result = handler.Handle(CreateRequest("{\"trackingEnabled\":true}"));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(EventKitDiagnosticRegistry.IsTrackingEnabled, "跟踪必须可重新开启");
            long versionBefore = EventKitDiagnosticRegistry.StateVersion;
            EventKit.Type.Send(new SamplePayload());
            Assert.Greater(EventKitDiagnosticRegistry.StateVersion, versionBefore, "重新开启后应恢复记录");
        }

        /// <summary>
        /// 缺字段或无法解析的布尔值必须被拒绝且不改变开关状态。
        /// 注意 <c>JsonHelper.TryExtractBool</c> 按既有契约接受可解析的字符串布尔值(如 "false")。
        /// </summary>
        [Test]
        public void InvalidPayloadIsRejectedAndLeavesTrackingUnchanged()
        {
            EventKitDiagnosticRegistry.EnsureInitialized();
            EventKitCommandHandler handler = CreateHandler();

            YokiFrameCommandResult missing = handler.Handle(CreateRequest("{}"));
            YokiFrameCommandResult unparsable = handler.Handle(CreateRequest("{\"trackingEnabled\":\"yes\"}"));

            Assert.IsFalse(missing.IsSuccess, "缺字段必须失败");
            Assert.IsFalse(unparsable.IsSuccess, "无法解析的布尔值必须失败");
            Assert.IsTrue(EventKitDiagnosticRegistry.IsTrackingEnabled, "失败的命令不得改变开关");
        }

        /// <summary>Provider 必须把 set_tracking 暴露为 UserAction 命令。</summary>
        [Test]
        public void ProviderDeclaresSetTrackingAsUserAction()
        {
            var provider = new EventKitInteractionProvider();
            bool found = false;
            for (var index = 0; index < provider.Commands.Count; index++)
            {
                if (provider.Commands[index].Action != "set_tracking") continue;
                found = true;
                Assert.AreEqual(
                    YokiFrameCommandKind.UserAction,
                    provider.Commands[index].Kind,
                    "set_tracking 属用户显式触发的变更命令");
            }

            Assert.IsTrue(found, "Provider 必须声明 set_tracking 命令");
        }
    }
}
