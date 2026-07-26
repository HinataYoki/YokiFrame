#if UNITY_EDITOR && YOKIFRAME_DOTWEEN_SUPPORT
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>覆盖 DOTween 接管、暂停、时间源和取消语义。</summary>
    public sealed class ActionKitDOTweenTests
    {
        private static readonly FieldInfo sDotweenInitializedField = typeof(DOTween).GetField(
            "initialized",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private bool mWasDotweenInitialized;

        /// <summary>EditMode 无法调用 DOTween public Init，测试前只置位全局标志以验证 Manual Tween 生命周期。</summary>
        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(sDotweenInitializedField, "当前 DOTween 版本缺少 initialized 状态字段。");
            mWasDotweenInitialized = (bool)sDotweenInitializedField.GetValue(null);
            sDotweenInitializedField.SetValue(null, true);
        }

        /// <summary>每个测试后关闭动作并清理 DOTween，避免跨测试保留静态状态。</summary>
        [TearDown]
        public void TearDown()
        {
            ActionKitScheduler.Cleanup();
            DOTween.KillAll(false);
            sDotweenInitializedField.SetValue(null, mWasDotweenInitialized);
        }

        /// <summary>验证 Sequence 后置 Tween 在轮到前保持暂停。</summary>
        [Test]
        public void SequenceTweenDoesNotPlayBeforeItsTurn()
        {
            float value = 0f;
            Tween tween = DOTween.To(() => value, current => value = current, 1f, 1f)
                .SetUpdate(UpdateType.Manual);
            ISequence sequence = ActionKit.Sequence().Delay(1f).DOTween(tween, UpdateType.Manual);

            sequence.Start();
            tween.ManualUpdate(0.5f, 0.5f);
            Assert.AreEqual(0f, value, 0.0001f);

            ActionKitScheduler.Tick(1f, 1f);
            tween.ManualUpdate(0.5f, 0.5f);
            Assert.Greater(value, 0f);
        }

        /// <summary>验证恢复父 Sequence 不会提前播放尚未轮到的 Tween。</summary>
        [Test]
        public void SequenceResumeDoesNotPlayTweenBeforeItsTurn()
        {
            float value = 0f;
            Tween tween = DOTween.To(() => value, current => value = current, 1f, 1f)
                .SetUpdate(UpdateType.Manual);
            IActionController controller = ActionKit.Sequence().Delay(1f).DOTween(tween).Start();

            controller.Pause();
            controller.Resume();
            tween.ManualUpdate(0.5f, 0.5f);

            Assert.IsFalse(tween.IsPlaying());
            Assert.AreEqual(0f, value, 0.0001f);
        }

        /// <summary>验证 controller 暂停和恢复直接映射到 Tween。</summary>
        [Test]
        public void ControllerPauseAndResumeControlTween()
        {
            float value = 0f;
            Tween tween = DOTween.To(() => value, current => value = current, 1f, 1f)
                .SetUpdate(UpdateType.Manual);
            IActionController controller = tween.ToAction().Start();

            Assert.IsTrue(tween.IsPlaying());
            controller.Pause();
            Assert.IsFalse(tween.IsPlaying());
            controller.Resume();
            Assert.IsTrue(tween.IsPlaying());
        }

        /// <summary>验证旧入口在 controller 时间源变化时仍完整保留调用方已有的 Manual/independent 配置。</summary>
        [Test]
        public void LegacyEntryPreservesExistingTweenTiming()
        {
            float value = 0f;
            Tween tween = DOTween.To(() => value, current => value = current, 1f, 1f)
                .SetUpdate(UpdateType.Manual, true);
            IActionController controller = tween.ToAction().Start();

            controller.UpdateMode = ActionUpdateModes.UnscaledDeltaTime;
            Assert.IsTrue(tween.IsTimeScaleIndependent());
            controller.UpdateMode = ActionUpdateModes.ScaledDeltaTime;
            Assert.IsTrue(tween.IsTimeScaleIndependent(), "旧入口不得改写调用方已有 independent 配置。");

            DOTween.ManualUpdate(0.25f, 0.25f);
            Assert.Greater(value, 0f, "旧入口必须保留调用方已有 Manual 更新阶段。");
        }

        /// <summary>验证显式时序入口使用指定 Manual 阶段，并随 controller 切换 scaled/unscaled。</summary>
        [Test]
        public void ManagedTimingEntryUsesExplicitPhaseAndControllerTimeSource()
        {
            float value = 0f;
            Tween tween = DOTween.To(() => value, current => value = current, 1f, 1f)
                .SetUpdate(UpdateType.Fixed, true);
            IActionController controller = tween.ToAction(UpdateType.Manual).Start();

            Assert.IsFalse(tween.IsTimeScaleIndependent(), "显式入口默认应同步 ScaledDeltaTime。");
            controller.UpdateMode = ActionUpdateModes.UnscaledDeltaTime;
            Assert.IsTrue(tween.IsTimeScaleIndependent());
            DOTween.ManualUpdate(0.25f, 0.25f);
            Assert.Greater(value, 0f, "显式入口必须把 Tween 配置为调用方指定的 Manual 阶段。");
            controller.UpdateMode = ActionUpdateModes.ScaledDeltaTime;
            Assert.IsFalse(tween.IsTimeScaleIndependent());
        }

        /// <summary>验证取消默认执行 Kill(false) 且不补齐终值。</summary>
        [Test]
        public void CancelKillsTweenWithoutCompleting()
        {
            float value = 0f;
            bool killInvoked = false;
            Tween tween = DOTween.To(() => value, current => value = current, 1f, 1f)
                .SetUpdate(UpdateType.Manual)
                .OnKill(() => killInvoked = true);
            IAction action = tween.ToAction();
            IActionController controller = action.Start();

            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCompleted, "取消请求应在 Tick 内进入终态。");
            Assert.IsTrue(controller.IsCancelled, "取消终态必须保持 IsCancelled。");
            Assert.IsTrue(action.Deinited, "取消终态必须执行 Action OnDeinit。");
            Assert.IsNull(((DOTweenAction)action).CurrentTween, "OnDeinit 必须释放 Tween 强引用。");
            Assert.IsTrue(killInvoked, "取消释放必须调用 DOTween Kill(false)。");
            Assert.IsFalse(tween.IsActive(), "Kill(false) 后 Tween 不应继续活动。");
            Assert.Less(value, 1f, "取消不应补齐 Tween 终值。");
        }

        /// <summary>验证取消 Sequence 时会终止尚未进入 OnStart 的暂停 Tween。</summary>
        [Test]
        public void CancelKillsTweenBeforeItsSequenceTurn()
        {
            bool killInvoked = false;
            Tween tween = DOTween.To(static () => 0f, static _ => { }, 1f, 1f)
                .SetUpdate(UpdateType.Manual)
                .OnKill(() => killInvoked = true);
            IActionController controller = ActionKit.Sequence().Delay(1f).DOTween(tween).Start();

            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCancelled);
            Assert.IsTrue(killInvoked, "startup 前取消也必须调用 DOTween Kill(false)。");
            Assert.IsFalse(tween.IsActive());
        }

        /// <summary>验证正常完成不会误杀由调用方配置为可复用的 Tween。</summary>
        [Test]
        public void NaturalCompletionKeepsReusableTweenActive()
        {
            float value = 0f;
            Tween tween = DOTween.To(() => value, current => value = current, 1f, 1f)
                .SetUpdate(UpdateType.Manual)
                .SetAutoKill(false);
            IActionController controller = tween.ToAction().Start();

            tween.ManualUpdate(1f, 1f);
            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCompleted);
            Assert.IsTrue(tween.IsComplete());
            Assert.IsTrue(tween.IsActive(), "正常完成不得 Kill 可复用 Tween。");
        }

        /// <summary>验证 Repeat 上一轮正常钩子不会让下一轮取消错误跳过 Kill(false)。</summary>
        [Test]
        public void RepeatCancellationKillsTweenAfterPreviousRoundWasExternallyFinished()
        {
            bool killInvoked = false;
            Tween tween = DOTween.To(static () => 0f, static _ => { }, 1f, 1f)
                .SetUpdate(UpdateType.Manual)
                .SetAutoKill(false)
                .OnKill(() => killInvoked = true);
            DOTweenAction action = new(tween);
            IRepeat repeat = ActionKit.Repeat(2);
            repeat.Append(action);
            IActionController controller = repeat.Start();

            action.Finish();
            ActionKitScheduler.Tick(0f, 0f);
            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCancelled);
            Assert.IsTrue(killInvoked, "新一轮取消必须清理仍活动的 Tween。");
            Assert.IsFalse(tween.IsActive());
        }

        /// <summary>验证公开 new 的实例释放后不会成为内部 ToAction 池的后续租约。</summary>
        [Test]
        public void PubliclyConstructedActionDoesNotEnterInternalPool()
        {
            Tween publicTween = DOTween.To(static () => 0f, static _ => { }, 1f, 1f)
                .SetUpdate(UpdateType.Manual);
            DOTweenAction publicAction = new(publicTween, UpdateType.Manual);
            Assert.IsFalse(publicAction.IsPoolOwned);
            IActionController controller = publicAction.Start();

            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);
            ActionKitScheduler.ProcessRecycle();

            Tween pooledTween = DOTween.To(static () => 0f, static _ => { }, 1f, 1f)
                .SetUpdate(UpdateType.Manual);
            DOTweenAction pooledAction = (DOTweenAction)pooledTween.ToAction(UpdateType.Manual);
            Assert.IsTrue(pooledAction.IsPoolOwned);
            Assert.AreNotSame(publicAction, pooledAction);
            ActionKitScheduler.DiscardUnscheduled(pooledAction);
            ActionKitScheduler.ProcessRecycle();
        }

        /// <summary>验证空 Sequence 参数在分配包装 Action 前明确失败。</summary>
        [Test]
        public void DotweenExtensionRejectsNullSequence()
        {
            Tween tween = DOTween.To(static () => 0f, static _ => { }, 1f, 1f)
                .SetUpdate(UpdateType.Manual);

            Assert.Throws<System.ArgumentNullException>(() =>
                DOTweenActionExtensions.DOTween(null, tween));
        }

        /// <summary>验证空 Tween 在进入 PoolKit 分配路径前明确失败。</summary>
        [Test]
        public void DotweenActionRejectsNullTween()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                DOTweenActionExtensions.ToAction(null));
        }

    }
}
#endif
