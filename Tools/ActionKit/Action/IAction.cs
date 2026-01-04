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
        ulong ActionID { get; }
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
        bool Deinited { get; }
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
        void OnStart();
        /// <summary>
        /// 任务执行
        /// </summary>
        void OnExecute(float dt);
        /// <summary>
        /// 任务结束
        /// </summary>
        void OnFinish();
        /// <summary>
        /// 获取调试信息（用于错误日志）
        /// </summary>
        string GetDebugInfo() => GetType().Name;
    }

    /// <summary>
    /// Action 抽象基类，处理公共状态和初始化逻辑
    /// </summary>
    public abstract class ActionBase : IAction
    {
        public ulong ActionID { get; protected set; }
        public ActionStatus ActionState { get; set; }
        public bool Paused { get; set; }
        public bool Deinited { get; protected set; }

        public virtual void OnInit()
        {
            ActionState = ActionStatus.NotStart;
            Paused = false;
        }

        public abstract void OnDeinit();
        public virtual void OnStart() { }
        public virtual void OnExecute(float dt) { }
        public virtual void OnFinish() { }

        /// <summary>
        /// 获取调试信息，子类可重写提供更详细的错误信息
        /// </summary>
        public virtual string GetDebugInfo() => GetType().Name;
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
            catch (Exception e)
            {
                // 直接调用接口方法，避免类型检查
                KitLogger.Error($"[ActionKit] {self.GetDebugInfo()} 执行出错: {e.Message}");
            }
            return false;
        }

        /// <summary>
        /// 任务完成
        /// </summary>
        public static void Finish(this IAction self) => self.ActionState = ActionStatus.Finished;
    }
}