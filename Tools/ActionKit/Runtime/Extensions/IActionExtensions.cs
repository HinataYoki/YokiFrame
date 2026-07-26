using System;

namespace YokiFrame
{
    /// <summary>
    /// 提供 IAction 的启动、手动推进和完成标记入口。
    /// </summary>
    public static class IActionExtensions
    {
        /// <summary>
        /// 在 Scheduler 宿主线程创建稳定 controller，并同步执行一次 dt=0 首推。
        /// </summary>
        /// <param name="self">待启动根 Action。</param>
        /// <param name="onFinish">仅在正常完成时调用的 controller 回调。</param>
        /// <returns>不会被复用给其它动作的 controller handle。</returns>
        public static IActionController Start(this IAction self, Action<IActionController> onFinish = null)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return ActionKitScheduler.Execute(self, onFinish);
        }

        /// <summary>
        /// 在 Scheduler 宿主线程手动推进单个 Action；自定义宿主通常应驱动 Scheduler，而不是逐个调用本方法。
        /// </summary>
        /// <param name="self">待推进 Action。</param>
        /// <param name="dt">本次推进秒数。</param>
        /// <returns>当前 Action 正常完成时返回 true。</returns>
        public static bool Update(this IAction self, float dt)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            if (dt < 0f || float.IsNaN(dt) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            ActionKitScheduler.EnsureHostThread();
            ActionOwnership.EnsureCanManuallyUpdate(self);
            if (self is ActionBase actionBase && self.ActionID == 0)
                actionBase.PrepareExecution(ActionKitScheduler.NextActionId());
            else if (!(self is ActionBase) && self.ActionID == 0)
                throw new InvalidOperationException("A custom IAction that does not derive from ActionBase must provide a non-zero ActionID.");
            return ActionKitScheduler.UpdateDetachedAction(self, dt);
        }

        /// <summary>
        /// 将当前 Action 标记为正常完成；Scheduler 会在本次推进返回后调用 OnFinish。
        /// </summary>
        /// <param name="self">待完成 Action。</param>
        public static void Finish(this IAction self)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            self.ActionState = ActionStatus.Finished;
        }
    }
}
