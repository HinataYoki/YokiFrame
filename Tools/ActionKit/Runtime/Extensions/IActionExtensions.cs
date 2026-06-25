using System;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="IAction"/> 的调度扩展方法。
    /// </summary>
    public static class IActionExtensions
    {
        /// <summary>
        /// 启动 Action，具体宿主由 Adapter 驱动 ActionKitScheduler。
        /// </summary>
        /// <param name="self">要启动的 Action。</param>
        /// <param name="onFinish">Action 完成后调用的回调。</param>
        public static IActionController Start(this IAction self, Action<IActionController> onFinish = null)
        {
            var controller = ActionController.Allocate();
            controller.CurExecuteActionID = self.ActionID;
            controller.Action = self;
            controller.UpdateMode = ActionUpdateModes.ScaledDeltaTime;
            controller.Finish = onFinish;

            if (ActionStackTraceService.Enabled)
                ActionStackTraceService.Register(self.ActionID, new System.Diagnostics.StackTrace(1, true));

            ActionKitScheduler.Execute(controller);
            return controller;
        }

        /// <summary>
        /// 执行一次 Action 更新。
        /// </summary>
        /// <param name="self">要更新的 Action。</param>
        /// <param name="dt">本次更新的时间步长。</param>
        public static bool Update(this IAction self, float dt)
        {
            if (self.Paused) return false;
            try
            {
                switch (self.ActionState)
                {
                    case ActionStatus.NotStart:
                        self.OnStart();
                        if (self.ActionState == ActionStatus.Finished)
                        {
                            self.OnFinish();
                            ActionEditorHooks.OnActionFinished?.Invoke(self);
                            return true;
                        }
                        self.ActionState = ActionStatus.Started;
                        ActionEditorHooks.OnActionStarted?.Invoke(self);
                        break;
                    case ActionStatus.Started:
                        self.OnExecute(dt);
                        if (self.ActionState == ActionStatus.Finished)
                        {
                            self.OnFinish();
                            ActionEditorHooks.OnActionFinished?.Invoke(self);
                            return true;
                        }
                        break;
                    case ActionStatus.Finished:
                        self.OnFinish();
                        ActionEditorHooks.OnActionFinished?.Invoke(self);
                        return true;
                }
            }
            catch (Exception e)
            {
                ActionKitRuntimeLog.Error("[ActionKit] " + self.GetDebugInfo() + " 执行出错: " + e.Message);
            }
            return false;
        }

        /// <summary>
        /// 将 Action 标记为完成。
        /// </summary>
        /// <param name="self">要标记完成的 Action。</param>
        public static void Finish(this IAction self) => self.ActionState = ActionStatus.Finished;
    }
}
