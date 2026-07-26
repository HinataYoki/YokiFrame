#if UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace YokiFrame.Tests
{
    /// <summary>覆盖 Unity Coroutine Adapter 独有的 yield、取消、故障、Repeat 与观察分配边界。</summary>
    public sealed class ActionKitUnityCoroutineTests
    {
        private const float TEST_TIMEOUT_SECONDS = 2f;
        private readonly ActionKitTestLogger mLogger = new();

        /// <summary>每个 PlayMode 用例前清空调度器并接管预期错误日志。</summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ActionKitScheduler.Cleanup();
            mLogger.Clear();
            LogKit.SetLogger(mLogger);
            yield return null;
        }

        /// <summary>每个 PlayMode 用例后停止残留原生 Coroutine，并拒绝未声明 Error。</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                ActionKitScheduler.Cleanup();
                mLogger.AssertNoErrors();
            }
            finally { LogKit.ClearLogger(); }
            yield return null;
        }

        /// <summary>验证 WaitForSeconds 由 Unity 解释，ActionKit 不把它退化为普通一帧等待。</summary>
        [UnityTest]
        public IEnumerator UnityYieldInstructionControlsCompletion()
        {
            var callbackCount = 0;
            IActionController controller = ActionKitUnityCoroutine
                .From(() => WaitForSecondsRoutine(() => callbackCount++))
                .Start();

            yield return null;
            Assert.IsFalse(controller.IsCompleted);

            yield return WaitForTerminal(controller);
            Assert.AreEqual(1, callbackCount);
            Assert.IsFalse(controller.IsFaulted);
        }

        /// <summary>验证嵌套 IEnumerator 仍由 Unity 原生 Coroutine 调用栈解释。</summary>
        [UnityTest]
        public IEnumerator NestedEnumeratorReturnsToParent()
        {
            var steps = 0;
            IActionController controller = ActionKitUnityCoroutine
                .From(() => ParentRoutine(() => steps++))
                .Start();

            yield return WaitForTerminal(controller);

            Assert.AreEqual(3, steps);
            Assert.IsFalse(controller.IsFaulted);
        }

        /// <summary>验证取消会停止 Unity Coroutine 并且只 Dispose 底层枚举器一次。</summary>
        [UnityTest]
        public IEnumerator CancellationStopsAndDisposesEnumeratorExactlyOnce()
        {
            TrackingEnumerator enumerator = new(new WaitForSeconds(10f));
            IActionController controller = ActionKitUnityCoroutine.From(enumerator).Start();

            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);
            yield return null;

            Assert.IsTrue(controller.IsCancelled);
            Assert.AreEqual(1, enumerator.DisposeCount);
            Assert.AreEqual(1, enumerator.MoveNextCount);
        }

        /// <summary>验证 Unity 调用 MoveNext 时的异常在 ActionKit 宿主线程形成一次 Faulted。</summary>
        [Test]
        public void EnumeratorExceptionBecomesFaultedTerminal()
        {
            IActionController controller = ActionKitUnityCoroutine.From(new ThrowingEnumerator()).Start();

            Assert.IsTrue(controller.IsCompleted);
            Assert.IsTrue(controller.IsFaulted);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            mLogger.AssertSingleError("[ActionKit] Action ");
        }

        /// <summary>验证宿主从 ActionKit 生命周期外终止 Coroutine 时形成 Faulted，并且只释放一次枚举器。</summary>
        [Test]
        public void ExternalDisposeBecomesFaultedTerminal()
        {
            TrackingEnumerator enumerator = new(new WaitForSeconds(10f));
            IAction action = ActionKitUnityCoroutine.From(enumerator);
            IActionController controller = action.Start();

            ((IDisposable)action).Dispose();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsFaulted);
            Assert.AreEqual(1, enumerator.DisposeCount);
            mLogger.AssertSingleError("[ActionKit] Action ");
        }

        /// <summary>验证暂停只冻结 ActionKit 终态消费，不伪装成能暂停已经提交的 Unity yield。</summary>
        [UnityTest]
        public IEnumerator PauseDefersTerminalObservationUntilResume()
        {
            IActionController controller = ActionKitUnityCoroutine.From(CompleteNextFrame()).Start();
            controller.Pause();

            yield return null;
            Assert.IsFalse(controller.IsCompleted);

            controller.Resume();
            ActionKitScheduler.Tick(0f, 0f);
            Assert.IsTrue(controller.IsCompleted);
        }

        /// <summary>验证 Repeat 每轮调用 factory 创建新的 Unity IEnumerator，并逐轮闭合资源。</summary>
        [UnityTest]
        public IEnumerator RepeatCreatesAndDisposesEnumeratorPerRound()
        {
            var factoryCount = 0;
            var disposeCount = 0;
            IRepeat repeat = ActionKit.Repeat(2);
            repeat.UnityCoroutine(() =>
            {
                factoryCount++;
                return new TrackingEnumerator(null, () => disposeCount++);
            });

            IActionController controller = repeat.Start();
            yield return WaitForTerminal(controller);

            Assert.AreEqual(2, factoryCount);
            Assert.AreEqual(2, disposeCount);
            Assert.IsFalse(controller.IsFaulted);
        }

        /// <summary>验证直接 IEnumerator 是一次性租约，Repeat 二次使用时明确 Faulted。</summary>
        [UnityTest]
        public IEnumerator DirectEnumeratorRejectsSecondRepeatConsumption()
        {
            TrackingEnumerator enumerator = new(null);
            IRepeat repeat = ActionKit.Repeat(2);
            repeat.Append(enumerator.ToUnityAction());

            IActionController controller = repeat.Start();
            yield return WaitForTerminal(controller);

            Assert.IsTrue(controller.IsFaulted);
            Assert.AreEqual(1, enumerator.DisposeCount);
            mLogger.AssertSingleError("[ActionKit] Action ");
        }

        /// <summary>验证等待原生 Coroutine 时，ActionKit 的稳定 Tick 不产生托管分配。</summary>
        [Test]
        public void PendingCoroutineObservationAllocatesZeroBytes()
        {
            const int TICK_COUNT = 256;
            IActionController controller = ActionKitUnityCoroutine
                .From(new TrackingEnumerator(new WaitForSeconds(10f)))
                .Start();
            ActionKitScheduler.Tick(0f, 0f);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < TICK_COUNT; index++)
                ActionKitScheduler.Tick(0f, 0f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);
        }

        /// <summary>创建等待 Unity 时间后调用回调的原生枚举器。</summary>
        /// <param name="completed">等待完成回调。</param>
        /// <returns>包含真实 WaitForSeconds 的枚举器。</returns>
        private static IEnumerator WaitForSecondsRoutine(Action completed)
        {
            yield return new WaitForSeconds(0.1f);
            completed();
        }

        /// <summary>创建包含嵌套枚举器的三步调用栈。</summary>
        /// <param name="step">每步调用一次的计数回调。</param>
        /// <returns>父枚举器。</returns>
        private static IEnumerator ParentRoutine(Action step)
        {
            step();
            yield return ChildRoutine(step);
            step();
        }

        /// <summary>创建跨一帧完成的嵌套枚举器。</summary>
        /// <param name="step">子步骤回调。</param>
        /// <returns>子枚举器。</returns>
        private static IEnumerator ChildRoutine(Action step)
        {
            yield return null;
            step();
        }

        /// <summary>创建下一 Unity 帧结束的枚举器。</summary>
        /// <returns>单帧枚举器。</returns>
        private static IEnumerator CompleteNextFrame()
        {
            yield return null;
        }

        /// <summary>等待 controller 进入终态，并在超时时明确失败。</summary>
        /// <param name="controller">待观察 controller。</param>
        /// <returns>UnityTest 可迭代等待器。</returns>
        private static IEnumerator WaitForTerminal(IActionController controller)
        {
            float deadline = Time.realtimeSinceStartup + TEST_TIMEOUT_SECONDS;
            while (!controller.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(controller.IsCompleted, "Unity Coroutine 在限定时间内未进入终态。");
        }

        /// <summary>提供可观察 MoveNext 与 Dispose 次数的原生枚举器。</summary>
        private sealed class TrackingEnumerator : IEnumerator, IDisposable
        {
            private readonly object mFirstYield;
            private readonly Action mDisposed;
            private bool mYielded;

            /// <summary>创建最多 yield 一次的枚举器。</summary>
            /// <param name="firstYield">首次产出的 Unity yield；null 表示普通一帧。</param>
            /// <param name="disposed">首次 Dispose 时调用的回调。</param>
            internal TrackingEnumerator(object firstYield, Action disposed = null)
            {
                mFirstYield = firstYield;
                mDisposed = disposed;
            }

            /// <summary>获取当前 Unity yield 值。</summary>
            public object Current => mFirstYield;

            /// <summary>获取 MoveNext 调用次数。</summary>
            internal int MoveNextCount { get; private set; }

            /// <summary>获取 Dispose 调用次数。</summary>
            internal int DisposeCount { get; private set; }

            /// <summary>首次调用产出 yield，第二次完成。</summary>
            public bool MoveNext()
            {
                MoveNextCount++;
                if (mYielded) return false;
                mYielded = true;
                return true;
            }

            /// <summary>测试枚举器不支持重置。</summary>
            public void Reset() => throw new NotSupportedException();

            /// <summary>记录首次资源闭合，重复 Dispose 不重复调用业务回调。</summary>
            public void Dispose()
            {
                DisposeCount++;
                if (DisposeCount == 1) mDisposed?.Invoke();
            }
        }

        /// <summary>在 Unity 第一次推进时抛出固定异常。</summary>
        private sealed class ThrowingEnumerator : IEnumerator
        {
            /// <summary>异常用例没有有效 Current。</summary>
            public object Current => null;

            /// <summary>模拟原生 Coroutine 用户代码故障。</summary>
            public bool MoveNext() => throw new InvalidOperationException("expected");

            /// <summary>测试枚举器不支持重置。</summary>
            public void Reset() => throw new NotSupportedException();
        }
    }
}
#endif
