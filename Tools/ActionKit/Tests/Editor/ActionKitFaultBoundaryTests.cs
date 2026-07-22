using System;
using System.Collections;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>验证不可信异常对象和自定义控制钩子不会破坏 ActionKit exactly-once 清理。</summary>
    public sealed class ActionKitFaultBoundaryTests
    {
        private readonly ActionKitTestLogger mLogger = new();

        /// <summary>每个测试前清空调度与诊断状态。</summary>
        [SetUp]
        public void SetUp()
        {
            ActionKitScheduler.Cleanup();
            mLogger.Clear();
            LogKit.SetLogger(mLogger);
        }

        /// <summary>每个测试后释放仍活动的自定义 Action。</summary>
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

        /// <summary>验证 Message 和 ToString 都抛错的异常仍只形成一次 Faulted 终态。</summary>
        [Test]
        public void HostileExceptionCannotInterruptFaultCleanup()
        {
            ExecuteFaultAction action = new();
            IActionController controller = action.Start();

            Assert.DoesNotThrow(() => ActionKitScheduler.Tick(0f, 0f));
            Assert.DoesNotThrow(() => ActionKitScheduler.Tick(0f, 0f));

            Assert.IsTrue(controller.IsFaulted);
            Assert.IsTrue(controller.IsCompleted);
            Assert.AreEqual(ActionStatus.Started, action.ActionState);
            Assert.IsTrue(action.DeinitCalled);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            Assert.AreEqual(0, ActionKitScheduler.ExecutingCount);
            mLogger.AssertErrors("[ActionKit] Action ");
        }

        /// <summary>验证 OnPause 抛错会在下一宿主 Tick 形成 Faulted，而不是永久暂停。</summary>
        [Test]
        public void PauseHookExceptionFaultsOnNextTick()
        {
            AssertControlHookFault(ControlHook.Pause);
        }

        /// <summary>验证 OnResume 抛错会在下一宿主 Tick 清理部分传播的暂停树。</summary>
        [Test]
        public void ResumeHookExceptionFaultsOnNextTick()
        {
            AssertControlHookFault(ControlHook.Resume);
        }

        /// <summary>验证时间源钩子抛错会形成 Faulted，不让外部驱动保持不一致状态。</summary>
        [Test]
        public void UpdateModeHookExceptionFaultsOnNextTick()
        {
            AssertControlHookFault(ControlHook.UpdateMode);
        }

        /// <summary>验证正常完成后的 OnDeinit 异常会改记 Faulted，并继续释放后续兄弟节点。</summary>
        [Test]
        public void DeinitExceptionOverridesCompletedAndContinuesTreeCleanup()
        {
            DeinitProbeAction sibling = new();
            ISequence sequence = ActionKit.Sequence()
                .Append(new DeinitFaultAction())
                .Append(sibling);

            IActionController controller = sequence.Start();

            Assert.IsTrue(controller.IsFaulted);
            Assert.IsTrue(sibling.DeinitCalled);
            Assert.AreEqual(0, ActionKitScheduler.FinishedCount);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            Assert.AreEqual(0, ActionKitScheduler.ExecutingCount);
            mLogger.AssertErrors(
                "[ActionKit] OnDeinit failed: ",
                "[ActionKit] Action ");
        }

        /// <summary>验证 Coroutine Dispose 异常会形成 Faulted，而不是伪装成取消成功。</summary>
        [Test]
        public void CoroutineDisposeExceptionOverridesCancelledTerminal()
        {
            IActionController controller = ActionKit.Coroutine(new ThrowingDisposeEnumerator()).Start();

            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsFaulted);
            Assert.IsFalse(controller.IsCancelled);
            Assert.AreEqual(0, ActionKitScheduler.CancelledCount);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            mLogger.AssertErrors(
                "[ActionKit] IEnumerator.Dispose failed: ",
                "[ActionKit] OnDeinit failed: ",
                "[ActionKit] Action ");
        }

        /// <summary>验证 Repeat 提前结束活动 Coroutine 时先释放旧枚举器，再创建下一轮实例。</summary>
        [Test]
        public void RepeatExternalFinishDisposesCoroutineBeforeNextRound()
        {
            TrackingEnumerator first = new();
            TrackingEnumerator second = new();
            var factoryCount = 0;
            IAction coroutine = ActionKit.Coroutine(() => ++factoryCount == 1 ? first : second);
            IRepeat repeat = ActionKit.Repeat(2);
            repeat.Append(coroutine);
            IActionController controller = repeat.Start();

            coroutine.Finish();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(first.Disposed);
            Assert.AreEqual(1, factoryCount);
            ActionKitScheduler.Tick(0f, 0f);
            Assert.AreEqual(2, factoryCount);
            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);
        }

        /// <summary>执行指定控制钩子并验证异常登记、终态与释放只发生一次。</summary>
        /// <param name="faultHook">需要抛出测试异常的控制钩子。</param>
        private void AssertControlHookFault(ControlHook faultHook)
        {
            ControlHookFaultAction action = new(faultHook);
            IActionController controller = action.Start();

            if (faultHook == ControlHook.Resume) controller.Pause();
            Assert.Throws<InvalidOperationException>(() => InvokeFaultingControl(controller, faultHook));
            Assert.DoesNotThrow(() => ActionKitScheduler.Tick(0f, 0f));
            Assert.DoesNotThrow(() => ActionKitScheduler.Tick(0f, 0f));

            Assert.IsTrue(controller.IsFaulted);
            Assert.IsTrue(action.DeinitCalled);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            Assert.AreEqual(0, ActionKitScheduler.ExecutingCount);
            mLogger.AssertErrors("[ActionKit] Action ");
        }

        /// <summary>调用当前测试选择的控制入口。</summary>
        /// <param name="controller">活动根 controller。</param>
        /// <param name="faultHook">需要触发的控制钩子。</param>
        private static void InvokeFaultingControl(IActionController controller, ControlHook faultHook)
        {
            switch (faultHook)
            {
                case ControlHook.Pause:
                    controller.Pause();
                    break;
                case ControlHook.Resume:
                    controller.Resume();
                    break;
                case ControlHook.UpdateMode:
                    controller.UpdateMode = ActionUpdateModes.UnscaledDeltaTime;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(faultHook));
            }
        }

        /// <summary>标识测试需要触发的控制生命周期钩子。</summary>
        private enum ControlHook
        {
            Pause,
            Resume,
            UpdateMode
        }

        /// <summary>在首次 OnExecute 抛出无法安全格式化的异常。</summary>
        private sealed class ExecuteFaultAction : ActionBase
        {
            /// <summary>获取故障清理是否执行了 OnDeinit。</summary>
            internal bool DeinitCalled { get; private set; }

            /// <summary>抛出自定义异常，覆盖诊断边界。</summary>
            public override void OnExecute(float dt) => throw new HostileException();

            /// <summary>记录动作树已完成故障释放。</summary>
            public override void OnDeinit() => DeinitCalled = true;
        }

        /// <summary>在指定控制钩子抛出异常，并记录最终释放。</summary>
        private sealed class ControlHookFaultAction : ActionBase
        {
            private readonly ControlHook mFaultHook;

            /// <summary>创建指定故障钩子的探针。</summary>
            /// <param name="faultHook">需要抛错的控制钩子。</param>
            internal ControlHookFaultAction(ControlHook faultHook) => mFaultHook = faultHook;

            /// <summary>获取故障清理是否执行了 OnDeinit。</summary>
            internal bool DeinitCalled { get; private set; }

            /// <summary>按测试配置在暂停钩子抛错。</summary>
            public override void OnPause() => ThrowWhen(ControlHook.Pause);

            /// <summary>按测试配置在恢复钩子抛错。</summary>
            public override void OnResume() => ThrowWhen(ControlHook.Resume);

            /// <summary>按测试配置在时间源变化钩子抛错。</summary>
            public override void OnUpdateModeChanged(ActionUpdateModes updateMode) =>
                ThrowWhen(ControlHook.UpdateMode);

            /// <summary>记录动作树已完成故障释放。</summary>
            public override void OnDeinit() => DeinitCalled = true;

            /// <summary>当前钩子匹配测试配置时抛出固定异常。</summary>
            /// <param name="currentHook">正在执行的控制钩子。</param>
            private void ThrowWhen(ControlHook currentHook)
            {
                if (mFaultHook == currentHook)
                    throw new InvalidOperationException("expected control hook failure");
            }
        }

        /// <summary>模拟 Message 与 ToString 都不可信的第三方异常实现。</summary>
        private sealed class HostileException : Exception
        {
            /// <summary>验证历史记录不会直接信任异常 Message。</summary>
            public override string Message => throw new InvalidOperationException("message unavailable");

            /// <summary>验证错误日志不会直接信任异常 ToString。</summary>
            public override string ToString() => throw new InvalidOperationException("text unavailable");
        }

        /// <summary>同步完成后在 OnDeinit 抛出不可信异常。</summary>
        private sealed class DeinitFaultAction : ActionBase
        {
            /// <summary>首次推进立即完成，使异常只发生在终态清理阶段。</summary>
            public override void OnStart() => this.Finish();

            /// <summary>模拟第三方清理钩子失败。</summary>
            public override void OnDeinit() => throw new HostileException();
        }

        /// <summary>同步完成并记录兄弟节点是否仍得到释放。</summary>
        private sealed class DeinitProbeAction : ActionBase
        {
            /// <summary>获取 OnDeinit 是否被调用。</summary>
            internal bool DeinitCalled { get; private set; }

            /// <summary>首次推进立即完成。</summary>
            public override void OnStart() => this.Finish();

            /// <summary>记录完整树清理没有被前一个兄弟异常中断。</summary>
            public override void OnDeinit() => DeinitCalled = true;
        }

        /// <summary>保持运行并在 Dispose 时抛错的枚举器。</summary>
        private sealed class ThrowingDisposeEnumerator : IEnumerator, IDisposable
        {
            /// <summary>返回空 yield 值。</summary>
            public object Current => null;

            /// <summary>保持 Coroutine 活动直到 controller 取消。</summary>
            public bool MoveNext() => true;

            /// <summary>测试枚举器不支持重置。</summary>
            public void Reset() => throw new NotSupportedException();

            /// <summary>模拟清理阶段 Dispose 失败。</summary>
            public void Dispose() => throw new HostileException();
        }

        /// <summary>保持运行且可观察 Dispose 的 Repeat 枚举器。</summary>
        private sealed class TrackingEnumerator : IEnumerator, IDisposable
        {
            /// <summary>获取上一轮资源是否已经释放。</summary>
            internal bool Disposed { get; private set; }

            /// <summary>返回空 yield 值。</summary>
            public object Current => null;

            /// <summary>保持当前轮活动，等待测试外部 Finish。</summary>
            public bool MoveNext() => true;

            /// <summary>测试枚举器不支持重置。</summary>
            public void Reset() => throw new NotSupportedException();

            /// <summary>记录 Repeat 重启前已关闭上一轮资源。</summary>
            public void Dispose() => Disposed = true;
        }
    }
}
