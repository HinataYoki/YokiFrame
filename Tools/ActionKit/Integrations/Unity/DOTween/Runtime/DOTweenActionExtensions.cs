#if UNITY_5_3_OR_NEWER && YOKIFRAME_DOTWEEN_SUPPORT
using DG.Tweening;

namespace YokiFrame
{
    /// <summary>提供 DOTween 与 ActionKit 的公开链式扩展。</summary>
    public static class DOTweenActionExtensions
    {
        /// <summary>将 Tween 包装为由 ActionKit 控制生命周期的动作。</summary>
        /// <param name="tween">待接管并立即暂停的补间。</param>
        /// <param name="killOnCancel">Action 非正常释放时是否执行 Kill(false)。</param>
        /// <returns>可加入组合器或直接 Start 的 Action。</returns>
        public static IAction ToAction(this Tween tween, bool killOnCancel = true)
        {
            return DOTweenAction.Allocate(tween, killOnCancel);
        }

        /// <summary>将 Tween 包装为由 ActionKit 显式管理更新阶段和时间源的动作。</summary>
        /// <param name="tween">待接管并立即暂停的补间。</param>
        /// <param name="updateType">ActionKit 后续切换时间源时必须保留的 DOTween 更新阶段。</param>
        /// <param name="killOnCancel">Action 非正常释放时是否执行 Kill(false)。</param>
        /// <returns>可加入组合器或直接 Start 的 Action。</returns>
        public static IAction ToAction(
            this Tween tween,
            UpdateType updateType,
            bool killOnCancel = true)
        {
            return DOTweenAction.Allocate(tween, updateType, killOnCancel);
        }

        /// <summary>向 Sequence 追加一个由 ActionKit 接管的 Tween。</summary>
        /// <param name="sequence">目标顺序容器。</param>
        /// <param name="tween">待接管并立即暂停的补间。</param>
        /// <param name="killOnCancel">Action 非正常释放时是否执行 Kill(false)。</param>
        /// <returns>原 Sequence，供链式继续装配。</returns>
        public static ISequence DOTween(
            this ISequence sequence,
            Tween tween,
            bool killOnCancel = true)
        {
            if (sequence == null)
            {
                throw new System.ArgumentNullException(nameof(sequence));
            }

            DOTweenAction action = DOTweenAction.Allocate(tween, killOnCancel);
            return ActionRuntime.AppendCreated(sequence, action);
        }

        /// <summary>向 Sequence 追加由 ActionKit 显式管理更新阶段和时间源的 Tween。</summary>
        /// <param name="sequence">目标顺序容器。</param>
        /// <param name="tween">待接管并立即暂停的补间。</param>
        /// <param name="updateType">ActionKit 后续切换时间源时必须保留的 DOTween 更新阶段。</param>
        /// <param name="killOnCancel">Action 非正常释放时是否执行 Kill(false)。</param>
        /// <returns>原 Sequence，供链式继续装配。</returns>
        public static ISequence DOTween(
            this ISequence sequence,
            Tween tween,
            UpdateType updateType,
            bool killOnCancel = true)
        {
            if (sequence == null)
            {
                throw new System.ArgumentNullException(nameof(sequence));
            }

            DOTweenAction action = DOTweenAction.Allocate(tween, updateType, killOnCancel);
            return ActionRuntime.AppendCreated(sequence, action);
        }
    }
}
#endif
