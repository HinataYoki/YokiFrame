using System;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证 ActionKit 根动作、控制器和 exactly-once 终态语义。
    /// </summary>
    public sealed class ActionKitLifecycleTests
    {
        private readonly ActionKitTestLogger mLogger = new();

        /// <summary>
        /// 每个测试前清空静态调度状态，避免执行计数和活动动作互相污染。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            ActionKitScheduler.Cleanup();
            ActionStackTraceService.Enabled = false;
            mLogger.Clear();
            LogKit.SetLogger(mLogger);
        }

        /// <summary>
        /// 每个测试后完整清理动作树和诊断状态，验证清理入口保持幂等。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            try
            {
                ActionKitScheduler.Cleanup();
                ActionStackTraceService.Enabled = false;
                mLogger.AssertNoErrors();
            }
            finally { LogKit.ClearLogger(); }
        }

        /// <summary>
        /// 验证 Start 会同步推进 dt=0，立即回调和正常完成回调各执行一次。
        /// </summary>
        [Test]
        public void StartImmediateCallbackCompletesSynchronouslyExactlyOnce()
        {
            var actionCount = 0;
            var finishCount = 0;

            IActionController controller = ActionKit.Callback(() => actionCount++)
                .Start(_ => finishCount++);

            Assert.AreEqual(1, actionCount);
            Assert.AreEqual(1, finishCount);
            Assert.AreEqual(1, ActionKitScheduler.FinishedCount);
            Assert.IsFalse(controller.IsCancelled);

            ActionKitScheduler.Tick(0.1f, 0.1f);
            Assert.AreEqual(1, actionCount);
            Assert.AreEqual(1, finishCount);
        }

        /// <summary>
        /// 验证正时长 Delay 只在累计时间达到阈值时执行完成回调。
        /// </summary>
        [Test]
        public void DelayCompletesAfterAccumulatedScaledTime()
        {
            var callbackCount = 0;
            IAction action = ActionKit.Delay(1f, () => callbackCount++);
            action.Start();

            ActionKitScheduler.Tick(0.4f, 4f);
            Assert.AreEqual(ActionStatus.Started, action.ActionState);
            Assert.AreEqual(0, callbackCount);

            ActionKitScheduler.Tick(0.6f, 6f);
            Assert.AreEqual(ActionStatus.Finished, action.ActionState);
            Assert.AreEqual(1, callbackCount);
        }

        /// <summary>
        /// 验证控制器选择 UnscaledDeltaTime 后只消费宿主非缩放时间。
        /// </summary>
        [Test]
        public void ControllerUsesUnscaledTimeWhenRequested()
        {
            IAction action = ActionKit.Delay(1f, null);
            IActionController controller = action.Start();
            controller.UpdateMode = ActionUpdateModes.UnscaledDeltaTime;

            ActionKitScheduler.Tick(0f, 1f);

            Assert.AreEqual(ActionStatus.Finished, action.ActionState);
        }

        /// <summary>
        /// 验证暂停期间不推进动作，恢复后继续使用后续时间步长。
        /// </summary>
        [Test]
        public void PauseAndResumeGateSchedulerProgress()
        {
            IAction action = ActionKit.Delay(1f, null);
            IActionController controller = action.Start();

            controller.Pause();
            ActionKitScheduler.Tick(10f, 10f);
            Assert.AreEqual(ActionStatus.Started, action.ActionState);

            controller.Resume();
            ActionKitScheduler.Tick(1f, 1f);
            Assert.AreEqual(ActionStatus.Finished, action.ActionState);
        }

        /// <summary>
        /// 验证取消只触发清理，不触发 Action 或 controller 的正常完成回调。
        /// </summary>
        [Test]
        public void CancelDoesNotInvokeNormalCompletionCallbacks()
        {
            var actionFinishCount = 0;
            var controllerFinishCount = 0;
            IAction action = ActionKit.Delay(10f, () => actionFinishCount++);
            IActionController controller = action.Start(_ => controllerFinishCount++);

            controller.Cancel();
            ActionKitScheduler.Tick(0.1f, 0.1f);

            Assert.IsTrue(controller.IsCancelled);
            Assert.AreEqual(ActionStatus.Started, action.ActionState);
            Assert.AreEqual(0, actionFinishCount);
            Assert.AreEqual(0, controllerFinishCount);
            Assert.AreEqual(1, ActionKitScheduler.CancelledCount);
        }

        /// <summary>
        /// 验证宿主线程取消尚未进入执行列表的根动作时，会在 Cancel 返回前终结并释放动作树。
        /// </summary>
        [Test]
        public void CancelPreparedActionReleasesSynchronouslyOnHostThread()
        {
            var finishCount = 0;
            CancellationProbeAction action = new(static () => { });
            IActionController controller = action.Start(_ => finishCount++);

            controller.Cancel();

            Assert.IsTrue(controller.IsCompleted);
            Assert.IsTrue(controller.IsCancelled);
            Assert.IsNull(controller.Action);
            Assert.IsTrue(action.DeinitCalled);
            Assert.AreEqual(0, finishCount);
            Assert.AreEqual(0, ActionKitScheduler.ExecutingCount);
            Assert.AreEqual(1, ActionKitScheduler.CancelledCount);
        }

        /// <summary>
        /// 验证旧 controller handle 在原动作结束后不能暂停或取消后续动作。
        /// </summary>
        [Test]
        public void CompletedControllerHandleCannotControlLaterAction()
        {
            IActionController oldController = ActionKit.Delay(0f, null).Start();
            IAction nextAction = ActionKit.Delay(10f, null);
            IActionController nextController = nextAction.Start();

            oldController.Pause();
            oldController.Cancel();
            ActionKitScheduler.Tick(0.5f, 0.5f);

            Assert.IsFalse(nextController.Paused);
            Assert.IsFalse(nextController.IsCancelled);
            Assert.AreEqual(ActionStatus.Started, nextAction.ActionState);
        }

        /// <summary>
        /// 验证生命周期异常只形成一次 Faulted 终态，不逐帧重试或调用正常完成回调。
        /// </summary>
        [Test]
        public void CallbackExceptionFaultsOnceWithoutRetry()
        {
            var invocationCount = 0;
            var finishCount = 0;
            IAction action = ActionKit.Callback(() =>
            {
                invocationCount++;
                throw new InvalidOperationException("expected");
            });

            action.Start(_ => finishCount++);
            ActionKitScheduler.Tick(0.1f, 0.1f);
            ActionKitScheduler.Tick(0.1f, 0.1f);

            Assert.AreEqual(1, invocationCount);
            Assert.AreEqual(0, finishCount);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            Assert.AreEqual(0, ActionKitScheduler.ExecutingCount);
            mLogger.AssertErrors("[ActionKit] Action ");
        }

        /// <summary>
        /// 验证自定义 Action 在首次执行前一定收到 OnInit，并自动获得非零运行 ID。
        /// </summary>
        [Test]
        public void CustomActionIsInitializedBeforeStartAndGetsRuntimeId()
        {
            ProbeAction action = new();

            action.Start();

            Assert.AreEqual(1, action.InitCount);
            Assert.AreEqual(1, action.StartCount);
            Assert.AreNotEqual(0ul, action.ActionID);
        }

        /// <summary>
        /// 验证活动 Action 的堆栈记录在正常完成后自动移除。
        /// </summary>
        [Test]
        public void StackTraceIsRemovedWhenActionTerminates()
        {
            ActionStackTraceService.Enabled = true;
            IAction action = ActionKit.Delay(1f, null);
            action.Start();
            Assert.AreEqual(1, ActionStackTraceService.Count);

            ActionKitScheduler.Tick(1f, 1f);

            Assert.AreEqual(0, ActionStackTraceService.Count);
        }

        /// <summary>
        /// 验证外部 Finish 标记不受暂停门控，已完成动作仍会在下一 Tick 正常终结。
        /// </summary>
        [Test]
        public void PausedActionMarkedFinishedStillFinalizes()
        {
            var finishCount = 0;
            IAction action = ActionKit.Delay(10f, () => finishCount++);
            IActionController controller = action.Start();
            controller.Pause();
            action.Finish();

            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCompleted);
            Assert.IsFalse(controller.IsCancelled);
            Assert.AreEqual(1, finishCount);
        }

        /// <summary>
        /// 验证 Action 在 OnExecute 内请求取消后，会在同一个宿主 Tick 清理而不是多运行一帧。
        /// </summary>
        [Test]
        public void CancellationRequestedDuringExecuteFinalizesInSameTick()
        {
            IActionController controller = null;
            CancellationProbeAction action = new(() => controller.Cancel());
            controller = action.Start();

            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCancelled);
            Assert.IsTrue(controller.IsCompleted);
            Assert.IsTrue(action.DeinitCalled);
            Assert.AreEqual(1, ActionKitScheduler.CancelledCount);
        }

        /// <summary>
        /// 验证动作已进入 OnFinish 后的取消请求不把已发生的正常完成改写成取消终态。
        /// </summary>
        [Test]
        public void NormalCompletionWinsCancellationRequestedFromOnFinish()
        {
            IActionController controller = null;
            IAction action = ActionKit.Delay(1f, () => controller.Cancel());
            controller = action.Start();

            ActionKitScheduler.Tick(1f, 1f);

            Assert.IsTrue(controller.IsCompleted);
            Assert.IsFalse(controller.IsCancelled);
            Assert.AreEqual(1, ActionKitScheduler.FinishedCount);
            Assert.AreEqual(0, ActionKitScheduler.CancelledCount);
        }

        /// <summary>
        /// 验证手动重复 Update 已完成 Action 时，正常完成钩子仍保持 exactly-once。
        /// </summary>
        [Test]
        public void ManualUpdateInvokesFinishExactlyOnce()
        {
            ManualFinishProbeAction action = new();

            Assert.IsTrue(action.Update(0f));
            Assert.IsTrue(action.Update(0f));

            Assert.AreEqual(1, action.FinishCount);
        }

        /// <summary>
        /// 验证直接实现 IAction 的自定义类型也只初始化一次，不会在每个 Tick 被重复 OnInit。
        /// </summary>
        [Test]
        public void PlainIActionInitializesOnceAcrossTicks()
        {
            PlainAction action = new();
            IActionController controller = action.Start();

            ActionKitScheduler.Tick(0f, 0f);
            ActionKitScheduler.Tick(0f, 0f);

            Assert.AreEqual(1, action.InitCount);
            Assert.AreEqual(1, action.StartCount);
            Assert.AreEqual(2, action.ExecuteCount);
            Assert.AreEqual(1, action.FinishCount);
            Assert.IsTrue(controller.IsCompleted);
        }

        /// <summary>
        /// 用最小自定义 Action 记录生命周期调用顺序，不依赖内置池化实现。
        /// </summary>
        private sealed class ProbeAction : ActionBase
        {
            /// <summary>获取初始化调用次数。</summary>
            public int InitCount { get; private set; }

            /// <summary>获取开始调用次数。</summary>
            public int StartCount { get; private set; }

            /// <summary>记录调度器在首次执行前完成了初始化。</summary>
            public override void OnInit()
            {
                base.OnInit();
                InitCount++;
            }

            /// <summary>记录开始并立即完成，便于验证同步 Start 契约。</summary>
            public override void OnStart()
            {
                StartCount++;
                this.Finish();
            }

            /// <summary>自定义动作没有外部资源，释放时只依赖基类终态标记。</summary>
            public override void OnDeinit() { }
        }

        /// <summary>在 OnExecute 中执行测试回调，并记录取消路径是否完成释放。</summary>
        private sealed class CancellationProbeAction : ActionBase
        {
            private readonly Action mExecute;

            /// <summary>创建使用指定执行回调的探针。</summary>
            /// <param name="execute">首次 Tick 执行的测试回调。</param>
            internal CancellationProbeAction(Action execute) => mExecute = execute;

            /// <summary>获取取消终态是否调用了释放钩子。</summary>
            internal bool DeinitCalled { get; private set; }

            /// <summary>执行测试回调但不主动完成，让取消请求决定本 Tick 终态。</summary>
            public override void OnExecute(float dt) => mExecute();

            /// <summary>记录调度器完成了取消清理。</summary>
            public override void OnDeinit() => DeinitCalled = true;
        }

        /// <summary>首次启动立即完成，并记录 OnFinish 调用次数。</summary>
        private sealed class ManualFinishProbeAction : ActionBase
        {
            /// <summary>获取当前轮次正常完成钩子调用次数。</summary>
            internal int FinishCount { get; private set; }

            /// <summary>首次推进立即标记完成。</summary>
            public override void OnStart() => this.Finish();

            /// <summary>记录正常完成钩子；重复手动 Update 不应再次进入。</summary>
            public override void OnFinish() => FinishCount++;
        }

        /// <summary>不继承 ActionBase 的最小 IAction，用于验证外部生命周期状态。</summary>
        private sealed class PlainAction : IAction
        {
            /// <summary>创建具有固定非零测试 ID 的自定义 Action。</summary>
            internal PlainAction() => ActionID = 1UL << 62;

            /// <summary>获取测试 Action 的非零运行 ID。</summary>
            public ulong ActionID { get; }

            /// <summary>获取或设置公开生命周期状态。</summary>
            public ActionStatus ActionState { get; set; }

            /// <summary>获取或设置暂停状态。</summary>
            public bool Paused { get; set; }

            /// <summary>获取当前测试租约是否已经释放。</summary>
            public bool Deinited { get; private set; }

            /// <summary>获取初始化调用次数。</summary>
            internal int InitCount { get; private set; }

            /// <summary>获取开始调用次数。</summary>
            internal int StartCount { get; private set; }

            /// <summary>获取执行调用次数。</summary>
            internal int ExecuteCount { get; private set; }

            /// <summary>获取正常完成调用次数。</summary>
            internal int FinishCount { get; private set; }

            /// <summary>重置公开状态并记录一次初始化。</summary>
            public void OnInit()
            {
                InitCount++;
                ActionState = ActionStatus.NotStart;
                Paused = false;
                Deinited = false;
            }

            /// <summary>记录调度器释放了当前租约。</summary>
            public void OnDeinit() => Deinited = true;

            /// <summary>记录首次开始。</summary>
            public void OnStart() => StartCount++;

            /// <summary>推进两次后标记完成。</summary>
            public void OnExecute(float dt)
            {
                ExecuteCount++;
                if (ExecuteCount >= 2) this.Finish();
            }

            /// <summary>记录一次正常完成。</summary>
            public void OnFinish() => FinishCount++;

            /// <summary>返回稳定测试名称。</summary>
            public string GetDebugInfo() => nameof(PlainAction);
        }
    }
}
