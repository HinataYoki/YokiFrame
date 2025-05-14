using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    internal class MonoUpdateExecutor : MonoBehaviour, IActionExecutor
    {
        /// <summary>
        /// 准备执行的任务队列
        /// </summary>
        private readonly List<IActionController> mPrepareExecutionActions = new();
        /// <summary>
        /// 正在执行的任务队列
        /// </summary>
        private readonly Dictionary<IAction, IActionController> mExecutingActions = new();
        /// <summary>
        /// 已经完成的任务队列
        /// </summary>
        private readonly List<IActionController> mToActionRemove = new();

        public void Execute(IActionController controller)
        {
            //防止获取的任务中有失败的执行结果
            if (controller.Action.ActionState == ActionStatus.Finished) controller.Action.OnInit();

            if (this.UpdateAction(controller, 0)) return;

            mPrepareExecutionActions.Add(controller);
        }

        private void Update()
        {
            //从准备队列移入执行队列
            if (mPrepareExecutionActions.Count > 0)
            {
                foreach (var prepareAction in mPrepareExecutionActions)
                {
                    if (mExecutingActions.ContainsKey(prepareAction.Action))
                    {
                        mExecutingActions[prepareAction.Action] = prepareAction;
                    }
                    else
                    {
                        mExecutingActions.Add(prepareAction.Action, prepareAction);
                    }
                }
                mPrepareExecutionActions.Clear();
            }

            var dt = Time.deltaTime;
            var unDt = Time.unscaledDeltaTime;

            //执行队列
            foreach (var exeute in mExecutingActions.Values)
            {
                if (this.UpdateAction(exeute, exeute.UpdateMode is ActionUpdateModes.ScaledDeltaTime ? dt : unDt))
                {
                    mToActionRemove.Add(exeute);
                }
            }

            //将完成的队列移出
            if (mToActionRemove.Count > 0)
            {
                foreach (var controller in mToActionRemove)
                {
                    mExecutingActions.Remove(controller.Action);
                    controller.Recycle();
                }

                mToActionRemove.Clear();
            }
        }
    }

    public static class MonoUpdateActionExecutorExtension
    {
        public static void ExecuteByUpdate<T>(this T self, IActionController controller) where T : MonoBehaviour
        {
            if (controller.Action.ActionState == ActionStatus.Finished) controller.Action.OnInit();
            self.gameObject.GetOrAddComponent<MonoUpdateExecutor>().Execute(controller);
        }

        public static T GetOrAddComponent<T>(this GameObject self) where T : Component
        {
            T comp = self.GetComponent<T>();
            return comp ? comp : self.AddComponent<T>();
        }
    }
}