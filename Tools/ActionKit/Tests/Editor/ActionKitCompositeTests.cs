using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证 Sequence、Parallel、Repeat 的组合顺序、所有权和清理语义。
    /// </summary>
    public sealed class ActionKitCompositeTests
    {
        /// <summary>每个测试前重置静态调度器。</summary>
        [SetUp]
        public void SetUp() => ActionKitScheduler.Cleanup();

        /// <summary>每个测试后回收仍活动的组合树。</summary>
        [TearDown]
        public void TearDown() => ActionKitScheduler.Cleanup();

        /// <summary>
        /// 验证空 Sequence 和空 Parallel 都在 Start 调用内完成。
        /// </summary>
        [Test]
        public void EmptyContainersCompleteSynchronously()
        {
            IAction sequence = ActionKit.Sequence();
            IAction parallel = ActionKit.Parallel();

            sequence.Start();
            parallel.Start();

            Assert.AreEqual(ActionStatus.Finished, sequence.ActionState);
            Assert.AreEqual(ActionStatus.Finished, parallel.ActionState);
            Assert.AreEqual(2, ActionKitScheduler.FinishedCount);
        }

        /// <summary>
        /// 验证 Sequence 在一个 Tick 内不会把同一 delta 重复消费给两段 Delay。
        /// </summary>
        [Test]
        public void SequenceConsumesDeltaOnlyOncePerTick()
        {
            var order = new List<int>();
            ISequence sequence = ActionKit.Sequence()
                .Delay(1f, () => order.Add(1))
                .Delay(1f, () => order.Add(2));
            sequence.Start();

            ActionKitScheduler.Tick(1.1f, 1.1f);
            CollectionAssert.AreEqual(new[] { 1 }, order);
            Assert.AreEqual(ActionStatus.Started, sequence.ActionState);

            ActionKitScheduler.Tick(1.1f, 1.1f);
            CollectionAssert.AreEqual(new[] { 1, 2 }, order);
            Assert.AreEqual(ActionStatus.Finished, sequence.ActionState);
        }

        /// <summary>
        /// 验证 waitAny 在首个分支完成后不会继续执行同 Tick 的后续副作用。
        /// </summary>
        [Test]
        public void ParallelWaitAnyStopsAfterFirstWinner()
        {
            var winnerCount = 0;
            var loserCount = 0;
            IParallel parallel = ActionKit.Parallel(false)
                .Append(ActionKit.Callback(() => winnerCount++))
                .Append(ActionKit.Callback(() => loserCount++));

            parallel.Start();

            Assert.AreEqual(1, winnerCount);
            Assert.AreEqual(0, loserCount);
            Assert.AreEqual(ActionStatus.Finished, parallel.ActionState);
        }

        /// <summary>
        /// 验证 waitAll 会推进所有分支，并等待最后一条分支完成。
        /// </summary>
        [Test]
        public void ParallelWaitAllCompletesAfterEveryBranch()
        {
            var finishCount = 0;
            IParallel parallel = ActionKit.Parallel()
                .Append(ActionKit.Delay(0.5f, () => finishCount++))
                .Append(ActionKit.Delay(1f, () => finishCount++));
            parallel.Start();

            ActionKitScheduler.Tick(0.5f, 0.5f);
            Assert.AreEqual(1, finishCount);
            Assert.AreEqual(ActionStatus.Started, parallel.ActionState);

            ActionKitScheduler.Tick(0.5f, 0.5f);
            Assert.AreEqual(2, finishCount);
            Assert.AreEqual(ActionStatus.Finished, parallel.ActionState);
        }

        /// <summary>验证子回调提前结束 Sequence 后，同 Tick 不再执行后续兄弟节点。</summary>
        [Test]
        public void ExternalFinishStopsSequenceImmediately()
        {
            ISequence sequence = null;
            var trailingCount = 0;
            sequence = ActionKit.Sequence()
                .Callback(() => sequence.Finish())
                .Callback(() => trailingCount++);

            sequence.Start();

            Assert.AreEqual(0, trailingCount);
            Assert.AreEqual(ActionStatus.Finished, sequence.ActionState);
        }

        /// <summary>验证子回调提前结束 waitAll Parallel 后，不再推进后续并行分支。</summary>
        [Test]
        public void ExternalFinishStopsParallelImmediately()
        {
            IParallel parallel = null;
            var trailingCount = 0;
            parallel = ActionKit.Parallel()
                .Append(ActionKit.Callback(() => parallel.Finish()))
                .Append(ActionKit.Callback(() => trailingCount++));

            parallel.Start();

            Assert.AreEqual(0, trailingCount);
            Assert.AreEqual(ActionStatus.Finished, parallel.ActionState);
        }

        /// <summary>验证子回调提前结束 Repeat 后，不再推进本轮后续节点。</summary>
        [Test]
        public void ExternalFinishStopsRepeatImmediately()
        {
            IRepeat repeat = null;
            var trailingCount = 0;
            repeat = ActionKit.Repeat(3);
            repeat.Callback(() => repeat.Finish())
                .Callback(() => trailingCount++);

            repeat.Start();

            Assert.AreEqual(0, trailingCount);
            Assert.AreEqual(ActionStatus.Finished, repeat.ActionState);
        }

        /// <summary>验证 condition 提前结束 Repeat 后，不会重置已终结子树。</summary>
        [Test]
        public void ExternalFinishFromRepeatConditionDoesNotResetRound()
        {
            IRepeat repeat = null;
            RoundRestartProbeAction child = new();
            repeat = ActionKit.Repeat(3, () =>
            {
                repeat.Finish();
                return true;
            });
            repeat.Append(child);

            repeat.Start();

            Assert.AreEqual(1, child.InitCount);
            Assert.AreEqual(ActionStatus.Finished, repeat.ActionState);
        }

        /// <summary>验证 Sequence 子回调取消根后，同 Tick 不再执行后续兄弟节点。</summary>
        [Test]
        public void CancellationFromSequenceChildStopsRemainingSiblings()
        {
            IActionController controller = null;
            var trailingCount = 0;
            ISequence sequence = ActionKit.Sequence()
                .DelayFrame(1)
                .Callback(() => controller.Cancel())
                .Callback(() => trailingCount++);
            controller = sequence.Start();

            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCancelled);
            Assert.AreEqual(0, trailingCount);
        }

        /// <summary>验证 Parallel 分支取消根后，本 Tick 不再推进尚未访问的分支。</summary>
        [Test]
        public void CancellationFromParallelBranchStopsRemainingBranches()
        {
            IActionController controller = null;
            var trailingCount = 0;
            IParallel parallel = ActionKit.Parallel()
                .Append(ActionKit.DelayFrame(1, () => controller.Cancel()))
                .Append(ActionKit.DelayFrame(1, () => trailingCount++));
            controller = parallel.Start();

            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCancelled);
            Assert.AreEqual(0, trailingCount);
        }

        /// <summary>验证 Repeat 子回调取消根后，不再推进本轮后续节点或重启下一轮。</summary>
        [Test]
        public void CancellationFromRepeatChildStopsCurrentRound()
        {
            IActionController controller = null;
            var trailingCount = 0;
            IRepeat repeat = ActionKit.Repeat(3);
            repeat.DelayFrame(1)
                .Callback(() => controller.Cancel())
                .Callback(() => trailingCount++);
            controller = repeat.Start();

            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCancelled);
            Assert.AreEqual(0, trailingCount);
        }

        /// <summary>验证外层取消上下文不会污染回调中手动推进的独立组合树。</summary>
        [Test]
        public void ManualUpdateInsideCancelledCallbackUsesDetachedContext()
        {
            IActionController controller = null;
            var detachedCount = 0;
            ISequence detached = ActionKit.Sequence()
                .Callback(() => detachedCount++)
                .Callback(() => detachedCount++);
            try
            {
                ISequence outer = ActionKit.Sequence()
                    .DelayFrame(1)
                    .Callback(() =>
                    {
                        controller.Cancel();
                        Assert.IsTrue(detached.Update(0f));
                    });
                controller = outer.Start();

                ActionKitScheduler.Tick(0f, 0f);

                Assert.IsTrue(controller.IsCancelled);
                Assert.AreEqual(2, detachedCount);
                Assert.AreEqual(ActionStatus.Finished, detached.ActionState);
            }
            finally
            {
                ActionKitScheduler.DiscardUnscheduled(detached);
                ActionKitScheduler.ProcessRecycle();
            }
        }

        /// <summary>
        /// 验证后置分支先完成时，交换到后方的前置分支不会在同一个 Tick 重复消费 delta。
        /// </summary>
        [Test]
        public void ParallelAdvancesEachPendingBranchOnlyOncePerTick()
        {
            var slowFinishCount = 0;
            IParallel parallel = ActionKit.Parallel()
                .Append(ActionKit.Delay(2f, () => slowFinishCount++))
                .Append(ActionKit.Delay(0.5f));
            parallel.Start();

            ActionKitScheduler.Tick(0.5f, 0.5f);
            ActionKitScheduler.Tick(1f, 1f);

            Assert.AreEqual(0, slowFinishCount);
            Assert.AreEqual(ActionStatus.Started, parallel.ActionState);

            ActionKitScheduler.Tick(0.5f, 0.5f);
            Assert.AreEqual(1, slowFinishCount);
            Assert.AreEqual(ActionStatus.Finished, parallel.ActionState);
        }

        /// <summary>
        /// 验证 Repeat 按轮执行即时序列，condition 在第一轮结束后才参与判断。
        /// </summary>
        [Test]
        public void RepeatExecutesFirstRoundBeforeCheckingCondition()
        {
            var count = 0;
            IRepeat repeat = ActionKit.Repeat(10, () => false);
            repeat.Callback(() => count++);

            repeat.Start();

            Assert.AreEqual(1, count);
            Assert.AreEqual(ActionStatus.Finished, repeat.ActionState);
        }

        /// <summary>
        /// 验证有限 Repeat 每个调度周期至多执行一轮即时子树，避免无限同步循环。
        /// </summary>
        [Test]
        public void RepeatRunsAtMostOneImmediateRoundPerUpdate()
        {
            var count = 0;
            IRepeat repeat = ActionKit.Repeat(3);
            repeat.Callback(() => count++);
            repeat.Start();
            Assert.AreEqual(1, count);

            ActionKitScheduler.Tick(0f, 0f);
            Assert.AreEqual(2, count);
            ActionKitScheduler.Tick(0f, 0f);
            Assert.AreEqual(3, count);
            Assert.AreEqual(ActionStatus.Finished, repeat.ActionState);
        }

        /// <summary>验证有限 Repeat 的最后一轮也会按 2.0-pre 语义调用 condition。</summary>
        [Test]
        public void RepeatChecksConditionAfterEveryRoundIncludingLast()
        {
            var conditionCount = 0;
            IRepeat repeat = ActionKit.Repeat(2, () =>
            {
                conditionCount++;
                return true;
            });
            repeat.Callback(null);

            repeat.Start();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.AreEqual(2, conditionCount);
            Assert.AreEqual(ActionStatus.Finished, repeat.ActionState);
        }

        /// <summary>
        /// 验证组合器在装配阶段拒绝 null，避免延迟到 Tick 才形成循环异常。
        /// </summary>
        [Test]
        public void AppendRejectsNullAction()
        {
            ISequence sequence = ActionKit.Sequence();

            Assert.Throws<ArgumentNullException>(() => sequence.Append(null));
        }

        /// <summary>
        /// 验证容器不能把自己作为子 Action，避免递归初始化导致栈溢出。
        /// </summary>
        [Test]
        public void AppendRejectsSelfReference()
        {
            ISequence sequence = ActionKit.Sequence();

            Assert.Throws<InvalidOperationException>(() => sequence.Append(sequence));
        }

        /// <summary>
        /// 验证同一个 Action 不能被两个父容器持有和重复释放。
        /// </summary>
        [Test]
        public void ActionCannotBelongToTwoParents()
        {
            IAction child = ActionKit.Delay(1f, null);
            ISequence first = ActionKit.Sequence().Append(child);
            ISequence second = ActionKit.Sequence();

            Assert.IsNotNull(first);
            Assert.Throws<InvalidOperationException>(() => second.Append(child));
        }

        /// <summary>
        /// 验证跨两个容器形成的间接环会在 Append 时被拒绝。
        /// </summary>
        [Test]
        public void AppendRejectsIndirectContainerCycle()
        {
            ISequence outer = ActionKit.Sequence();
            ISequence inner = ActionKit.Sequence();
            outer.Append(inner);

            Assert.Throws<InvalidOperationException>(() => inner.Append(outer));
        }

        /// <summary>
        /// 验证活动根 Action 不能被第二个 controller 重复启动。
        /// </summary>
        [Test]
        public void ActiveActionCannotStartTwice()
        {
            IAction action = ActionKit.Delay(10f, null);
            action.Start();

            Assert.Throws<InvalidOperationException>(() => action.Start());
        }

        /// <summary>
        /// 验证全局 Cleanup 不会清除尚未启动树的父子所有权，使同一个子节点被第二棵树窃取。
        /// </summary>
        [Test]
        public void CleanupPreservesOwnershipOfUnscheduledTree()
        {
            IAction child = ActionKit.Delay(1f, null);
            ISequence first = ActionKit.Sequence().Append(child);
            ISequence second = ActionKit.Sequence();
            try
            {
                ActionKitScheduler.Cleanup();

                Assert.Throws<InvalidOperationException>(() => second.Append(child));
            }
            finally
            {
                ActionKitScheduler.DiscardUnscheduled(first);
                ActionKitScheduler.DiscardUnscheduled(second);
                ActionKitScheduler.ProcessRecycle();
            }
        }

        /// <summary>
        /// 验证根树一旦启动，后续 fluent Append 会立即失败，而不是在 Tick 中修改活动列表。
        /// </summary>
        [Test]
        public void ActiveContainerRejectsFurtherConfiguration()
        {
            ISequence sequence = ActionKit.Sequence().Delay(10f);
            sequence.Start();

            Assert.Throws<InvalidOperationException>(() => sequence.Callback(null));
        }

        /// <summary>
        /// 验证嵌套配置回调抛异常时，已创建容器及其子树仍会完成 Deinit 并等待回池。
        /// </summary>
        [Test]
        public void FailedNestedBuilderClosesCreatedTree()
        {
            ISequence parent = ActionKit.Sequence();
            IAction nestedAction = null;
            try
            {
                Assert.Throws<InvalidOperationException>(() => parent.Sequence(nested =>
                {
                    nestedAction = nested;
                    nested.Delay(1f);
                    throw new InvalidOperationException("expected");
                }));

                Assert.IsNotNull(nestedAction);
                Assert.IsTrue(nestedAction.Deinited);
            }
            finally
            {
                ActionKitScheduler.DiscardUnscheduled(parent);
                ActionKitScheduler.ProcessRecycle();
            }
        }

        /// <summary>记录 Repeat 是否在终结后错误重启下一轮。</summary>
        private sealed class RoundRestartProbeAction : ActionBase
        {
            /// <summary>获取当前探针被初始化的次数。</summary>
            internal int InitCount { get; private set; }

            /// <summary>记录轮次初始化。</summary>
            public override void OnInit()
            {
                base.OnInit();
                InitCount++;
            }

            /// <summary>探针在进入本轮时立即正常完成。</summary>
            public override void OnStart() => this.Finish();
        }
    }
}
