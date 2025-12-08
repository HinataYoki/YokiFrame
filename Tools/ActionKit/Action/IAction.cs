using System;
using UnityEngine;

namespace YokiFrame
{
    public enum ActionStatus
    {
        NotStart,
        Started,
        Finished,
    }

    public interface IAction
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        ulong ActionID { get; set; }
        /// <summary>
        /// 任务状态
        /// </summary>
        ActionStatus ActionState { get; set; }
        /// <summary>
        /// 是否处于暂停
        /// </summary>
        bool Paused { get; set; }
        /// <summary>
        /// 是否被回收
        /// </summary>
        bool Deinited { get; set; }
        /// <summary>
        /// 任务重置
        /// </summary>
        void OnInit();
        /// <summary>
        /// 任务回收
        /// </summary>
        void OnDeinit();
        /// <summary>
        /// 任务开始
        /// </summary>
        virtual void OnStart() { }
        /// <summary>
        /// 任务执行
        /// </summary>
        /// <param name="dt"></param>
        virtual void OnExecute(float dt) { }
        /// <summary>
        /// 任务结束
        /// </summary>
        virtual void OnFinish() { }
        /// <summary>
        /// 报错
        /// </summary>
        string LogError();
    }

    public static class IActionExtensions
    {
        public static IActionController Start(this IAction self, MonoBehaviour monoBehaviour, Action<IActionController> onFinish = null)
        {
            var controller = ActionController.Allocate();
            controller.CurExcuteActionID = self.ActionID;
            controller.Action = self;
            controller.UpdateMode = ActionUpdateModes.ScaledDeltaTime;
            controller.Finish = onFinish;
            monoBehaviour.ExecuteByUpdate(controller);
            return controller;
        }

        /// <summary>
        /// 任务执行
        /// </summary>
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
                            return true;
                        }
                        self.ActionState = ActionStatus.Started;
                        break;
                    case ActionStatus.Started:
                        self.OnExecute(dt);
                        if (self.ActionState == ActionStatus.Finished)
                        {
                            self.OnFinish();
                            return true;
                        }
                        break;
                    case ActionStatus.Finished:
                        self.OnFinish();
                        return true;
                }
            }
            catch
            {
                KitLogger.LogError<ActionKit>(self.LogError());
            }
            return false;
        }

        /// <summary>
        /// 任务完成
        /// </summary>
        /// <param name="self"></param>
        public static void Finish(this IAction self) => self.ActionState = ActionStatus.Finished;
    }
}