#if UNITY_5_3_OR_NEWER && YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>覆盖 UniTask Integration 独有的完成、取消、故障、Repeat 和分配边界。</summary>
    public sealed class ActionKitUniTaskTests
    {
        private readonly ActionKitTestLogger mLogger = new();

        /// <summary>每个测试前清空静态调度状态并接管预期错误日志。</summary>
        [SetUp]
        public void SetUp()
        {
            ActionKitScheduler.Cleanup();
            mLogger.Clear();
            LogKit.SetLogger(mLogger);
        }

        /// <summary>每个测试后关闭活动异步租约，并拒绝未声明 Error。</summary>
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

        /// <summary>验证同步完成的 UniTask 在 Start 的 dt=0 首推内正常完成。</summary>
        [Test]
        public void Start_工厂已完成_同步结束()
        {
            IActionController controller = ActionKitUniTask.From(static () => UniTask.CompletedTask).Start();

            Assert.IsTrue(controller.IsCompleted);
            Assert.IsFalse(controller.IsFaulted);
            Assert.AreEqual(1, ActionKitScheduler.FinishedCount);
        }

        /// <summary>验证异步源完成后不会从 continuation 直接终结 controller，而由下一宿主 Tick 观察。</summary>
        [Test]
        public void Tick_UniTask完成后_只在宿主帧观察()
        {
            UniTaskCompletionSource source = new();
            IActionController controller = ActionKitUniTask.From(() => source.Task).Start();

            source.TrySetResult();
            Assert.IsFalse(controller.IsCompleted);

            ActionKitScheduler.Tick(0f, 0f);
            Assert.IsTrue(controller.IsCompleted);
            Assert.IsFalse(controller.IsFaulted);
        }

        /// <summary>验证可取消 factory 接收的 token 在 ActionKit 取消时被同步请求取消。</summary>
        [Test]
        public void Cancel_带令牌工厂_同步取消()
        {
            CancellationToken observedToken = default;
            IActionController controller = ActionKitUniTask.From(token =>
            {
                observedToken = token;
                return UniTask.WaitUntilCanceled(token);
            }).Start();

            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(observedToken.IsCancellationRequested);
            Assert.IsTrue(controller.IsCancelled);
            Assert.IsFalse(controller.IsFaulted);
        }

        /// <summary>验证已正常完成的 token factory 只释放资源，不在成功终态触发取消回调。</summary>
        [Test]
        public void Cleanup_已完成令牌工厂_不再取消()
        {
            CancellationToken observedToken = default;
            IActionController controller = ActionKitUniTask.From(token =>
            {
                observedToken = token;
                return UniTask.CompletedTask;
            }).Start();

            Assert.IsTrue(controller.IsCompleted);
            Assert.IsFalse(controller.IsFaulted);
            Assert.IsFalse(observedToken.IsCancellationRequested);
        }

        /// <summary>验证 faulted UniTask 只形成一次 Faulted，不调用正常完成路径。</summary>
        [Test]
        public void Tick_UniTask失败_只进入Faulted()
        {
            IActionController controller = ActionKitUniTask
                .From(static () => UniTask.FromException(new InvalidOperationException("expected")))
                .Start();

            Assert.IsTrue(controller.IsCompleted);
            Assert.IsTrue(controller.IsFaulted);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            mLogger.AssertSingleError("[ActionKit] Action ");
        }

        /// <summary>验证 Action 租约取消后才发生的 UniTask fault 仍被观察，不产生未观察异常。</summary>
        [Test]
        public void Cancel_取消后源失败_仍报告故障()
        {
            UniTaskCompletionSource source = new();
            IActionController controller = ActionKitUniTask.From(() => source.Task).Start();

            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);
            source.TrySetException(new InvalidOperationException("detached expected"));

            Assert.IsTrue(controller.IsCancelled);
            Assert.IsFalse(controller.IsFaulted);
            mLogger.AssertSingleError("[ActionKit] Detached UniTask faulted: ");
        }

        /// <summary>验证 Repeat 每轮重新调用 factory，不重复消费上一轮 UniTask source。</summary>
        [Test]
        public void Repeat_每一轮_重新调用工厂()
        {
            var factoryCount = 0;
            IRepeat repeat = ActionKit.Repeat(2);
            repeat.UniTask(() =>
            {
                factoryCount++;
                return UniTask.CompletedTask;
            });

            IActionController controller = repeat.Start();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCompleted);
            Assert.AreEqual(2, factoryCount);
        }

        /// <summary>验证直接 UniTask 是一次性租约，Repeat 二次消费会明确 Faulted 而非隐藏复用 source。</summary>
        [Test]
        public void Repeat_直接UniTask第二次_进入Faulted()
        {
            IRepeat repeat = ActionKit.Repeat(2);
            repeat.Append(UniTask.CompletedTask.ToAction());

            IActionController controller = repeat.Start();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsFaulted);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            mLogger.AssertSingleError("[ActionKit] Action ");
        }

        /// <summary>验证未完成 UniTask 的稳定 Tick 只轮询状态，不产生每帧托管分配。</summary>
        [Test]
        public void Tick_未完成UniTask_不产生托管分配()
        {
            const int TICK_COUNT = 256;
            UniTaskCompletionSource source = new();
            IActionController controller = ActionKitUniTask.From(() => source.Task).Start();
            ActionKitScheduler.Tick(0f, 0f);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < TICK_COUNT; index++)
                ActionKitScheduler.Tick(0f, 0f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            source.TrySetResult();
            ActionKitScheduler.Tick(0f, 0f);
            Assert.IsTrue(controller.IsCompleted);
        }
    }
}
#endif
