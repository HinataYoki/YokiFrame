using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    public sealed partial class YokiFrameFsmKitBehaviorTests
    {
        /// <summary>
        /// 验证七个诊断事件按创建、状态、启动、切换、释放、清理顺序发布。
        /// </summary>
        [Test]
        public void DiagnosticsHookPublishesClosedLifecycleEvents()
        {
            List<string> events = new List<string>();
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
                ((IState)fsm).Dispose();
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

        /// <summary>
        /// 验证同名 FSM 在诊断注册表中保持独立 instanceId，并可用 instanceId 精确读取。
        /// </summary>
        [Test]
        public void CommandRegistryKeepsSameNameInstancesSeparate()
        {
            FSM<SampleStateId> first = new FSM<SampleStateId>("Duplicate");
            FSM<SampleStateId> second = new FSM<SampleStateId>("Duplicate");
            first.Add(SampleStateId.Idle, new TrackingState("first"));
            second.Add(SampleStateId.Run, new TrackingState("second"));
            FsmKitCommandHandler handler = new FsmKitCommandHandler();

            FsmListEnvelope list = JsonUtility.FromJson<FsmListEnvelope>(handler.HandleAction("list_all", "{}"));

            Assert.AreEqual(2, list.count);
            Assert.AreEqual(2, list.fsms.Length);
            Assert.AreNotEqual(list.fsms[0].instanceId, list.fsms[1].instanceId);
            string selectedJson = handler.HandleAction(
                "get_state",
                "{\"instanceId\":\"" + list.fsms[1].instanceId + "\"}");
            FsmStateEnvelope selected = JsonUtility.FromJson<FsmStateEnvelope>(selectedJson);
            Assert.AreEqual(list.fsms[1].instanceId, selected.instanceId);
            Assert.AreEqual("Run", selected.currentState);
        }

        /// <summary>
        /// 验证 get_state 返回当前状态、加入顺序和可递归诊断的状态节点。
        /// </summary>
        [Test]
        public void GetStateReturnsCurrentSelectionAndStateTree()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Player");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            fsm.Add(SampleStateId.Run, new TrackingState("run"));
            fsm.Start(SampleStateId.Run);
            FsmKitCommandHandler handler = new FsmKitCommandHandler();

            FsmStateEnvelope state = JsonUtility.FromJson<FsmStateEnvelope>(
                handler.HandleAction("get_state", "{\"fsmName\":\"Player\"}"));

            Assert.AreEqual("Player", state.fsmName);
            Assert.AreEqual("Running", state.machineState);
            Assert.AreEqual("Run", state.currentState);
            Assert.AreEqual(2, state.states.Length);
            Assert.AreEqual(0, state.states[0].orderIndex);
            Assert.AreEqual(1, state.states[1].orderIndex);
            Assert.IsTrue(state.states[1].isCurrent);
            Assert.AreEqual(0L, state.states[0].entryCount);
            Assert.AreEqual(1L, state.states[1].entryCount);
        }

        /// <summary>
        /// 验证 Core 内建切换历史为有界队列，超过 200 条后只保留最新记录。
        /// </summary>
        [Test]
        public void TransitionHistoryIsBoundedToLatestTwoHundredRecords()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("History");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            fsm.Add(SampleStateId.Run, new TrackingState("run"));
            fsm.Start();
            for (var index = 0; index < 205; index++)
            {
                fsm.Change(index % 2 == 0 ? SampleStateId.Run : SampleStateId.Idle);
            }

            FsmHistoryEnvelope history = JsonUtility.FromJson<FsmHistoryEnvelope>(
                new FsmKitCommandHandler().HandleAction("get_history", "{\"fsmName\":\"History\"}"));

            Assert.AreEqual(200, history.count);
            Assert.AreEqual(200, history.history.Length);
            Assert.AreEqual("Run", history.history[history.history.Length - 1].to);
        }

        /// <summary>
        /// 验证状态进入次数独立于两百条历史窗口持续累计，Workbench 不会在窗口填满后显示停滞。
        /// </summary>
        [Test]
        public void StateEntryCountContinuesAfterTransitionHistoryIsFull()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("EntryCount");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            fsm.Add(SampleStateId.Run, new TrackingState("run"));
            fsm.Start();
            for (var index = 0; index < 405; index++)
            {
                fsm.Change(index % 2 == 0 ? SampleStateId.Run : SampleStateId.Idle);
            }

            FsmStateEnvelope state = JsonUtility.FromJson<FsmStateEnvelope>(
                new FsmKitCommandHandler().HandleAction("get_state", "{\"fsmName\":\"EntryCount\"}"));

            Assert.AreEqual(203L, state.states[0].entryCount);
            Assert.AreEqual(203L, state.states[1].entryCount);
        }

        /// <summary>
        /// 验证状态加入和移除会进入生命周期记录，供 Workbench/AI 调试状态图变化。
        /// </summary>
        [Test]
        public void StateLifecycleCommandReturnsAddAndRemoveEvents()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Lifecycle");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            fsm.Add(SampleStateId.Run, new TrackingState("run"));
            fsm.Remove(SampleStateId.Run);

            FsmStateEventsEnvelope stateEvents = JsonUtility.FromJson<FsmStateEventsEnvelope>(
                new FsmKitCommandHandler().HandleAction("get_state_events", "{\"fsmName\":\"Lifecycle\"}"));

            Assert.AreEqual(3, stateEvents.count);
            Assert.AreEqual("added", stateEvents.events[0].eventName);
            Assert.AreEqual("removed", stateEvents.events[2].eventName);
            Assert.AreEqual("Run", stateEvents.events[2].state);
        }

        /// <summary>
        /// 验证外部 provider 仍可覆盖历史和状态事件 JSON，不影响新版内建记录。
        /// </summary>
        [Test]
        public void OptionalProvidersOverrideHistoryAndStateEvents()
        {
            FsmKitCommandHandler.HistoryProvider = _ => "{\"history\":[],\"count\":7}";
            FsmKitCommandHandler.StateLifecycleProvider = _ => "{\"events\":[],\"count\":9}";
            FsmKitCommandHandler handler = new FsmKitCommandHandler();

            string history = handler.HandleAction("get_history", "{\"fsmName\":\"Any\"}");
            string stateEvents = handler.HandleAction("get_state_events", "{\"fsmName\":\"Any\"}");

            StringAssert.Contains("\"count\":7", history);
            StringAssert.Contains("\"count\":9", stateEvents);
        }

        /// <summary>
        /// 验证缺失查询标识和未知 FSM 产生明确异常，而不是返回含糊空对象。
        /// </summary>
        [Test]
        public void StateQueryRejectsMissingOrUnknownIdentity()
        {
            FsmKitCommandHandler handler = new FsmKitCommandHandler();

            Assert.Throws<ArgumentException>(() => handler.HandleAction("get_state", "{}"));
            Assert.Throws<KeyNotFoundException>(() =>
                handler.HandleAction("get_state", "{\"fsmName\":\"Missing\"}"));
        }

        /// <summary>
        /// 验证新版 Runtime dispatcher 入口把 FsmKit 只读查询转换为成功终态。
        /// </summary>
        [Test]
        public void RuntimeCommandHandlerReturnsTerminalSuccess()
        {
            new FSM<SampleStateId>("Command").Add(SampleStateId.Idle, new TrackingState("idle"));
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
            StringAssert.Contains("\"count\":1", result.ResultJson);
        }

        /// <summary>
        /// 验证 Workbench 聚合 action 同时包含列表、选中状态、历史和生命周期记录。
        /// </summary>
        [Test]
        public void WorkbenchSnapshotContainsAllFsmDiagnosticSections()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Workbench");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));
            fsm.Start();

            string json = new FsmKitCommandHandler().HandleAction(
                "get_workbench_snapshot",
                "{\"fsmName\":\"Workbench\"}");

            StringAssert.Contains("\"fsms\":", json);
            StringAssert.Contains("\"selected\":", json);
            StringAssert.Contains("\"history\":", json);
            StringAssert.Contains("\"stateEvents\":", json);
        }

        /// <summary>
        /// 验证 FsmKit 领域变化推进版本，并为每个活动 instanceId 提供完整命名 Telemetry payload。
        /// </summary>
        [Test]
        public void FsmKitPublishesVersionedTelemetryPerActiveInstance()
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

            Assert.Greater(addedVersion, initialVersion);
            Assert.Greater(instanceVersion, 0L);
            Assert.AreEqual(0L, named.GetTelemetryVersion("fsm-missing"));
            StringAssert.Contains("\"fsmName\":\"Responsive\"", payloadJson);
            StringAssert.Contains("\"instanceId\":\"" + instanceId + "\"", payloadJson);

            fsm.Start();
            long startedVersion = versioned.StateVersion;
            long startedInstanceVersion = named.GetTelemetryVersion(instanceId);
            fsm.Suspend();
            Assert.Greater(startedVersion, addedVersion);
            Assert.Greater(versioned.StateVersion, startedVersion);
            Assert.Greater(startedInstanceVersion, instanceVersion);
            Assert.Greater(named.GetTelemetryVersion(instanceId), startedInstanceVersion);
            Assert.AreEqual(unchangedVersion, named.GetTelemetryVersion(unchangedInstanceId));
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

        [Serializable]
        private sealed class FsmListEnvelope
        {
            public int count;
            public FsmSummary[] fsms = Array.Empty<FsmSummary>();
        }

        [Serializable]
        private sealed class FsmSummary
        {
            public string instanceId = string.Empty;
            public string name = string.Empty;
        }

        [Serializable]
        private sealed class FsmStateEnvelope
        {
            public string fsmName = string.Empty;
            public string instanceId = string.Empty;
            public string machineState = string.Empty;
            public string currentState = string.Empty;
            public FsmStateNode[] states = Array.Empty<FsmStateNode>();
        }

        [Serializable]
        private sealed class FsmStateNode
        {
            public int orderIndex;
            public bool isCurrent;
            public long entryCount;
        }

        [Serializable]
        private sealed class FsmHistoryEnvelope
        {
            public int count;
            public FsmHistoryRecord[] history = Array.Empty<FsmHistoryRecord>();
        }

        [Serializable]
        private sealed class FsmHistoryRecord
        {
            public string to = string.Empty;
        }

        [Serializable]
        private sealed class FsmStateEventsEnvelope
        {
            public int count;
            public FsmStateEventRecord[] events = Array.Empty<FsmStateEventRecord>();
        }

        [Serializable]
        private sealed class FsmStateEventRecord
        {
            public string eventName = string.Empty;
            public string state = string.Empty;
        }
    }
}
