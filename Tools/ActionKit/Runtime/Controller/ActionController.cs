using System;

namespace YokiFrame
{
    public class ActionController : IActionController
    {
        private static readonly SimplePoolKit<IActionController> controllerPool = new(() => new ActionController(),
            controller =>
            {
                controller.UpdateMode = ActionUpdateModes.ScaledDeltaTime;
                controller.CurExcuteActionID = 0;
                controller.Action = null;
                controller.Finish = null;
                ((ActionController)controller).mIsCancelled = false;
            });

        public static IActionController Allocate() => controllerPool.Allocate();

        private bool mIsCancelled;

        public ulong CurExcuteActionID { get; set; }
        public IAction Action { get; set; }
        public Action<IActionController> Finish { get; set; }
        public ActionUpdateModes UpdateMode { get; set; }
        public bool IsCancelled => mIsCancelled;
        public bool Paused
        {
            get => Action.Paused;
            set => Action.Paused = value;
        }

        public void OnStart()
        {
            //ID验证防止任务重新使用时不匹配
            if (Action.ActionID == CurExcuteActionID)
            {
                Action.OnInit();
            }
        }

        public void OnEnd()
        {
            if (Action != null && Action.ActionID == CurExcuteActionID)
            {
                Action.OnDeinit();
            }
        }

        public void Cancel()
        {
            if (mIsCancelled) return;
            mIsCancelled = true;
            ActionKitPlayerLoopSystem.CancelAction(this);
        }

        public void Recycle()
        {
            controllerPool.Recycle(this);
        }
    }
}