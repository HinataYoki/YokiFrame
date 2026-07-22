namespace YokiFrame
{
    /// <summary>
    /// 提供 controller 的 null-safe fluent 控制入口。
    /// </summary>
    public static class IActionControllerExtensions
    {
        /// <summary>对非空 controller 请求取消。</summary>
        /// <param name="self">目标 controller；null 时保持无操作。</param>
        public static void Cancel(this IActionController self) => self?.Cancel();

        /// <summary>
        /// 在 Scheduler 宿主线程暂停非空且仍活动的 controller。
        /// </summary>
        /// <param name="self">目标 controller。</param>
        /// <returns>原 controller。</returns>
        public static IActionController Pause(this IActionController self)
        {
            if (self != null) self.Paused = true;
            return self;
        }

        /// <summary>
        /// 在 Scheduler 宿主线程恢复非空且仍活动的 controller。
        /// </summary>
        /// <param name="self">目标 controller。</param>
        /// <returns>原 controller。</returns>
        public static IActionController Resume(this IActionController self)
        {
            if (self != null) self.Paused = false;
            return self;
        }

        /// <summary>
        /// 在 Scheduler 宿主线程切换非空且仍活动的 controller 暂停状态。
        /// </summary>
        /// <param name="self">目标 controller。</param>
        /// <returns>原 controller。</returns>
        public static IActionController TogglePause(this IActionController self)
        {
            if (self != null) self.Paused = !self.Paused;
            return self;
        }
    }
}
