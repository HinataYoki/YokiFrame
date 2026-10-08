using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed partial class YokiFrameFsmKitBehaviorTests
    {
        /// <summary>验证创建、加入、启动、切换、释放和清理按固定顺序发布诊断事件。</summary>
        [Test]
        public void Start_创建到释放_按顺序发布诊断事件()
        {
            List<string> events = new();
            Action<IFSM> created = _ => events.Add("created");
            Action<IFSM> disposed = _ => events.Add("disposed");
            Action<IFSM> cleared = _ => events.Add("cleared");
            Action<IFSM, string> started = (_, state) => events.Add("started:" + state);
            Action<IFSM, string, string> changed = (_, from, to) => events.Add("changed:" + from + ">" + to);
            Action<IFSM, string> added = (_, state) => events.Add("added:" + state);
            SubscribeDiagnostics(created, disposed, cleared, started, changed, added);
            try
            {
                FSM<SampleStateId> fsm = new FSM<SampleStateId>("Player");
                fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
                fsm.Add(SampleStateId.Run, new TrackingState("run"));
                fsm.Start();
                fsm.Change(SampleStateId.Run);
                fsm.Dispose();
            }
            finally
            {
                UnsubscribeDiagnostics(created, disposed, cleared, started, changed, added);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "created",
                    "added:Idle",
                    "added:Run",
                    "started:Idle",
                    "changed:Idle>Run",
                    "disposed",
                    "cleared"
                },
                events);
        }

        /// <summary>验证同名 FSM 保持独立 instanceId，并能按该标识精确读取。</summary>
        [Test]
        public void HandleAction_同名两台_按instanceId分别读取()
        {
            FSM<SampleStateId> first = new FSM<SampleStateId>("Duplicate");
            FSM<SampleStateId> second = new FSM<SampleStateId>("Duplicate");
            first.Add(SampleStateId.Idle, new TrackingState("first"));
            second.Add(SampleStateId.Run, new TrackingState("second"));
            FsmKitCommandHandler handler = new FsmKitCommandHandler();

            using (JsonDocument listDocument = JsonDocument.Parse(handler.HandleAction("list_all", "{}")))
            {
                JsonElement fsms = listDocument.RootElement.GetProperty("fsms");
                string secondId = ReadString(fsms[1], "instanceId");
                Assert.AreEqual(2, ReadCount(listDocument.RootElement));
                Assert.AreEqual(2, fsms.GetArrayLength());
                Assert.AreNotEqual(ReadString(fsms[0], "instanceId"), secondId);
                AssertSelectedInstance(handler, secondId);
            }

            GC.KeepAlive(first);
            GC.KeepAlive(second);
        }

        /// <summary>验证 get_state 返回当前状态、加入顺序和进入次数。</summary>
        [Test]
        public void HandleAction_get_state_返回当前状态和加入顺序()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Player");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            fsm.Add(SampleStateId.Run, new TrackingState("run"));
            fsm.Start(SampleStateId.Run);
            FsmKitCommandHandler handler = new FsmKitCommandHandler();

            using (JsonDocument document = JsonDocument.Parse(
                handler.HandleAction("get_state", "{\"fsmName\":\"Player\"}")))
            {
                JsonElement root = document.RootElement;
                JsonElement states = root.GetProperty("states");
                Assert.AreEqual("Player", ReadString(root, "fsmName"));
                Assert.AreEqual("Running", ReadString(root, "machineState"));
                Assert.AreEqual("Run", ReadString(root, "currentState"));
                Assert.AreEqual(2, states.GetArrayLength());
                Assert.AreEqual(0, ReadInt32(states[0], "orderIndex"));
                Assert.AreEqual(1, ReadInt32(states[1], "orderIndex"));
                Assert.IsTrue(ReadBool(states[1], "isCurrent"));
                Assert.AreEqual(0L, ReadInt64(states[0], "entryCount"));
                Assert.AreEqual(1L, ReadInt64(states[1], "entryCount"));
            }

            GC.KeepAlive(fsm);
        }

        /// <summary>验证内建切换历史超过 200 条后只保留最新记录。</summary>
        [Test]
        public void HandleAction_切换超过两百次_历史只保留最新()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("History");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            fsm.Add(SampleStateId.Run, new TrackingState("run"));
            fsm.Start();
            for (var index = 0; index < 205; index++)
            {
                fsm.Change(index % 2 == 0 ? SampleStateId.Run : SampleStateId.Idle);
            }

            using (JsonDocument document = JsonDocument.Parse(
                new FsmKitCommandHandler().HandleAction("get_history", "{\"fsmName\":\"History\"}")))
            {
                JsonElement history = document.RootElement.GetProperty("history");
                Assert.AreEqual(200, ReadCount(document.RootElement));
                Assert.AreEqual(200, history.GetArrayLength());
                Assert.AreEqual("Run", ReadString(history[history.GetArrayLength() - 1], "to"));
            }

            GC.KeepAlive(fsm);
        }

        /// <summary>验证历史窗口填满后，状态进入次数仍继续累计。</summary>
        [Test]
        public void HandleAction_历史窗口已满_进入次数继续累计()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("EntryCount");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            fsm.Add(SampleStateId.Run, new TrackingState("run"));
            fsm.Start();
            for (var index = 0; index < 405; index++)
            {
                fsm.Change(index % 2 == 0 ? SampleStateId.Run : SampleStateId.Idle);
            }

            using (JsonDocument document = JsonDocument.Parse(
                new FsmKitCommandHandler().HandleAction("get_state", "{\"fsmName\":\"EntryCount\"}")))
            {
                JsonElement states = document.RootElement.GetProperty("states");
                Assert.AreEqual(203L, ReadInt64(states[0], "entryCount"));
                Assert.AreEqual(203L, ReadInt64(states[1], "entryCount"));
            }

            GC.KeepAlive(fsm);
        }

        /// <summary>验证状态加入和移除会进入生命周期记录。</summary>
        [Test]
        public void HandleAction_加入后移除_返回新增和移除事件()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Lifecycle");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            fsm.Add(SampleStateId.Run, new TrackingState("run"));
            fsm.Remove(SampleStateId.Run);

            using (JsonDocument document = JsonDocument.Parse(
                new FsmKitCommandHandler().HandleAction("get_state_events", "{\"fsmName\":\"Lifecycle\"}")))
            {
                JsonElement events = document.RootElement.GetProperty("events");
                Assert.AreEqual(3, ReadCount(document.RootElement));
                Assert.AreEqual("added", ReadString(events[0], "eventName"));
                Assert.AreEqual("removed", ReadString(events[2], "eventName"));
                Assert.AreEqual("Run", ReadString(events[2], "state"));
            }

            GC.KeepAlive(fsm);
        }

        /// <summary>验证外部 provider 覆盖历史和状态事件 JSON。</summary>
        [Test]
        public void HandleAction_外部provider_覆盖历史和状态事件()
        {
            FsmKitCommandHandler.HistoryProvider = _ => "{\"history\":[],\"count\":7}";
            FsmKitCommandHandler.StateLifecycleProvider = _ => "{\"events\":[],\"count\":9}";
            FsmKitCommandHandler handler = new FsmKitCommandHandler();

            string history = handler.HandleAction("get_history", "{\"fsmName\":\"Any\"}");
            string stateEvents = handler.HandleAction("get_state_events", "{\"fsmName\":\"Any\"}");

            using (JsonDocument historyDocument = JsonDocument.Parse(history))
            using (JsonDocument eventDocument = JsonDocument.Parse(stateEvents))
            {
                Assert.AreEqual(7, ReadCount(historyDocument.RootElement));
                Assert.AreEqual(9, ReadCount(eventDocument.RootElement));
            }
        }

        /// <summary>验证缺失标识和未知 FSM 分别拒绝查询，而不是返回空对象。</summary>
        [Test]
        public void HandleAction_缺少或未知标识_拒绝查询()
        {
            FsmKitCommandHandler handler = new FsmKitCommandHandler();

            Assert.Throws<ArgumentException>(() => handler.HandleAction("get_state", "{}"));
            Assert.Throws<KeyNotFoundException>(() =>
                handler.HandleAction("get_state", "{\"fsmName\":\"Missing\"}"));
        }

        /// <summary>验证 Runtime 命令入口把 list_all 转换为包含已注册实例的成功终态。</summary>
        [Test]
        public void Handle_list_all_返回成功终态()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Command");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            FsmKitCommandHandler handler = new FsmKitCommandHandler();
            YokiFrameCommandRequest request = new YokiFrameCommandRequest(
                "cli",
                "FsmKit",
                "list_all",
                "{}",
                5000,
                128);

            YokiFrameCommandResult result = handler.Handle(request);

            Assert.IsTrue(result.IsSuccess);
            using (JsonDocument document = JsonDocument.Parse(result.ResultJson))
            {
                Assert.AreEqual(1, ReadCount(document.RootElement));
                Assert.AreEqual("Command", document.RootElement.GetProperty("fsms")[0].GetProperty("name").GetString());
            }

            GC.KeepAlive(fsm);
        }

        /// <summary>验证 Workbench 聚合结果同时包含列表、选中状态、历史和生命周期记录。</summary>
        [Test]
        public void HandleAction_get_workbench_snapshot_包含全部诊断段()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Workbench");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            fsm.Start();

            string json = new FsmKitCommandHandler().HandleAction(
                "get_workbench_snapshot",
                "{\"fsmName\":\"Workbench\"}");

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                Assert.AreEqual("Workbench", root.GetProperty("selected").GetProperty("fsmName").GetString());
                Assert.AreEqual(1, ReadCount(root));
                Assert.AreEqual("Workbench", root.GetProperty("fsms")[0].GetProperty("name").GetString());
                Assert.AreEqual(1, ReadCount(root.GetProperty("history")));
                Assert.AreEqual("Idle", root.GetProperty("history").GetProperty("history")[0].GetProperty("to").GetString());
                Assert.GreaterOrEqual(ReadCount(root.GetProperty("stateEvents")), 1);
            }

            GC.KeepAlive(fsm);
        }

        /// <summary>验证领域变化推进版本，且每个活动实例都有独立 Telemetry payload。</summary>
        [Test]
        public void CreateTelemetry_活动实例_版本随领域变化推进()
        {
            YokiFrameKitInteractionRegistry registry = YokiFrameCoreKitInteractions.CreateDefault();
            IYokiFrameKitInteractionProvider provider = registry.Providers.First(
                item => item.Kit == "FsmKit");
            IYokiFrameVersionedKitInteractionProvider versioned =
                provider as IYokiFrameVersionedKitInteractionProvider;
            IYokiFrameVersionedNamedTelemetryProvider named =
                provider as IYokiFrameVersionedNamedTelemetryProvider;
            Assert.IsNotNull(versioned);
            Assert.IsNotNull(named);
            long initialVersion = versioned.StateVersion;

            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Responsive");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            long addedVersion = versioned.StateVersion;
            string instanceId = named.TelemetryNames[0];
            long instanceVersion = named.GetTelemetryVersion(instanceId);
            string payloadJson = named.CreateTelemetry(instanceId);

            FSM<SampleStateId> unchanged = new FSM<SampleStateId>("Unchanged");
            unchanged.Add(SampleStateId.Run, new TrackingState("run"));
            string unchangedInstanceId = named.TelemetryNames[1];
            long unchangedVersion = named.GetTelemetryVersion(unchangedInstanceId);

            AssertVersionedTelemetry(
                versioned,
                named,
                initialVersion,
                addedVersion,
                instanceId,
                instanceVersion,
                unchangedInstanceId,
                unchangedVersion,
                payloadJson,
                fsm);
            GC.KeepAlive(unchanged);
        }

        /// <summary>确认选中结果就是请求的 instanceId，且当前状态来自第二台状态机。</summary>
        /// <param name="handler">FsmKit 命令入口。</param>
        /// <param name="instanceId">第二台状态机标识。</param>
        private static void AssertSelectedInstance(FsmKitCommandHandler handler, string instanceId)
        {
            using (JsonDocument selected = JsonDocument.Parse(handler.HandleAction(
                "get_state",
                "{\"instanceId\":\"" + instanceId + "\"}")))
            {
                JsonElement root = selected.RootElement;
                Assert.AreEqual(instanceId, ReadString(root, "instanceId"));
                Assert.AreEqual("Run", ReadString(root, "currentState"));
            }
        }

        /// <summary>确认版本只随目标实例变化，且 Telemetry 指向该实例。</summary>
        private static void AssertVersionedTelemetry(
            IYokiFrameVersionedKitInteractionProvider versioned,
            IYokiFrameVersionedNamedTelemetryProvider named,
            long initialVersion,
            long addedVersion,
            string instanceId,
            long instanceVersion,
            string unchangedInstanceId,
            long unchangedVersion,
            string payloadJson,
            FSM<SampleStateId> fsm)
        {
            Assert.Greater(addedVersion, initialVersion);
            Assert.Greater(instanceVersion, 0L);
            Assert.AreEqual(0L, named.GetTelemetryVersion("fsm-missing"));
            using (JsonDocument document = JsonDocument.Parse(payloadJson))
            {
                Assert.AreEqual("Responsive", document.RootElement.GetProperty("fsmName").GetString());
                Assert.AreEqual(instanceId, document.RootElement.GetProperty("instanceId").GetString());
            }

            fsm.Start();
            long startedVersion = versioned.StateVersion;
            long startedInstanceVersion = named.GetTelemetryVersion(instanceId);
            fsm.Suspend();
            Assert.Greater(startedVersion, addedVersion);
            Assert.Greater(versioned.StateVersion, startedVersion);
            Assert.Greater(startedInstanceVersion, instanceVersion);
            Assert.Greater(named.GetTelemetryVersion(instanceId), startedInstanceVersion);
            Assert.AreEqual(unchangedVersion, named.GetTelemetryVersion(unchangedInstanceId));
            GC.KeepAlive(fsm);
        }

        /// <summary>订阅本用例需要的六类诊断回调；StateRemoved 在该流程中不参与断言。</summary>
        private static void SubscribeDiagnostics(
            Action<IFSM> created,
            Action<IFSM> disposed,
            Action<IFSM> cleared,
            Action<IFSM, string> started,
            Action<IFSM, string, string> changed,
            Action<IFSM, string> added)
        {
            FsmEditorHook.OnFsmCreated += created;
            FsmEditorHook.OnFsmDisposed += disposed;
            FsmEditorHook.OnFsmCleared += cleared;
            FsmEditorHook.OnFsmStarted += started;
            FsmEditorHook.OnStateChanged += changed;
            FsmEditorHook.OnStateAdded += added;
        }

        /// <summary>解除本用例订阅，保证异常路径也不会污染全局事件。</summary>
        private static void UnsubscribeDiagnostics(
            Action<IFSM> created,
            Action<IFSM> disposed,
            Action<IFSM> cleared,
            Action<IFSM, string> started,
            Action<IFSM, string, string> changed,
            Action<IFSM, string> added)
        {
            FsmEditorHook.OnFsmCreated -= created;
            FsmEditorHook.OnFsmDisposed -= disposed;
            FsmEditorHook.OnFsmCleared -= cleared;
            FsmEditorHook.OnFsmStarted -= started;
            FsmEditorHook.OnStateChanged -= changed;
            FsmEditorHook.OnStateAdded -= added;
        }

        /// <summary>读取 JSON 字符串字段。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段文本。</returns>
        private static string ReadString(JsonElement element, string propertyName)
        {
            return element.GetProperty(propertyName).GetString();
        }

        /// <summary>读取 JSON 整数字段。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段整数。</returns>
        private static int ReadInt32(JsonElement element, string propertyName)
        {
            int value;
            Assert.IsTrue(element.GetProperty(propertyName).TryGetInt32(out value), propertyName);
            return value;
        }

        /// <summary>读取 JSON 64 位整数字段。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段整数。</returns>
        private static long ReadInt64(JsonElement element, string propertyName)
        {
            long value;
            Assert.IsTrue(element.GetProperty(propertyName).TryGetInt64(out value), propertyName);
            return value;
        }

        /// <summary>读取 JSON 布尔字段。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段值。</returns>
        private static bool ReadBool(JsonElement element, string propertyName)
        {
            return element.GetProperty(propertyName).GetBoolean();
        }
    }
}
