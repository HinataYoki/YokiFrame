using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证 Task、Coroutine、清理和稳定 Tick 的竞态与分配边界。
    /// </summary>
    public sealed class ActionKitAsyncSafetyTests
    {
        private readonly ActionKitTestLogger mLogger = new();

        /// <summary>每个测试前清空所有活动与待启动动作。</summary>
        [SetUp]
        public void SetUp()
        {
            ActionKitScheduler.Cleanup();
            mLogger.Clear();
            LogKit.SetLogger(mLogger);
        }

        /// <summary>每个测试后再次清理，确保失败断言不会污染后续用例。</summary>
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

        /// <summary>
        /// 验证 Task 仅由宿主 Tick 观察完成，不依赖 async continuation 改写 Action 状态。
        /// </summary>
        [Test]
        public void TaskCompletionIsObservedOnSchedulerTick()
        {
            TaskCompletionSource<bool> completion = new();
            IAction action = ActionKit.Task(() => completion.Task);
            action.Start();

            completion.SetResult(true);
            Assert.AreEqual(ActionStatus.Started, action.ActionState);

            ActionKitScheduler.Tick(0f, 0f);
            Assert.AreEqual(ActionStatus.Finished, action.ActionState);
        }

        /// <summary>
        /// 验证旧 Task 在 Action 取消后完成，不会结束后续 TaskAction 租约。
        /// </summary>
        [Test]
        public void CancelledTaskCannotCompleteLaterActionLease()
        {
            TaskCompletionSource<bool> oldCompletion = new();
            IAction oldAction = ActionKit.Task(() => oldCompletion.Task);
            IActionController oldController = oldAction.Start();
            oldController.Cancel();
            ActionKitScheduler.Tick(0f, 0f);
            ActionKitScheduler.ProcessRecycle();

            TaskCompletionSource<bool> nextCompletion = new();
            IAction nextAction = ActionKit.Task(() => nextCompletion.Task);
            nextAction.Start();

            Assert.AreNotSame(oldAction, nextAction, "公开 Action 租约不得复用同一对象并产生 ABA。");

            oldCompletion.SetResult(true);
            ActionKitScheduler.Tick(0f, 0f);
            Assert.AreEqual(ActionStatus.Started, nextAction.ActionState);

            nextCompletion.SetResult(true);
            ActionKitScheduler.Tick(0f, 0f);
            Assert.AreEqual(ActionStatus.Finished, nextAction.ActionState);
        }

        /// <summary>
        /// 验证 faulted Task 形成 ActionKit Faulted 终态，不调用正常完成回调。
        /// </summary>
        [Test]
        public void FaultedTaskDoesNotReportNormalCompletion()
        {
            var finishCount = 0;
            IAction action = ActionKit.Task(() => System.Threading.Tasks.Task.FromException(new InvalidOperationException("expected")));

            action.Start(_ => finishCount++);

            Assert.AreEqual(0, finishCount);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            Assert.AreEqual(0, ActionKitScheduler.ExecutingCount);
            mLogger.AssertErrors("[ActionKit] Action ");
        }

        /// <summary>
        /// 验证空 Task factory 在构造阶段明确失败，不创建永久悬挂 Action。
        /// </summary>
        [Test]
        public void TaskRejectsNullFactory()
        {
            Assert.Throws<ArgumentNullException>(() => ActionKit.Task((Func<Task>)null));
        }

        /// <summary>
        /// 验证 Coroutine 取消会 Dispose 当前枚举器，使 finally/资源释放路径闭合。
        /// </summary>
        [Test]
        public void CoroutineCancellationDisposesEnumerator()
        {
            DisposableEnumerator enumerator = new();
            IActionController controller = ActionKit.Coroutine(() => enumerator).Start();

            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(enumerator.Disposed);
        }

        /// <summary>
        /// 验证嵌套 IEnumerator 会在内部结束后返回父枚举器继续推进。
        /// </summary>
        [Test]
        public void CoroutineSupportsNestedEnumerator()
        {
            var steps = 0;
            IAction action = ActionKit.Coroutine(() => ParentRoutine(() => steps++));
            action.Start();

            ActionKitScheduler.Tick(0f, 0f);
            ActionKitScheduler.Tick(0f, 0f);
            ActionKitScheduler.Tick(0f, 0f);

            Assert.AreEqual(3, steps);
            Assert.AreEqual(ActionStatus.Finished, action.ActionState);
        }

        /// <summary>
        /// 验证超过嵌套深度时，尚未压栈的最深 IEnumerator 也会被释放。
        /// </summary>
        [Test]
        public void CoroutineDepthFailureDisposesRejectedNestedEnumerator()
        {
            NestedDisposableEnumerator[] enumerators =
                CreateNestedEnumerators(CoroutineAction.MAX_NESTED_DEPTH + 2);

            IActionController controller = ActionKit.Coroutine(enumerators[0]).Start();

            Assert.IsTrue(controller.IsFaulted);
            for (var index = 0; index < enumerators.Length; index++)
                Assert.IsTrue(enumerators[index].Disposed, "嵌套枚举器未释放，索引: " + index);
            mLogger.AssertErrors("[ActionKit] Action ");
        }

        /// <summary>
        /// 验证 Cleanup 同时释放待启动和正在执行的动作，而不是直接丢弃集合引用。
        /// </summary>
        [Test]
        public void CleanupDeinitializesEveryScheduledAction()
        {
            ProbeAction first = new();
            ProbeAction second = new();
            first.Start();
            second.Start();

            ActionKitScheduler.Cleanup();

            Assert.IsTrue(first.DeinitCalled);
            Assert.IsTrue(second.DeinitCalled);
            Assert.AreEqual(0, ActionKitScheduler.ExecutingCount);
        }

        /// <summary>
        /// 验证帧计数使用 long，避免长期运行和 DelayFrame 截止值发生 int 溢出。
        /// </summary>
        [Test]
        public void SchedulerFrameCountUsesLong()
        {
            Assert.AreEqual(typeof(long), typeof(ActionKitScheduler).GetProperty(nameof(ActionKitScheduler.FrameCount)).PropertyType);
        }

        /// <summary>
        /// 验证容量预热后纯 Tick 热路径不产生 ActionKit 自身的线程分配。
        /// </summary>
        [Test]
        public void SteadyStateTickDoesNotAllocate()
        {
            ActionKit.Delay(10000f, null).Start();
            for (var index = 0; index < 16; index++)
            {
                ActionKitScheduler.Tick(0.016f, 0.016f);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 256; index++)
            {
                ActionKitScheduler.Tick(0.016f, 0.016f);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated, "稳定 Tick 不应创建临时集合、快照数组或日志字符串。");
        }

        /// <summary>
        /// 验证 Repeat 每轮都会重新调用 Task factory，而不是重复观察上一轮已经完成的 Task。
        /// </summary>
        [Test]
        public void RepeatRecreatesFactoryTaskForEveryRound()
        {
            var factoryCount = 0;
            IRepeat repeat = ActionKit.Repeat(2);
            repeat.Task(() =>
            {
                factoryCount++;
                return System.Threading.Tasks.Task.CompletedTask;
            });

            repeat.Start();
            Assert.AreEqual(1, factoryCount);
            ActionKitScheduler.Tick(0f, 0f);

            Assert.AreEqual(2, factoryCount);
            Assert.AreEqual(ActionStatus.Finished, repeat.ActionState);
        }

        /// <summary>
        /// 验证已完成 Action 不能复活，后续分配也不会复用同一公开对象。
        /// </summary>
        [Test]
        public void CompletedActionCannotBeResurrectedOrReused()
        {
            IAction oldAction = ActionKit.Callback(null);
            oldAction.Start();
            ActionKitScheduler.ProcessRecycle();

            oldAction.OnInit();
            Assert.IsTrue(oldAction.Deinited);
            Assert.Throws<InvalidOperationException>(() => oldAction.Update(0f));
            Assert.Throws<InvalidOperationException>(() => oldAction.Start());

            IAction nextLease = ActionKit.Callback(null);
            Assert.AreNotSame(oldAction, nextLease);
            nextLease.Start();
            Assert.AreEqual(ActionStatus.Finished, nextLease.ActionState);
        }

        /// <summary>验证旧 Action 引用的 Finish 不会提前完成后续同类型租约。</summary>
        [Test]
        public void OldActionReferenceCannotFinishLaterLease()
        {
            IAction oldAction = ActionKit.Delay(0f);
            oldAction.Start();
            ActionKitScheduler.ProcessRecycle();

            var finishCount = 0;
            IAction nextAction = ActionKit.Delay(10f, () => finishCount++);
            nextAction.Start();

            oldAction.Finish();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.AreNotSame(oldAction, nextAction);
            Assert.AreEqual(ActionStatus.Started, nextAction.ActionState);
            Assert.AreEqual(0, finishCount);
        }

        /// <summary>
        /// 验证显式暂停、恢复和时间源切换只遍历现有树，不创建递归闭包或临时集合。
        /// </summary>
        [Test]
        public void ExplicitControllerChangesDoNotAllocate()
        {
            IActionController controller = ActionKit.Delay(10000f, null).Start();
            controller.Pause();
            controller.Resume();
            controller.UpdateMode = ActionUpdateModes.UnscaledDeltaTime;
            controller.UpdateMode = ActionUpdateModes.ScaledDeltaTime;

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 256; index++)
            {
                controller.Pause();
                controller.Resume();
                controller.UpdateMode = ActionUpdateModes.UnscaledDeltaTime;
                controller.UpdateMode = ActionUpdateModes.ScaledDeltaTime;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated, "显式控制不应为递归传播创建委托或临时集合。");
        }

        /// <summary>验证异常并发峰值结束后，Scheduler 静态列表不会永久保留峰值数组。</summary>
        [Test]
        public void SchedulerBuffersTrimAfterAbnormalPeak()
        {
            const int peakActionCount = 1100;
            for (var index = 0; index < peakActionCount; index++)
                ActionKit.Delay(1000f).Start();

            ActionKitScheduler.Tick(0f, 0f);
            ActionKitScheduler.Cleanup();

            Assert.LessOrEqual(
                GetSchedulerListCapacity("sPrepared"),
                ActionKitScheduler.MAX_RETAINED_PREPARED_CAPACITY);
            Assert.LessOrEqual(
                GetSchedulerListCapacity("sExecuting"),
                ActionKitScheduler.MAX_RETAINED_EXECUTING_CAPACITY);
            Assert.LessOrEqual(
                GetSchedulerListCapacity("sPendingRecycle"),
                ActionKitScheduler.MAX_RETAINED_RECYCLE_CAPACITY);
        }

        /// <summary>读取指定 Scheduler 私有 List 的 Capacity，验证峰值缩容策略。</summary>
        /// <param name="fieldName">Scheduler 静态列表字段名。</param>
        /// <returns>当前底层数组容量。</returns>
        private static int GetSchedulerListCapacity(string fieldName)
        {
            FieldInfo field = typeof(ActionKitScheduler).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            object list = field.GetValue(null);
            PropertyInfo capacity = list.GetType().GetProperty("Capacity");
            Assert.IsNotNull(capacity);
            return (int)capacity.GetValue(list);
        }

        /// <summary>
        /// 构造包含一个嵌套枚举器的三步流程，验证父子恢复顺序。
        /// </summary>
        /// <param name="step">每一步递增计数的回调。</param>
        /// <returns>可由 CoroutineAction 逐帧推进的父枚举器。</returns>
        private static IEnumerator ParentRoutine(Action step)
        {
            step();
            yield return ChildRoutine(step);
            step();
        }

        /// <summary>
        /// 构造单步子枚举器，并显式跨过一个调度 Tick。
        /// </summary>
        /// <param name="step">记录子步骤的回调。</param>
        /// <returns>嵌套枚举器。</returns>
        private static IEnumerator ChildRoutine(Action step)
        {
            step();
            yield return null;
        }

        /// <summary>
        /// 构造指定数量的单向嵌套可释放枚举器，首项作为 Coroutine 根。
        /// </summary>
        /// <param name="count">枚举器总数。</param>
        /// <returns>按父到子顺序排列的枚举器数组。</returns>
        private static NestedDisposableEnumerator[] CreateNestedEnumerators(int count)
        {
            NestedDisposableEnumerator[] enumerators = new NestedDisposableEnumerator[count];
            NestedDisposableEnumerator child = null;
            for (var index = count - 1; index >= 0; index--)
            {
                child = new NestedDisposableEnumerator(child);
                enumerators[index] = child;
            }

            return enumerators;
        }

        /// <summary>
        /// 提供持续运行且可观察 Dispose 的枚举器，用于取消资源释放测试。
        /// </summary>
        private sealed class DisposableEnumerator : IEnumerator, IDisposable
        {
            /// <summary>获取枚举器是否已被释放。</summary>
            public bool Disposed { get; private set; }

            /// <summary>持续返回同一个空值，保持动作活动。</summary>
            public object Current => null;

            /// <summary>保持枚举器未完成，直到测试发起取消。</summary>
            public bool MoveNext() => true;

            /// <summary>测试枚举器不支持重置。</summary>
            public void Reset() => throw new NotSupportedException();

            /// <summary>记录 ActionKit 已闭合枚举器生命周期。</summary>
            public void Dispose() => Disposed = true;
        }

        /// <summary>只产出一个子枚举器并记录释放，用于深度上限资源闭合测试。</summary>
        private sealed class NestedDisposableEnumerator : IEnumerator, IDisposable
        {
            private readonly IEnumerator mChild;
            private bool mYielded;

            /// <summary>保存下一层枚举器；最深节点使用 null。</summary>
            /// <param name="child">下一层枚举器。</param>
            internal NestedDisposableEnumerator(IEnumerator child) => mChild = child;

            /// <summary>获取本轮产出的下一层枚举器。</summary>
            public object Current => mChild;

            /// <summary>有子节点时只产出一次，叶节点直接完成。</summary>
            public bool MoveNext()
            {
                if (mYielded || mChild == null) return false;
                mYielded = true;
                return true;
            }

            /// <summary>测试枚举器不支持重置。</summary>
            public void Reset() => throw new NotSupportedException();

            /// <summary>获取枚举器是否已释放。</summary>
            internal bool Disposed { get; private set; }

            /// <summary>记录 ActionKit 已闭合当前嵌套枚举器。</summary>
            public void Dispose() => Disposed = true;
        }

        /// <summary>
        /// 记录 Cleanup 是否对活动自定义 Action 调用了 OnDeinit。
        /// </summary>
        private sealed class ProbeAction : ActionBase
        {
            /// <summary>获取释放钩子是否被调用。</summary>
            public bool DeinitCalled { get; private set; }

            /// <summary>保持动作运行，等待 Cleanup 关闭生命周期。</summary>
            public override void OnExecute(float dt) { }

            /// <summary>记录调度器执行了清理。</summary>
            public override void OnDeinit() => DeinitCalled = true;
        }
    }
}
