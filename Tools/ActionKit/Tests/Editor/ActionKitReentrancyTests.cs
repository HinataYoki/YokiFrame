using System;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>验证取消、控制故障和容器初始化回调的同 Tick 终态优先级。</summary>
    public sealed class ActionKitReentrancyTests
    {
        private readonly ActionKitTestLogger mLogger = new();

        /// <summary>每个测试前清空静态调度状态。</summary>
        [SetUp]
        public void SetUp()
        {
            ActionKitScheduler.Cleanup();
            mLogger.Clear();
            LogKit.SetLogger(mLogger);
        }

        /// <summary>每个测试后释放尚未终结的动作树。</summary>
        [TearDown]
        public void TearDown()
        {
            try
            {
                ActionKitScheduler.Cleanup();
                mLogger.AssertNoErrors();
            }
            finally { LogKit.ClearLogger(); }
        }

        /// <summary>验证执行中捕获控制钩子异常并 Finish 仍由故障终态优先。</summary>
        [Test]
        public void CaughtControlHookFaultWinsNormalFinishInSameTick()
        {
            CaughtControlFaultAction action = new();
            IActionController controller = action.Start();
            action.Controller = controller;

            Assert.DoesNotThrow(() => ActionKitScheduler.Tick(0f, 0f));

            Assert.IsTrue(controller.IsCompleted);
            Assert.IsTrue(controller.IsFaulted);
            Assert.AreEqual(0, action.FinishCount);
            Assert.IsTrue(action.DeinitCalled);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            Assert.AreEqual(0, ActionKitScheduler.FinishedCount);
            mLogger.AssertErrors("[ActionKit] Action ");
        }

        /// <summary>验证子 OnFinish 登记控制故障后不会继续执行 Sequence 兄弟。</summary>
        [Test]
        public void ControlFaultFromChildFinishStopsSequenceSiblings()
        {
            FinishHookControlFaultAction action = new();
            var siblingCount = 0;
            ISequence sequence = ActionKit.Sequence()
                .Append(action)
                .Callback(() => siblingCount++);
            IActionController controller = sequence.Start();
            action.Controller = controller;

            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsFaulted);
            Assert.IsTrue(action.ControlFaultCaught);
            Assert.AreEqual(0, siblingCount);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            mLogger.AssertErrors("[ActionKit] Action ");
        }

        /// <summary>验证根 Condition 在 predicate 内先取消再满足时不会进入正常完成。</summary>
        [Test]
        public void ConditionCancellationBeforeFinishWinsCompletedPredicate()
        {
            IActionController controller = null;
            var predicateCount = 0;
            var finishCount = 0;
            IAction condition = ActionKit.Condition(() =>
            {
                if (++predicateCount == 1) return false;
                controller.Cancel();
                return true;
            });
            controller = condition.Start(_ => finishCount++);

            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCompleted);
            Assert.IsTrue(controller.IsCancelled);
            Assert.AreEqual(0, finishCount);
            Assert.AreEqual(1, ActionKitScheduler.CancelledCount);
            Assert.AreEqual(0, ActionKitScheduler.FinishedCount);
        }

        /// <summary>验证容器子 OnInit 终结父节点后不会继续初始化后续兄弟。</summary>
        /// <param name="containerKind">待验证的组合容器类型。</param>
        [TestCase(ContainerKind.Sequence)]
        [TestCase(ContainerKind.Parallel)]
        [TestCase(ContainerKind.Repeat)]
        public void ParentFinishDuringChildInitStopsRemainingSiblings(ContainerKind containerKind)
        {
            ISequence container = CreateContainer(containerKind);
            IAction root = container;
            InitCallbackAction first = new(() => root.Finish());
            InitProbeAction sibling = new();
            container.Append(first).Append(sibling);

            IActionController controller = root.Start();

            Assert.IsTrue(controller.IsCompleted);
            Assert.AreEqual(0, sibling.InitCount);
        }

        /// <summary>验证 Repeat 新轮首个 OnInit 取消后不会重启后续兄弟。</summary>
        [Test]
        public void RepeatCancellationDuringRoundResetStopsRemainingSiblings()
        {
            CancelOnSecondInitAction first = new();
            InitProbeAction sibling = new();
            IRepeat repeat = ActionKit.Repeat(2);
            repeat.Append(first).Append(sibling);
            IActionController controller = repeat.Start();
            first.Controller = controller;

            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCancelled);
            Assert.AreEqual(2, first.InitCount);
            Assert.AreEqual(1, sibling.InitCount);
        }

        /// <summary>按测试参数创建共享 ISequence 装配面的组合容器。</summary>
        /// <param name="containerKind">目标容器类型。</param>
        /// <returns>新的空组合容器。</returns>
        private static ISequence CreateContainer(ContainerKind containerKind)
        {
            switch (containerKind)
            {
                case ContainerKind.Sequence: return ActionKit.Sequence();
                case ContainerKind.Parallel: return ActionKit.Parallel();
                case ContainerKind.Repeat: return ActionKit.Repeat(1);
                default: throw new ArgumentOutOfRangeException(nameof(containerKind));
            }
        }

        /// <summary>标识共享初始化终结测试覆盖的组合容器。</summary>
        public enum ContainerKind
        {
            Sequence,
            Parallel,
            Repeat
        }

        /// <summary>在 OnExecute 捕获暂停钩子故障并尝试正常完成的探针。</summary>
        private sealed class CaughtControlFaultAction : ActionBase
        {
            /// <summary>获取测试绑定的活动 controller。</summary>
            internal IActionController Controller { get; set; }

            /// <summary>获取正常完成钩子调用次数。</summary>
            internal int FinishCount { get; private set; }

            /// <summary>获取故障终态是否完成释放。</summary>
            internal bool DeinitCalled { get; private set; }

            /// <summary>触发并捕获暂停钩子异常，再模拟业务请求完成。</summary>
            /// <param name="dt">本测试不使用的时间步长。</param>
            public override void OnExecute(float dt)
            {
                try { Controller.Pause(); }
                catch (InvalidOperationException) { this.Finish(); }
            }

            /// <summary>模拟控制生命周期钩子故障。</summary>
            public override void OnPause() => throw new InvalidOperationException("expected pause failure");

            /// <summary>记录是否错误进入正常完成钩子。</summary>
            public override void OnFinish() => FinishCount++;

            /// <summary>记录完整树已执行故障释放。</summary>
            public override void OnDeinit() => DeinitCalled = true;
        }

        /// <summary>在 OnFinish 内触发并捕获控制故障的 Sequence 子节点。</summary>
        private sealed class FinishHookControlFaultAction : ActionBase
        {
            /// <summary>获取测试绑定的活动 controller。</summary>
            internal IActionController Controller { get; set; }

            /// <summary>获取业务模拟是否捕获了预期控制故障。</summary>
            internal bool ControlFaultCaught { get; private set; }

            /// <summary>首次宿主 Tick 推进时请求正常完成。</summary>
            /// <param name="dt">本测试不使用的时间步长。</param>
            public override void OnExecute(float dt) => this.Finish();

            /// <summary>正常完成钩子内触发控制故障，并模拟业务捕获异常。</summary>
            public override void OnFinish()
            {
                try { Controller.Pause(); }
                catch (InvalidOperationException) { ControlFaultCaught = true; }
            }

            /// <summary>模拟暂停生命周期钩子故障。</summary>
            public override void OnPause() => throw new InvalidOperationException("expected pause failure");
        }

        /// <summary>在初始化时调用指定回调并保持自身未完成的探针。</summary>
        private sealed class InitCallbackAction : ActionBase
        {
            private readonly Action mOnInit;

            /// <summary>创建初始化回调探针。</summary>
            /// <param name="onInit">每次初始化时调用的回调。</param>
            internal InitCallbackAction(Action onInit) => mOnInit = onInit;

            /// <summary>先重置自身状态，再调用可能终结父容器的回调。</summary>
            public override void OnInit()
            {
                base.OnInit();
                mOnInit();
            }
        }

        /// <summary>记录初始化次数并在执行时立即完成的兄弟探针。</summary>
        private sealed class InitProbeAction : ActionBase
        {
            /// <summary>获取初始化调用次数。</summary>
            internal int InitCount { get; private set; }

            /// <summary>记录当前轮次初始化。</summary>
            public override void OnInit()
            {
                base.OnInit();
                InitCount++;
            }

            /// <summary>轮到当前探针时立即完成。</summary>
            public override void OnStart() => this.Finish();
        }

        /// <summary>第二轮初始化时取消 controller，首轮在一次 Tick 后完成。</summary>
        private sealed class CancelOnSecondInitAction : ActionBase
        {
            /// <summary>获取测试绑定的活动 controller。</summary>
            internal IActionController Controller { get; set; }

            /// <summary>获取初始化调用次数。</summary>
            internal int InitCount { get; private set; }

            /// <summary>第二轮初始化时提交取消请求。</summary>
            public override void OnInit()
            {
                base.OnInit();
                InitCount++;
                if (InitCount == 2) Controller.Cancel();
            }

            /// <summary>首个宿主 Tick 推进时完成当前轮。</summary>
            /// <param name="dt">本测试不使用的时间步长。</param>
            public override void OnExecute(float dt) => this.Finish();
        }
    }
}
