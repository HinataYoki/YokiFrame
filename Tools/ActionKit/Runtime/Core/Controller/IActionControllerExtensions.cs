namespace YokiFrame
{
    /// <summary>
    /// <see cref="IActionController"/> 扩展方法。
    /// </summary>
    public static class IActionControllerExtensions
    {
        /// <summary>
        /// 取消控制器。
        /// </summary>
        /// <param name="self">目标控制器。</param>
        public static void Cancel(this IActionController self)
        {
            self?.Cancel();
        }

        /// <summary>
        /// 暂停控制器。
        /// </summary>
        /// <param name="self">目标控制器。</param>
        public static IActionController Pause(this IActionController self)
        {
            if (self != null)
                self.Paused = true;
            return self;
        }

        /// <summary>
        /// 恢复控制器。
        /// </summary>
        /// <param name="self">目标控制器。</param>
        public static IActionController Resume(this IActionController self)
        {
            if (self != null)
                self.Paused = false;
            return self;
        }

        /// <summary>
        /// 切换控制器暂停状态。
        /// </summary>
        /// <param name="self">目标控制器。</param>
        public static IActionController TogglePause(this IActionController self)
        {
            if (self != null)
                self.Paused = !self.Paused;
            return self;
        }
    }
}
