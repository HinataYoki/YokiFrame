using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 EventKit Editor/Tools 诊断历史、版本、只读 Interaction 和 capability 边界。
    /// </summary>
    public sealed class YokiFrameEventKitInteractionTests
    {
        /// <summary>测试用负载，确保 Type channel 能输出稳定 payloadType。</summary>
        private sealed class SamplePayload { }

        /// <summary>提供第一个同短名负载类型。</summary>
        private static class CollisionScopeA
        {
            internal sealed class Payload { }
            internal enum Signal { Ready }
        }

        /// <summary>提供第二个同短名负载类型。</summary>
        private static class CollisionScopeB
        {
            internal sealed class Payload { }
            internal enum Signal { Ready }
        }

        /// <summary>每个测试前清空总线和诊断历史，保证 sequence 从零开始。</summary>
        [SetUp]
        public void SetUp()
        {
            EasyEventEditorHook.SetTrackingEnabled(false);
            EventKit.Clear();
            EventKitDiagnosticRegistry.ResetForTests();
        }

        /// <summary>每个测试后释放监听器和历史，避免污染其它 EventKit 用例。</summary>
        [TearDown]
        public void TearDown()
        {
            EasyEventEditorHook.SetTrackingEnabled(false);
            EventKit.Clear();
            EventKitDiagnosticRegistry.ResetForTests();
        }

        /// <summary>验证 Runtime 总线在 Editor Provider 真正创建前不会维护诊断历史。</summary>
        [Test]
        public void RuntimeBusStartsTrackingOnlyAfterEditorProviderInitialization()
        {
            EventKit.Type.Send(new SamplePayload());

            Assert.AreEqual(0L, EventKitDiagnosticRegistry.StateVersion);
            GetEventKitProvider();
            EventKit.Type.Send(new SamplePayload());

            Assert.AreEqual(1L, EventKitDiagnosticRegistry.StateVersion);
        }

        /// <summary>验证 Runtime hook 为每次活动分配 sequence，并同步推进版本。</summary>
        [Test]
        public void RuntimeHookPublishesMonotonicVersionAndSequence()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetEventKitProvider();
            Action<SamplePayload> listener = _ => { };

            LinkUnRegister<SamplePayload> token = EventKit.Type.Register(listener);
            long registeredVersion = provider.StateVersion;
            EventKit.Type.Send(new SamplePayload());
            long sentVersion = provider.StateVersion;
            token.UnRegister();

            Assert.AreEqual(1L, registeredVersion);
            Assert.AreEqual(2L, sentVersion);
            Assert.AreEqual(3L, provider.StateVersion);

            EventKitDiagnosticSnapshot diagnostics = EventKitDiagnosticRegistry.CreateSnapshot();
            Assert.AreEqual(3L, diagnostics.Sequence);
            CollectionAssert.AreEqual(
                new long[] { 1L, 2L, 3L },
                diagnostics.Activities.Select(activity => activity.Sequence).ToArray());
        }

        /// <summary>验证 state Snapshot 合并当前监听器与近期活动，并保持唯一只读命令面。</summary>
        [Test]
        public void StateSnapshotContainsRuntimeRegistrationsAndRecentEvents()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetEventKitProvider();
            Action<SamplePayload> listener = _ => { };
            EventKit.Type.Register(listener);
            EventKit.Type.Send(new SamplePayload());

            string payloadJson = provider.CreateSnapshot("state");
            YokiFrameCommandResult commandResult = provider.Handle(new YokiFrameCommandRequest(
                "eventkit-test",
                "EventKit",
                "get_workbench_snapshot",
                "{}",
                1000,
                64));

            Assert.AreEqual(1, provider.Commands.Count);
            Assert.AreEqual("get_workbench_snapshot", provider.Commands[0].Action);
            Assert.IsTrue(commandResult.IsSuccess);
            StringAssert.Contains("\"eventKey\":\"" + typeof(SamplePayload).FullName + "\"", payloadJson);
            StringAssert.Contains("\"payloadType\":\"" + typeof(SamplePayload).FullName + "\"", payloadJson);
            StringAssert.Contains("\"handlerCount\":1", payloadJson);
            StringAssert.Contains("\"sequence\":2", payloadJson);
            StringAssert.Contains("\"kind\":\"register\"", payloadJson);
            StringAssert.Contains("\"kind\":\"send\"", payloadJson);
        }

        /// <summary>验证没有监听器的纯发送事件仍会形成可见行，不依赖静态代码扫描。</summary>
        [Test]
        public void SendWithoutRegistrationStillAppearsInSnapshot()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetEventKitProvider();

            EventKit.Type.Send(new SamplePayload());
            string payloadJson = provider.CreateSnapshot("state");

            StringAssert.Contains("\"eventKey\":\"" + typeof(SamplePayload).FullName + "\"", payloadJson);
            StringAssert.Contains("\"handlerCount\":0", payloadJson);
            StringAssert.Contains("\"lastSequence\":1", payloadJson);
        }

        /// <summary>验证 Type 与 Enum 同短名类型使用完整身份，不会在 Workbench 行中静默折叠。</summary>
        [Test]
        public void SameShortTypeNamesRemainDistinctInSnapshot()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetEventKitProvider();
            EventKit.Type.Register<CollisionScopeA.Payload>(_ => { });
            EventKit.Type.Register<CollisionScopeB.Payload>(_ => { });
            EventKit.Enum.Register(CollisionScopeA.Signal.Ready, () => { });
            EventKit.Enum.Register(CollisionScopeB.Signal.Ready, () => { });

            string payloadJson = provider.CreateSnapshot("state");

            StringAssert.Contains(typeof(CollisionScopeA.Payload).FullName, payloadJson);
            StringAssert.Contains(typeof(CollisionScopeB.Payload).FullName, payloadJson);
            StringAssert.Contains(typeof(CollisionScopeA.Signal).FullName, payloadJson);
            StringAssert.Contains(typeof(CollisionScopeB.Signal).FullName, payloadJson);
            StringAssert.Contains("\"totalEvents\":4", payloadJson);
        }

        /// <summary>验证不存在的直接注销不会制造活动或推进诊断版本。</summary>
        [Test]
        public void MissingDirectUnregisterDoesNotCreateDiagnosticActivity()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetEventKitProvider();
            Action<SamplePayload> listener = _ => { };

            EventKit.Type.UnRegister(listener);

            Assert.AreEqual(0L, provider.StateVersion);
            Assert.AreEqual(0, EventKitDiagnosticRegistry.CreateSnapshot().Activities.Length);
        }

        /// <summary>验证全局 clear 更新既有 typed 行，并保留可供页面归属的活动。</summary>
        [Test]
        public void ClearUpdatesTypedRegistrationAndActivityHistory()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetEventKitProvider();
            EventKit.Type.Register<SamplePayload>(_ => { });

            EventKit.Type.Clear();
            string payloadJson = provider.CreateSnapshot("state");

            StringAssert.Contains("\"kind\":\"clear\"", payloadJson);
            StringAssert.Contains("\"eventKey\":\"*\"", payloadJson);
            StringAssert.Contains("\"lastSequence\":2", payloadJson);
        }

        /// <summary>验证活动历史固定保留最新两百条，同时全局 sequence 不回退。</summary>
        [Test]
        public void DiagnosticHistoryRetainsLatestTwoHundredActivities()
        {
            GetEventKitProvider();
            for (var index = 0; index < 205; index++)
            {
                EventKit.Type.Send(new SamplePayload());
            }

            EventKitDiagnosticSnapshot snapshot = EventKitDiagnosticRegistry.CreateSnapshot();

            Assert.AreEqual(205L, snapshot.Version);
            Assert.AreEqual(205L, snapshot.Sequence);
            Assert.AreEqual(200, snapshot.Activities.Length);
            Assert.AreEqual(6L, snapshot.Activities[0].Sequence);
            Assert.AreEqual(205L, snapshot.Activities[199].Sequence);
        }

        /// <summary>验证 Editor 观察回调失败不会反向中断 Runtime 事件发送和监听器执行。</summary>
        [Test]
        public void EditorObserverFailureDoesNotInterruptRuntimeEventDispatch()
        {
            GetEventKitProvider();
            Action<EventKitEditorNotification> failingObserver = _ =>
                throw new InvalidOperationException("Expected test observer failure.");
            EasyEventEditorHook.Activity += failingObserver;
            try
            {
                var received = 0;
                EventKit.Type.Register<SamplePayload>(_ => received++);
                EventKit.Type.Send(new SamplePayload());

                Assert.AreEqual(1, received);
            }
            finally
            {
                EasyEventEditorHook.Activity -= failingObserver;
            }
        }

        /// <summary>验证 capability 只声明 state 和只读 get_workbench_snapshot。</summary>
        [Test]
        public void CapabilityDescriptorExcludesLegacyActions()
        {
            string path = Path.Combine(
                Application.dataPath,
                "YokiFrame",
                "Core",
                "Editor",
                "EventKit",
                "Capabilities",
                "capability.json");
            string descriptor = File.ReadAllText(path);

            StringAssert.Contains("\"get_workbench_snapshot\"", descriptor);
            StringAssert.DoesNotContain("fire_event", descriptor);
            StringAssert.DoesNotContain("monitor_start", descriptor);
            StringAssert.DoesNotContain("scan", descriptor);
        }

        /// <summary>从 Core 默认组合中取得 EventKit versioned Provider。</summary>
        private static IYokiFrameVersionedKitInteractionProvider GetEventKitProvider()
        {
            IYokiFrameKitInteractionProvider provider = YokiFrameCoreKitInteractions.CreateDefault()
                .Providers.First(item => string.Equals(item.Kit, "EventKit", StringComparison.Ordinal));
            var versioned = provider as IYokiFrameVersionedKitInteractionProvider;
            Assert.IsNotNull(versioned);
            return versioned;
        }
    }
}
