using System;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>补充普通 FSM 的释放终态、失败收敛和生命周期重入约束。</summary>
    public sealed partial class YokiFrameFsmKitBehaviorTests
    {
        /// <summary>验证 Dispose 幂等，但释放后的实例不能重新执行查询、tick 或状态修改。</summary>
        [Test]
        public void DisposedFsmRejectsReuseAndStaysUnregistered()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Disposable");
            fsm.Add(SampleStateId.Idle, new TrackingState("idle"));

            ((IState)fsm).Dispose();
            Assert.DoesNotThrow(() => ((IState)fsm).Dispose());

            Assert.Throws<ObjectDisposedException>(() => fsm.Get(SampleStateId.Idle, out _));
            Assert.Throws<ObjectDisposedException>(() => fsm.Add(SampleStateId.Run, new TrackingState("run")));
            Assert.Throws<ObjectDisposedException>(() => fsm.Start());
            Assert.Throws<ObjectDisposedException>(() => fsm.Update());
            Assert.Throws<ObjectDisposedException>(() => fsm.Clear());
            StringAssert.Contains(
                "\"count\":0",
                new FsmKitCommandHandler().HandleAction("list_all", "{}"));
        }

        /// <summary>验证进入回调失败会向调用方抛出，并把机器收敛为可重试的 End 状态。</summary>
        [Test]
        public void FailedStartLeavesMachineEndedWithoutSuccessHistory()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("FailedStart");
            ThrowingStartState state = new ThrowingStartState();
            fsm.Add(SampleStateId.Idle, state);

            Assert.Throws<InvalidOperationException>(() => fsm.Start());

            Assert.AreEqual(MachineState.End, fsm.MachineState);
            Assert.AreSame(state, fsm.CurState);
            StringAssert.Contains(
                "\"count\":0",
                new FsmKitCommandHandler().HandleAction("get_history", "{\"fsmName\":\"FailedStart\"}"));
        }

        /// <summary>验证 Start 回调不能嵌套发起 Change，避免一次调用提交两条相互覆盖的转换。</summary>
        [Test]
        public void LifecycleCallbackCannotReenterTransition()
        {
            FSM<SampleStateId> fsm = new FSM<SampleStateId>("Reentrant");
            ReentrantStartState idle = new ReentrantStartState(fsm);
            TrackingState run = new TrackingState("run");
            fsm.Add(SampleStateId.Idle, idle);
            fsm.Add(SampleStateId.Run, run);

            Assert.Throws<InvalidOperationException>(() => fsm.Start());

            Assert.AreEqual(MachineState.End, fsm.MachineState);
            Assert.AreSame(idle, fsm.CurState);
            Assert.AreEqual(0, run.StartCount);
        }

        /// <summary>提供固定抛出进入异常的测试状态。</summary>
        private sealed class ThrowingStartState : IState
        {
            /// <summary>始终允许进入，使测试只覆盖 Start 失败路径。</summary>
            /// <returns>始终返回 true。</returns>
            public bool Condition() => true;

            /// <summary>模拟业务状态进入失败。</summary>
            public void Start() => throw new InvalidOperationException("start failed");

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

        /// <summary>在进入回调中尝试切换同一状态机，用于验证重入守卫。</summary>
        private sealed class ReentrantStartState : IState
        {
            private readonly FSM<SampleStateId> mFsm;

            /// <summary>保存待重入的状态机实例。</summary>
            /// <param name="fsm">进入时尝试修改的状态机。</param>
            internal ReentrantStartState(FSM<SampleStateId> fsm)
            {
                mFsm = fsm;
            }

            /// <summary>始终允许进入。</summary>
            /// <returns>始终返回 true。</returns>
            public bool Condition() => true;

            /// <summary>在进入回调内发起嵌套切换，预期由 FSM 明确拒绝。</summary>
            public void Start() => mFsm.Change(SampleStateId.Run);

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
