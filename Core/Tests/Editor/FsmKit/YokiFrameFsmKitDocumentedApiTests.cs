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
        public void Start_快速上手序列_进入一次并处于Running()
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
        public void Start_带参抽象状态_把参数交给OnEnter()
        {
            FSM<PlayerState, int> spawnFsm = new();
            SpawnState spawn = new(spawnFsm, new object());
            spawnFsm.Add(PlayerState.Idle, spawn);
            spawnFsm.Start(PlayerState.Idle, 3);

            Assert.AreEqual(3, spawn.ReceivedLevel, "带参 Start 必须把参数交给 OnEnter(TArgs)");

            spawnFsm.Dispose();
        }

        /// <summary>验证 Get 按标识返回已注册状态，缺失标识得到空引用。</summary>
        [Test]
        public void Get_已注册与缺失_返回实例或空()
        {
            FSM<PlayerState> fsm = new("GameFlow");
            IdleState idle = new(fsm, new object());
            fsm.Add(PlayerState.Idle, idle);

            fsm.Get(PlayerState.Idle, out IState found);
            fsm.Get(PlayerState.Run, out IState missing);

            Assert.AreSame(idle, found);
            Assert.IsNull(missing);
            fsm.Dispose();
        }

    }
}
