using System;

namespace YokiFrame
{
    public class ActionController : IActionController
    {
        private static readonly SimpleObjectPool<IActionController> controllerPool = new(() => new ActionController(),
            controller =>
            {
                controller.UpdateMode = ActionUpdateModes.ScaledDeltaTime;
                controller.CurExcuteActionID = 0;
                controller.Action = null;
                controller.Finish = null;
            });

        public static IActionController Allocate() => controllerPool.Allocate() as ActionController;

        public ulong CurExcuteActionID { get; set; }
        public IAction Action { get; set; }
        public Action<IActionController> Finish { get; set; }
        public ActionUpdateModes UpdateMode { get; set; }
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

        public void Recycle()
        {
            controllerPool.Recycle(this);
        }
    }
}