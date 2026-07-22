#if UNITY_5_3_OR_NEWER && YOKIFRAME_DOTWEEN_SUPPORT
using System;
using System.Runtime.CompilerServices;
using DG.Tweening;

[assembly: InternalsVisibleTo("YokiFrame.ActionKit.DOTween.Tests")]

namespace YokiFrame
{
    /// <summary>将 DOTween 补间作为 ActionKit 动作接管，并同步控制器生命周期。</summary>
    public sealed class DOTweenAction : ActionBase, IPooledAction
    {
        private static readonly ObjectPool<DOTweenAction> sPool = PoolKit.Create(
            static () => new DOTweenAction(),
            null,
            static action => action.ResetForPool(),
            ActionPoolSettings.Default);
        private Tween mTween;
        private UpdateType mUpdateType;
        private bool mKillOnCancel;
        private bool mStarted;
        private bool mFinishedNormally;
        private bool mManageTiming;
        private bool mPoolOwned;

        /// <summary>获取当前实例是否由内部 PoolKit 分配，仅供程序集内验证所有权边界。</summary>
        internal bool IsPoolOwned => mPoolOwned;

        /// <summary>限制实例只能由静态 Allocate 和 PoolKit 创建。</summary>
        private DOTweenAction() { }

        /// <summary>
        /// 创建兼容 2.0-pre 公开面的 DOTween Action，并立即暂停尚未完成的 Tween。
        /// </summary>
        /// <param name="tween">待接管的补间。</param>
        /// <param name="killOnCancel">Action 取消、故障或宿主重置时是否终止补间。</param>
        public DOTweenAction(Tween tween, bool killOnCancel = true)
        {
            Configure(tween, UpdateType.Normal, killOnCancel, TimingControl.PreserveTween);
        }

        /// <summary>
        /// 创建由 ActionKit 管理更新阶段和时间源的 DOTween Action，并立即暂停尚未完成的 Tween。
        /// </summary>
        /// <param name="tween">待接管的补间。</param>
        /// <param name="updateType">ActionKit 后续切换时间源时必须保留的 DOTween 更新阶段。</param>
        /// <param name="killOnCancel">Action 取消、故障或宿主重置时是否终止补间。</param>
        public DOTweenAction(Tween tween, UpdateType updateType, bool killOnCancel = true)
        {
            Configure(tween, updateType, killOnCancel, TimingControl.ActionKit);
        }

        /// <summary>接管 Tween 并立即暂停，避免它在 Sequence 尚未轮到时提前播放。</summary>
        /// <param name="tween">待接管的补间。</param>
        /// <param name="killOnCancel">Action 取消、故障或宿主重置时是否终止补间。</param>
        /// <returns>新的 DOTween Action 租约。</returns>
        internal static DOTweenAction Allocate(Tween tween, bool killOnCancel)
        {
            return Allocate(tween, UpdateType.Normal, killOnCancel, TimingControl.PreserveTween);
        }

        /// <summary>从内部池分配由 ActionKit 显式管理更新阶段的 DOTween Action。</summary>
        /// <param name="tween">待接管的补间。</param>
        /// <param name="updateType">需要保留的 DOTween 更新阶段。</param>
        /// <param name="killOnCancel">非正常终态是否终止补间。</param>
        /// <returns>新的池化 DOTween Action 租约。</returns>
        internal static DOTweenAction Allocate(Tween tween, UpdateType updateType, bool killOnCancel)
        {
            return Allocate(tween, updateType, killOnCancel, TimingControl.ActionKit);
        }

        /// <summary>重置每一轮的正常完成标记，避免 Repeat 后续取消沿用上一轮终态。</summary>
        public override void OnInit()
        {
            base.OnInit();
            mStarted = false;
            mFinishedNormally = false;
        }

        /// <summary>轮到当前动作时启动仍有效且未完成的 Tween。</summary>
        public override void OnStart()
        {
            if (!CanContinue())
            {
                this.Finish();
                return;
            }

            mStarted = true;
            mTween.Play();
        }

        /// <summary>轮询 DOTween 自身状态；时间推进仍由 DOTween 的宿主更新承担。</summary>
        /// <param name="dt">ActionKit 时间步长；DOTween 不直接消费该值。</param>
        public override void OnExecute(float dt)
        {
            if (!CanContinue())
            {
                this.Finish();
            }
        }

        /// <summary>ActionKit 暂停时同步暂停仍活动的 Tween。</summary>
        public override void OnPause()
        {
            if (mStarted && CanContinue())
            {
                mTween.Pause();
            }
        }

        /// <summary>ActionKit 恢复时继续播放仍活动的 Tween。</summary>
        public override void OnResume()
        {
            if (mStarted && CanContinue())
            {
                mTween.Play();
            }
        }

        /// <summary>记录 ActionKit 已按正常完成路径观察到 Tween 终态。</summary>
        public override void OnFinish()
        {
            mFinishedNormally = true;
        }

        /// <summary>仅对显式托管时序的入口映射 controller 时间源，并保留调用方指定的更新阶段。</summary>
        /// <param name="updateMode">新的 ActionKit 时间源。</param>
        public override void OnUpdateModeChanged(ActionUpdateModes updateMode)
        {
            if (mManageTiming && CanContinue())
            {
                mTween.SetUpdate(
                    mUpdateType,
                    updateMode == ActionUpdateModes.UnscaledDeltaTime);
            }
        }

        /// <summary>释放 Tween 引用；仅在取消、故障或宿主重置时终止仍活动的补间。</summary>
        public override void OnDeinit()
        {
            Tween tween = mTween;
            bool shouldKill = mKillOnCancel
                && !mFinishedNormally
                && tween != null
                && tween.IsActive();
            mTween = null;
            mUpdateType = UpdateType.Normal;
            mKillOnCancel = true;
            mStarted = false;
            mFinishedNormally = false;
            mManageTiming = false;

            if (shouldKill)
            {
                // 旧版 DOTween 对尚未 startup 的暂停 Tween 可能忽略 Kill，先强制初始化再终止。
                tween.ForceInit();
                if (tween.IsActive()) tween.Kill(false);
            }
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>创建不触发 Tween 回调的按需诊断摘要。</summary>
        /// <returns>当前 Tween 类型与活动状态。</returns>
        public override string GetDebugInfo()
        {
            return mTween == null
                ? "DOTweenAction(null)"
                : "DOTweenAction(" + mTween.GetType().Name + ", active=" + mTween.IsActive() + ")";
        }
#endif

        /// <summary>池内租约归还 PoolKit；公开构造实例只清理状态，绝不混入内部池。</summary>
        void IPooledAction.ReturnToPool()
        {
            if (mPoolOwned)
            {
                sPool.Recycle(this);
                return;
            }

            ResetForPool();
        }

        /// <summary>判断当前 Tween 是否仍需由 ActionKit 等待或控制。</summary>
        /// <returns>Tween 存在、活动且未完成时返回 true。</returns>
        private bool CanContinue()
        {
            return mTween != null && mTween.IsActive() && !mTween.IsComplete();
        }

        /// <summary>从内部池分配并安装指定的时序所有权策略。</summary>
        /// <param name="tween">待接管的补间。</param>
        /// <param name="updateType">显式托管时需要保留的更新阶段。</param>
        /// <param name="killOnCancel">非正常终态是否终止补间。</param>
        /// <param name="timingControl">调用方与 ActionKit 之间的时序所有权。</param>
        /// <returns>新的池化 DOTween Action 租约。</returns>
        private static DOTweenAction Allocate(
            Tween tween,
            UpdateType updateType,
            bool killOnCancel,
            TimingControl timingControl)
        {
            if (tween == null) throw new ArgumentNullException(nameof(tween));
            DOTweenAction action = sPool.Allocate();
            try
            {
                action.mPoolOwned = true;
                action.Configure(tween, updateType, killOnCancel, timingControl);
                return action;
            }
            catch
            {
                try { sPool.Recycle(action); }
                catch (Exception exception)
                {
                    ActionKitFailureReporter.TryLog("[ActionKit] DOTween allocation rollback failed: ", exception);
                }
                throw;
            }
        }

        /// <summary>安装新租约；旧入口保留现有配置，显式入口通过 DOTween 公开 API 托管时序。</summary>
        /// <param name="tween">待接管的补间。</param>
        /// <param name="updateType">显式托管时需要写入的更新阶段。</param>
        /// <param name="killOnCancel">非正常终态是否终止补间。</param>
        /// <param name="timingControl">调用方与 ActionKit 之间的时序所有权。</param>
        private void Configure(
            Tween tween,
            UpdateType updateType,
            bool killOnCancel,
            TimingControl timingControl)
        {
            if (tween == null) throw new ArgumentNullException(nameof(tween));

            PreparePooled(ActionKitScheduler.NextActionId());
            mTween = tween;
            mUpdateType = updateType;
            mKillOnCancel = killOnCancel;
            mStarted = false;
            mFinishedNormally = false;
            mManageTiming = timingControl == TimingControl.ActionKit;
            if (!tween.IsActive() || tween.IsComplete()) return;

            if (mManageTiming) tween.SetUpdate(updateType, false);
            tween.Pause();
        }

        /// <summary>回池前清除 Tween 与取消策略。</summary>
        private void ResetForPool()
        {
            mTween = null;
            mUpdateType = UpdateType.Normal;
            mKillOnCancel = true;
            mStarted = false;
            mFinishedNormally = false;
            mManageTiming = false;
            mPoolOwned = false;
        }

        /// <summary>区分保留调用方 Tween 时序与由 ActionKit 显式接管时序。</summary>
        private enum TimingControl
        {
            /// <summary>不改写 Tween 的更新阶段或 independent 配置。</summary>
            PreserveTween,

            /// <summary>按显式 UpdateType 和 controller 时间源管理 Tween。</summary>
            ActionKit,
        }
    }
}
#endif
