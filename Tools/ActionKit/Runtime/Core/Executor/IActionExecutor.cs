namespace YokiFrame
{
    public interface IActionExecutor
    {
        void Execute(IActionController controller);
    }

    public static class IActionExecutorExtensions
    {
        /// <summary>
        /// 更新任务
        /// </summary>
        public static bool UpdateAction(this IActionExecutor self, IActionController controller, float dt)
        {
            if (!controller.Action.Deinited && controller.Action.Update(dt))
            {
                controller.Finish?.Invoke(controller);
                controller.OnEnd();
                return true;
            }

            return controller.Action.Deinited;
        }
    }
}