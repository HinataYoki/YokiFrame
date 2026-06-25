#if !GODOT && YOKIFRAME_DOTWEEN_SUPPORT
using DG.Tweening;

namespace YokiFrame
{
    /// <summary>
    /// 将 DOTween 补间包装为 ActionKit 动作。
    /// </summary>
    public sealed class DOTweenAction : ActionBase
    {
        private static ulong sIdGenerator = 1UL << 48;

        private Tween mTween;
        private bool mKillOnCancel = true;

        /// <summary>
        /// 创建 DOTween Action。
        /// </summary>
        /// <param name="tween">要等待的 DOTween 补间。</param>
        /// <param name="killOnCancel">Action 回收时是否终止补间。</param>
        public DOTweenAction(Tween tween, bool killOnCancel = true)
        {
            ActionID = sIdGenerator++;
            mTween = tween;
            mKillOnCancel = killOnCancel;
            OnInit();
        }

        public override void OnStart()
        {
            if (mTween == null || !mTween.IsActive() || mTween.IsComplete())
            {
                this.Finish();
                return;
            }

            if (!mTween.IsPlaying())
                mTween.Play();
        }

        public override void OnExecute(float dt)
        {
            if (mTween == null || !mTween.IsActive() || mTween.IsComplete())
                this.Finish();
        }

        public override void OnDeinit()
        {
            if (Deinited) return;

            if (mKillOnCancel && mTween != null && mTween.IsActive() && !mTween.IsComplete())
                mTween.Kill(false);

            mTween = null;
            Deinited = true;
        }

        public override string GetDebugInfo()
        {
            if (mTween == null)
                return "DOTweenAction(null)";

            return "DOTweenAction(" + mTween.GetType().Name + ")";
        }
    }

    /// <summary>
    /// DOTween 与 ActionKit 的链式扩展。
    /// </summary>
    public static class DOTweenActionExtensions
    {
        /// <summary>
        /// 将 DOTween 补间包装为 ActionKit 动作。
        /// </summary>
        public static IAction ToAction(this Tween self, bool killOnCancel = true)
        {
            return new DOTweenAction(self, killOnCancel);
        }

        /// <summary>
        /// 向序列中追加一个 DOTween 补间动作。
        /// </summary>
        public static ISequence DOTween(this ISequence self, Tween tween, bool killOnCancel = true)
        {
            return self.Append(new DOTweenAction(tween, killOnCancel));
        }
    }
}
#endif
