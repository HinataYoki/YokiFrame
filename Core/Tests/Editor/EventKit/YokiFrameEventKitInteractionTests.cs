using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using JsonDocument = YokiFrame.Json.JsonDocument;
using JsonElement = YokiFrame.Json.JsonElement;

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

        /// <summary>验证 state 命令返回当前监听器与近期活动，并且与 CreateSnapshot 相同。</summary>
        [Test]
        public void Handle_注册并发送_返回监听器和近期活动()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetEventKitProvider();
            EventKit.Type.Register<SamplePayload>(_ => { });
            EventKit.Type.Send(new SamplePayload());

            string payloadJson = provider.CreateSnapshot("state");
            YokiFrameCommandResult commandResult = provider.Handle(new YokiFrameCommandRequest(
                "eventkit-test",
                "EventKit",
                "get_workbench_snapshot",
                "{}",
                1000,
                64));

            Assert.AreEqual(2, provider.Commands.Count);
            Assert.AreEqual("get_workbench_snapshot", provider.Commands[0].Action);
            Assert.AreEqual("set_tracking", provider.Commands[1].Action);
            Assert.IsTrue(commandResult.IsSuccess);
            Assert.AreEqual(payloadJson, commandResult.ResultJson);
            using (JsonDocument document = JsonDocument.Parse(commandResult.ResultJson))
            {
                JsonElement events = document.RootElement.GetProperty("events");
                Assert.AreEqual(1, events.GetArrayLength());
                JsonElement registration = events[0];
                Assert.AreEqual(typeof(SamplePayload).FullName, registration.GetProperty("payloadType").GetString());
                Assert.AreEqual(1, ReadJsonInt32(registration, "handlerCount"));
                Assert.AreEqual(2L, ReadJsonInt64(registration, "lastSequence"));
                JsonElement activities = document.RootElement.GetProperty("recentEvents").GetProperty("events");
                Assert.IsTrue(HasJsonKind(activities, "register"));
                Assert.IsTrue(HasJsonKind(activities, "send"));
            }
        }

        /// <summary>验证没有监听器的纯发送事件仍会形成可见行，不依赖静态代码扫描。</summary>
        [Test]
        public void SendWithoutRegistrationStillAppearsInSnapshot()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetEventKitProvider();

            EventKit.Type.Send(new SamplePayload());

            EventKitWorkbenchSnapshot snapshot = EventKitSnapshotBuilder.Create();
            EventKitRegistrationSnapshot registration = FindRegistration(snapshot, typeof(SamplePayload).FullName);
            Assert.IsNotNull(registration);
            Assert.AreEqual(0, registration.HandlerCount);
            Assert.AreEqual(1L, registration.LastSequence);
            Assert.AreEqual(1L, provider.StateVersion);
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

            EventKitWorkbenchSnapshot snapshot = EventKitSnapshotBuilder.Create();

            Assert.IsNotNull(FindRegistration(snapshot, EventKitTypeIdentity.Format(typeof(CollisionScopeA.Payload))));
            Assert.IsNotNull(FindRegistration(snapshot, EventKitTypeIdentity.Format(typeof(CollisionScopeB.Payload))));
            Assert.IsNotNull(FindRegistration(snapshot, EventKitTypeIdentity.FormatEnumEventKey(
                new EnumEventKey(typeof(CollisionScopeA.Signal), (ulong)CollisionScopeA.Signal.Ready))));
            Assert.IsNotNull(FindRegistration(snapshot, EventKitTypeIdentity.FormatEnumEventKey(
                new EnumEventKey(typeof(CollisionScopeB.Signal), (ulong)CollisionScopeB.Signal.Ready))));
            Assert.AreEqual(4, snapshot.Registrations.Count);
            Assert.Greater(provider.StateVersion, 0L);
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

            EventKitWorkbenchSnapshot snapshot = EventKitSnapshotBuilder.Create();
            EventKitRegistrationSnapshot registration = FindRegistration(snapshot, typeof(SamplePayload).FullName);
            Assert.IsTrue(HasActivity(snapshot, "clear", "*"));
            Assert.IsNotNull(registration);
            Assert.AreEqual(2L, registration.LastSequence);
            Assert.AreEqual(2L, provider.StateVersion);
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

        /// <summary>验证 capability 命令 action 与 Provider 目录一致。</summary>
        [Test]
        public void CapabilityDescriptor_与Provider目录_命令一致()
        {
            string path = Path.Combine(
                Application.dataPath,
                "YokiFrame",
                "Core",
                "Editor",
                "EventKit",
                "Capabilities",
                "capability.json");
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                CollectionAssert.AreEquivalent(
                    GetEventKitProvider().Commands.Select(static command => command.Action).ToArray(),
                    ReadActions(document.RootElement.GetProperty("kit").GetProperty("commands")));
            }
        }

        /// <summary>
        /// 按事件键查找工作台注册行。
        /// </summary>
        /// <param name="snapshot">当前工作台快照。</param>
        /// <param name="eventKey">完整事件键。</param>
        /// <returns>匹配行；不存在时返回 null。</returns>
        private static EventKitRegistrationSnapshot FindRegistration(EventKitWorkbenchSnapshot snapshot, string eventKey)
        {
            for (var index = 0; index < snapshot.Registrations.Count; index++)
            {
                EventKitRegistrationSnapshot registration = snapshot.Registrations[index];
                if (registration.EventKey == eventKey || registration.PayloadType == eventKey)
                {
                    return registration;
                }
            }

            return null;
        }

        /// <summary>
        /// 判断活动历史是否包含指定种类。
        /// </summary>
        /// <param name="snapshot">当前工作台快照。</param>
        /// <param name="kind">活动种类。</param>
        /// <param name="eventKey">可选事件键；为空时只匹配种类。</param>
        /// <returns>存在匹配活动时返回 true。</returns>
        private static bool HasActivity(EventKitWorkbenchSnapshot snapshot, string kind, string eventKey = null)
        {
            for (var index = 0; index < snapshot.Activities.Length; index++)
            {
                EventKitActivityRecord activity = snapshot.Activities[index];
                if (activity.Kind != kind)
                {
                    continue;
                }

                if (eventKey == null || activity.EventKey == eventKey)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>读取 capability 命令 action。</summary>
        /// <param name="commands">命令数组。</param>
        /// <returns>action 列表。</returns>
        private static string[] ReadActions(JsonElement commands)
        {
            var actions = new string[commands.GetArrayLength()];
            for (var index = 0; index < actions.Length; index++)
            {
                actions[index] = commands[index].GetProperty("action").GetString();
            }

            return actions;
        }

        /// <summary>判断活动数组是否包含指定种类。</summary>
        /// <param name="events">recentEvents.events。</param>
        /// <param name="kind">活动种类。</param>
        /// <returns>存在匹配种类时返回 true。</returns>
        private static bool HasJsonKind(JsonElement events, string kind)
        {
            int length = events.GetArrayLength();
            for (var index = 0; index < length; index++)
            {
                if (events[index].GetProperty("kind").GetString() == kind)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>读取 JSON 整数。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>整数值。</returns>
        private static int ReadJsonInt32(JsonElement element, string propertyName)
        {
            int value;
            Assert.IsTrue(element.GetProperty(propertyName).TryGetInt32(out value), propertyName);
            return value;
        }

        /// <summary>读取 JSON 64 位整数。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>整数值。</returns>
        private static long ReadJsonInt64(JsonElement element, string propertyName)
        {
            long value;
            Assert.IsTrue(element.GetProperty(propertyName).TryGetInt64(out value), propertyName);
            return value;
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
