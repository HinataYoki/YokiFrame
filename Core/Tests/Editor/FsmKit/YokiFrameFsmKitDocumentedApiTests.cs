using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 守护 Documentation~/Api/02-Core/FsmKit.md 公开的 API 面。
    /// 本文件的类型与调用直接从该文档的「快速上手」「带启动参数」「实践模式」段逐字摘取：
    /// 一旦文档记录的 API 被删除或改名，本测试程序集将无法编译，从而暴露文档漂移。
    /// </summary>
    public sealed class YokiFrameFsmKitDocumentedApiTests
    {
        /// <summary>文档示例使用的状态枚举。</summary>
        private enum PlayerState { Idle, Run }

        /// <summary>文档「快速上手」段的示例状态。</summary>
        private sealed class IdleState : AbstractState<PlayerState, object>
        {
            /// <summary>按文档签名接收状态机与黑板。</summary>
            /// <param name="fsm">所属状态机。</param>
            /// <param name="blackboard">共享黑板。</param>
            public IdleState(FSM<PlayerState> fsm, object blackboard)
                : base(fsm, blackboard)
            {
            }

            /// <summary>记录文档声明的进入覆写点。</summary>
            protected override void OnEnter() => EnterCount++;

            /// <summary>获取进入次数，用于断言生命周期确实触发。</summary>
            public int EnterCount { get; private set; }
        }

        /// <summary>文档「带启动参数」段的示例状态。</summary>
        private sealed class SpawnState : AbstractState<PlayerState, object, int>
        {
            /// <summary>按文档签名接收状态机与黑板。</summary>
            /// <param name="fsm">所属状态机。</param>
            /// <param name="blackboard">共享黑板。</param>
            public SpawnState(FSM<PlayerState> fsm, object blackboard)
                : base(fsm, blackboard)
            {
            }

            /// <summary>获取文档声明的带参进入回调收到的等级。</summary>
            public int ReceivedLevel { get; private set; } = -1;

            /// <summary>记录文档声明的带参进入覆写点。</summary>
            /// <param name="level">启动参数。</param>
            protected override void OnEnter(int level) => ReceivedLevel = level;
        }

        /// <summary>验证文档「快速上手」段的创建、Add、Start、Update、Dispose 序列可用且语义正确。</summary>
        [Test]
        public void QuickStartSequenceMatchesDocumentation()
        {
            FSM<PlayerState> fsm = new();
            IdleState idle = new(fsm, new object());
            fsm.Add(PlayerState.Idle, idle);
            fsm.Start(PlayerState.Idle);
            fsm.Update();

            Assert.AreEqual(1, idle.EnterCount, "Start 后应进入一次状态");
            Assert.AreEqual(MachineState.Running, fsm.MachineState);
            Assert.AreEqual(PlayerState.Idle, fsm.CurEnum);

            fsm.Dispose();
        }

        /// <summary>验证文档「带启动参数」段的 FSM&lt;TEnum,TArgs&gt; 与 Start(id,args) 可用。</summary>
        [Test]
        public void StartWithArgumentsMatchesDocumentation()
        {
            FSM<PlayerState, int> spawnFsm = new();
            SpawnState spawn = new(spawnFsm, new object());
            spawnFsm.Add(PlayerState.Idle, spawn);
            spawnFsm.Start(PlayerState.Idle, 3);

            Assert.AreEqual(3, spawn.ReceivedLevel, "带参 Start 必须把参数交给 OnEnter(TArgs)");

            spawnFsm.Dispose();
        }

        /// <summary>验证文档「实践模式」段列出的控制与查询成员存在且行为一致。</summary>
        [Test]
        public void PracticePatternMembersMatchDocumentation()
        {
            FSM<PlayerState> fsm = new("GameFlow");
            fsm.Add(PlayerState.Idle, new IdleState(fsm, new object()));
            fsm.Add(PlayerState.Run, new IdleState(fsm, new object()));
            fsm.Start(PlayerState.Idle);

            fsm.Change(PlayerState.Run);
            Assert.AreEqual(PlayerState.Run, fsm.CurEnum);

            fsm.Suspend();
            Assert.AreEqual(MachineState.Suspend, fsm.MachineState);

            fsm.Resume();
            Assert.AreEqual(MachineState.Running, fsm.MachineState);

            fsm.SendMessage(42);
            Assert.NotNull(fsm.CurState);

            // 文档表格记录 Get 为 void + out 参数；不存在时 out 为空值。
            fsm.Get(PlayerState.Idle, out IState found);
            Assert.NotNull(found);
            fsm.Get(PlayerState.Run, out IState missing);
            Assert.NotNull(missing);

            fsm.Remove(PlayerState.Run);
            fsm.End();
            Assert.AreEqual(MachineState.End, fsm.MachineState);

            fsm.Clear();
            fsm.Dispose();
        }

        /// <summary>
        /// 验证文档声明的全部 AbstractState 覆写点签名与基类一致。
        /// 该类型只用于编译期守护：覆写点被改名或改签名时本程序集无法编译。
        /// </summary>
        private sealed class AllOverridePointsState : AbstractState<PlayerState, object>
        {
            /// <summary>创建覆盖全部文档覆写点的状态。</summary>
            /// <param name="fsm">所属状态机。</param>
            /// <param name="blackboard">共享黑板。</param>
            public AllOverridePointsState(FSM<PlayerState> fsm, object blackboard)
                : base(fsm, blackboard)
            {
            }

            /// <summary>覆写进入前条件。</summary>
            /// <returns>始终允许进入。</returns>
            protected override bool OnCondition() => true;

            /// <summary>覆写进入回调。</summary>
            protected override void OnEnter() { }

            /// <summary>覆写挂起回调。</summary>
            protected override void OnSuspend() { }

            /// <summary>覆写恢复回调。</summary>
            protected override void OnResume() { }

            /// <summary>覆写 Update 回调。</summary>
            protected override void OnUpdate() { }

            /// <summary>覆写 FixedUpdate 回调。</summary>
            protected override void OnFixedUpdate() { }

            /// <summary>覆写 CustomUpdate 回调。</summary>
            protected override void OnCustomUpdate() { }

            /// <summary>覆写退出回调。</summary>
            protected override void OnExit() { }

            /// <summary>覆写释放回调。</summary>
            protected override void OnDispose() { }

            /// <summary>覆写强类型消息回调。</summary>
            /// <typeparam name="TMsg">消息类型。</typeparam>
            /// <param name="message">消息内容。</param>
            protected override void OnMessage<TMsg>(TMsg message) { }
        }

        /// <summary>验证文档声明的全部覆写点可被同一状态类型同时实现。</summary>
        [Test]
        public void AllDocumentedOverridePointsAreAvailable()
        {
            FSM<PlayerState> fsm = new();
            fsm.Add(PlayerState.Idle, new AllOverridePointsState(fsm, new object()));
            fsm.Start(PlayerState.Idle);
            fsm.Update();
            fsm.FixedUpdate();
            fsm.CustomUpdate();
            fsm.SendMessage("payload");
            fsm.Dispose();
        }
    }
}
