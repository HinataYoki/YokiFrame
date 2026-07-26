using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证普通 FSM、带参状态和共享诊断入口的新版行为。
    /// </summary>
    public sealed partial class YokiFrameFsmKitBehaviorTests
    {
        /// <summary>测试使用的稳定状态标识。</summary>
        private enum SampleStateId
        {
            Idle,
            Run,
            Jump
        }

        /// <summary>
        /// 每个测试前清理全局诊断注册表和可选 provider，避免状态机实例跨用例泄漏。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            FsmKitCommandHandler.ClearAll();
            FsmKitCommandHandler.HistoryProvider = null;
            FsmKitCommandHandler.StateLifecycleProvider = null;
        }

        /// <summary>
        /// 每个测试后再次清理全局诊断状态，使失败路径也不会污染后续测试。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            FsmKitCommandHandler.ClearAll();
            FsmKitCommandHandler.HistoryProvider = null;
            FsmKitCommandHandler.StateLifecycleProvider = null;
        }

        /// <summary>
        /// 验证首个状态自动选中，普通切换严格执行 Condition、旧 End、新 Start 的顺序。
        /// </summary>
        [Test]
        public void FsmStartsFirstStateAndChangesInClosedLifecycleOrder()
        {
            List<string> calls = new List<string>();
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Player");
            TrackingState idle = new TrackingState("idle", calls);
            TrackingState run = new TrackingState("run", calls);

            fsm.Add(SampleStateId.Idle, idle);
            fsm.Add(SampleStateId.Run, run);
            fsm.Start();
            fsm.Change(SampleStateId.Run);

            Assert.AreSame(run, fsm.CurState);
            Assert.AreEqual(SampleStateId.Run, fsm.CurEnum);
            Assert.AreEqual(MachineState.Running, fsm.MachineState);
            CollectionAssert.AreEqual(
                new[] { "idle.condition", "idle.start", "run.condition", "idle.end", "run.start" },
                calls);
        }

        /// <summary>
        /// 验证未运行、条件失败、缺失状态和当前状态重复切换均保持 no-op。
        /// </summary>
        [Test]
        public void FsmTransitionGatesLeaveSelectionUntouched()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>();
            TrackingState idle = new TrackingState("idle") { AllowEnter = false };
            TrackingState run = new TrackingState("run") { AllowEnter = false };
            fsm.Add(SampleStateId.Idle, idle);
            fsm.Add(SampleStateId.Run, run);

            fsm.Change(SampleStateId.Run);
            fsm.Start();
            Assert.AreEqual(MachineState.End, fsm.MachineState);
            Assert.AreSame(idle, fsm.CurState);

            idle.AllowEnter = true;
            fsm.Start();
            fsm.Change(SampleStateId.Jump);
            fsm.Change(SampleStateId.Run);
            fsm.Change(SampleStateId.Idle);

            Assert.AreSame(idle, fsm.CurState);
            Assert.AreEqual(1, idle.StartCount);
            Assert.AreEqual(0, idle.EndCount);
            Assert.AreEqual(0, run.StartCount);
        }

        /// <summary>
        /// 验证三类 tick 和消息只在 Running 时转发，Suspend 后立即停止转发。
        /// </summary>
        [Test]
        public void FsmForwardsTicksAndMessagesOnlyWhileRunning()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>();
            TrackingState idle = new TrackingState("idle");
            fsm.Add(SampleStateId.Idle, idle);

            fsm.Update();
            fsm.FixedUpdate();
            fsm.CustomUpdate();
            fsm.SendMessage("before");
            fsm.Start();
            fsm.Update();
            fsm.FixedUpdate();
            fsm.CustomUpdate();
            fsm.SendMessage("running");
            fsm.Suspend();
            fsm.Update();
            fsm.SendMessage("suspended");

            Assert.AreEqual(1, idle.UpdateCount);
            Assert.AreEqual(1, idle.FixedUpdateCount);
            Assert.AreEqual(1, idle.CustomUpdateCount);
            Assert.AreEqual(1, idle.MessageCount);
            Assert.AreEqual("running", idle.LastMessage);
            Assert.AreEqual(1, idle.SuspendCount);
            Assert.AreEqual(MachineState.Suspend, fsm.MachineState);
        }

        /// <summary>
        /// 验证挂起阶段 Start 其他状态会先闭合被挂起状态的生命周期，再启动目标状态。
        /// </summary>
        [Test]
        public void StartingDifferentStateWhileSuspendedClosesSuspendedState()
        {
            List<string> calls = new List<string>();
            FSM<SampleStateId> fsm = new FSM<SampleStateId>();
            TrackingState idle = new TrackingState("idle", calls);
            TrackingState run = new TrackingState("run", calls);
            fsm.Add(SampleStateId.Idle, idle);
            fsm.Add(SampleStateId.Run, run);
            fsm.Start();
            fsm.Suspend();
            calls.Clear();

            fsm.Start(SampleStateId.Run);

            Assert.AreSame(run, fsm.CurState);
            Assert.AreEqual(MachineState.Running, fsm.MachineState);
            Assert.AreEqual(1, idle.EndCount);
            Assert.AreEqual(1, run.StartCount);
            CollectionAssert.AreEqual(new[] { "run.condition", "idle.end", "run.start" }, calls);
        }

        /// <summary>
        /// 验证 Resume 只恢复挂起的机器并继续转发 tick，不重复触发进入逻辑，也不产生第二条 Start 历史。
        /// </summary>
        [Test]
        public void ResumeRestoresSuspendedMachineWithoutRestartingState()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Resume");
            TrackingState idle = new TrackingState("idle");
            fsm.Add(SampleStateId.Idle, idle);

            fsm.Resume();
            Assert.AreEqual(MachineState.End, fsm.MachineState);

            fsm.Start();
            fsm.Suspend();
            fsm.Resume();
            fsm.Update();

            Assert.AreEqual(1, idle.StartCount);
            Assert.AreEqual(1, idle.SuspendCount);
            Assert.AreEqual(1, idle.ResumeCount);
            Assert.AreEqual(1, idle.UpdateCount);
            Assert.AreEqual(MachineState.Running, fsm.MachineState);
            StringAssert.Contains(
                "\"count\":1",
                new FsmKitCommandHandler().HandleAction("get_history", "{\"fsmName\":\"Resume\"}"));
            GC.KeepAlive(fsm);
        }

        /// <summary>
        /// 验证带参启动和切换向匹配状态传参，普通状态则回落无参 Start。
        /// </summary>
        [Test]
        public void FsmPassesArgumentsAndFallsBackToParameterlessState()
        {
            FSM<SampleStateId, int> fsm = new FSM<SampleStateId, int>();
            ArgumentState idle = new ArgumentState();
            TrackingState run = new TrackingState("run");
            fsm.Add(SampleStateId.Idle, idle);
            fsm.Add(SampleStateId.Run, run);

            fsm.Start(42);
            fsm.Change(SampleStateId.Run, 99);

            Assert.AreEqual(42, idle.LastArgument);
            Assert.AreEqual(1, idle.StartCount);
            Assert.AreEqual(1, run.StartCount);
        }

        /// <summary>
        /// 验证运行中替换当前状态会先结束并释放旧状态，再启动满足条件的新状态。
        /// </summary>
        [Test]
        public void ReplacingRunningCurrentStateClosesOldStateAndStartsReplacement()
        {
            List<string> calls = new List<string>();
            FSM<SampleStateId> fsm = new FSM<SampleStateId>();
            TrackingState oldState = new TrackingState("old", calls);
            TrackingState replacement = new TrackingState("new", calls);
            fsm.Add(SampleStateId.Idle, oldState);
            fsm.Start();
            calls.Clear();

            fsm.Add(SampleStateId.Idle, replacement);

            Assert.AreSame(replacement, fsm.CurState);
            Assert.AreEqual(MachineState.Running, fsm.MachineState);
            CollectionAssert.AreEqual(new[] { "old.end", "old.dispose", "new.condition", "new.start" }, calls);
        }

        /// <summary>
        /// 验证 End 保留一致的状态选择以支持无参重启，只有 Clear 才清空选择。
        /// </summary>
        [Test]
        public void EndKeepsSelectionConsistentAndAllowsRestart()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>();
            TrackingState idle = new TrackingState("idle");
            fsm.Add(SampleStateId.Idle, idle);
            fsm.Start();

            fsm.End();
            Assert.AreSame(idle, fsm.CurState);
            Assert.AreEqual(SampleStateId.Idle, fsm.CurEnum);
            Assert.AreEqual(MachineState.End, fsm.MachineState);

            fsm.Start();
            Assert.AreEqual(2, idle.StartCount);
            Assert.AreEqual(1, idle.EndCount);
        }

        /// <summary>
        /// 验证移除当前状态会闭合生命周期、释放一次并把状态机复位为空 End。
        /// </summary>
        [Test]
        public void RemovingCurrentStateResetsMachineAndDisposesOnce()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>();
            TrackingState idle = new TrackingState("idle");
            fsm.Add(SampleStateId.Idle, idle);
            fsm.Start();

            fsm.Remove(SampleStateId.Idle);
            fsm.Remove(SampleStateId.Idle);

            Assert.IsNull(fsm.CurState);
            Assert.AreEqual(-1, fsm.CurrentStateId);
            Assert.AreEqual(MachineState.End, fsm.MachineState);
            Assert.AreEqual(1, idle.EndCount);
            Assert.AreEqual(1, idle.DisposeCount);
        }

        /// <summary>
        /// 验证 Clear 结束当前状态、释放每个状态一次并清空诊断快照。
        /// </summary>
        [Test]
        public void ClearResetsSelectionAndDisposesEveryStateOnce()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>();
            TrackingState idle = new TrackingState("idle");
            TrackingState run = new TrackingState("run");
            fsm.Add(SampleStateId.Idle, idle);
            fsm.Add(SampleStateId.Run, run);
            fsm.Start();

            fsm.Clear();
            fsm.Clear();

            Assert.AreEqual(MachineState.End, fsm.MachineState);
            Assert.IsNull(fsm.CurState);
            Assert.AreEqual(-1, fsm.CurrentStateId);
            Assert.AreEqual(0, fsm.GetAllStates().Count);
            Assert.AreEqual(1, idle.EndCount);
            Assert.AreEqual(1, idle.DisposeCount);
            Assert.AreEqual(1, run.DisposeCount);
        }

        /// <summary>
        /// 验证空状态被立即拒绝，避免在后续 tick 中产生延迟空引用。
        /// </summary>
        [Test]
        public void AddRejectsNullState()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>();

            Assert.Throws<ArgumentNullException>(() => fsm.Add(SampleStateId.Idle, null));
        }

        /// <summary>
        /// 验证 GetAllStates 返回独立字典快照，外部清理不会修改内部状态。
        /// </summary>
        [Test]
        public void StateDictionarySnapshotCannotMutateFsm()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>();
            TrackingState idle = new TrackingState("idle");
            fsm.Add(SampleStateId.Idle, idle);
            var snapshot = fsm.GetAllStates() as IDictionary<int, IState>;

            Assert.IsNotNull(snapshot);
            snapshot.Clear();

            Assert.AreEqual(1, fsm.GetAllStates().Count);
            Assert.AreSame(idle, fsm.CurState);
        }

        /// <summary>
        /// 记录完整 IState 生命周期和调用计数，供普通与带参 FSM 用例复用。
        /// </summary>
        private sealed class TrackingState : IState
        {
            private readonly List<string> mCalls;
            private readonly string mName;

            /// <summary>创建可记录调用顺序的状态。</summary>
            /// <param name="name">写入调用记录的状态名。</param>
            /// <param name="calls">共享调用记录；为空时使用内部列表。</param>
            internal TrackingState(string name, List<string> calls = null)
            {
                mName = name;
                mCalls = calls ?? new List<string>();
            }

            internal bool AllowEnter { get; set; } = true;
            internal int StartCount { get; private set; }
            internal int SuspendCount { get; private set; }
            internal int ResumeCount { get; private set; }
            internal int UpdateCount { get; private set; }
            internal int FixedUpdateCount { get; private set; }
            internal int CustomUpdateCount { get; private set; }
            internal int EndCount { get; private set; }
            internal int DisposeCount { get; private set; }
            internal int MessageCount { get; private set; }
            internal object LastMessage { get; private set; }

            /// <summary>记录并返回进入条件。</summary>
            /// <returns>当前测试配置的允许值。</returns>
            public bool Condition()
            {
                mCalls.Add(mName + ".condition");
                return AllowEnter;
            }

            /// <summary>记录进入。</summary>
            public void Start()
            {
                StartCount++;
                mCalls.Add(mName + ".start");
            }

            /// <summary>记录暂停。</summary>
            public void Suspend()
            {
                SuspendCount++;
                mCalls.Add(mName + ".suspend");
            }

            /// <summary>记录恢复。</summary>
            public void Resume()
            {
                ResumeCount++;
                mCalls.Add(mName + ".resume");
            }

            /// <summary>记录普通更新。</summary>
            public void Update() => UpdateCount++;

            /// <summary>记录固定更新。</summary>
            public void FixedUpdate() => FixedUpdateCount++;

            /// <summary>记录自定义更新。</summary>
            public void CustomUpdate() => CustomUpdateCount++;

            /// <summary>记录结束。</summary>
            public void End()
            {
                EndCount++;
                mCalls.Add(mName + ".end");
            }

            /// <summary>记录释放。</summary>
            public void Dispose()
            {
                DisposeCount++;
                mCalls.Add(mName + ".dispose");
            }

            /// <summary>记录消息。</summary>
            /// <typeparam name="TMsg">消息类型。</typeparam>
            /// <param name="message">消息值。</param>
            public void SendMessage<TMsg>(TMsg message)
            {
                MessageCount++;
                LastMessage = message;
            }
        }

        /// <summary>
        /// 记录强类型进入参数的状态。
        /// </summary>
        private sealed class ArgumentState : IState<int>
        {
            internal int LastArgument { get; private set; }
            internal int StartCount { get; private set; }

            /// <summary>始终允许进入。</summary>
            /// <returns>始终返回 true。</returns>
            public bool Condition() => true;

            /// <summary>记录强类型进入参数。</summary>
            /// <param name="args">进入参数。</param>
            public void Start(int args)
            {
                LastArgument = args;
                StartCount++;
            }

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
