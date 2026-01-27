namespace YokiFrame
{
    /// <summary>
    /// IActionController 扩展方法
    /// </summary>
    public static class IActionControllerExtensions
    {
        /// <summary>
        /// 取消 Action（提前终止）
        /// </summary>
        public static void Cancel(this IActionController self)
        {
            self?.Cancel();
        }

        /// <summary>
        /// 暂停 Action
        /// </summary>
        public static IActionController Pause(this IActionController self)
        {
            if (self != default)
            {
                self.Paused = true;
            }
            return self;
        }

        /// <summary>
        /// 恢复 Action
        /// </summary>
        public static IActionController Resume(this IActionController self)
        {
            if (self != default)
            {
                self.Paused = false;
            }
            return self;
        }

        /// <summary>
        /// 切换暂停状态
        /// </summary>
        public static IActionController TogglePause(this IActionController self)
        {
            if (self != default)
            {
                self.Paused = !self.Paused;
            }
            return self;
        }
    }
}
