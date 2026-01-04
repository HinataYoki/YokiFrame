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
                for (int i = 0; i < mPrepareExecutionActions.Count; i++)
                {
                    var prepareAction = mPrepareExecutionActions[i];
                    // 使用 TryAdd 避免 ContainsKey + Add/索引器 的重复查询
                    if (!mExecutingActions.TryAdd(prepareAction.Action, prepareAction))
                    {
                        mExecutingActions[prepareAction.Action] = prepareAction;
                    }
                }
                mPrepareExecutionActions.Clear();
            }

            var dt = Time.deltaTime;
            var unDt = Time.unscaledDeltaTime;

            // 使用结构体枚举器避免 foreach 对 Dictionary.Values 的装箱
            var enumerator = mExecutingActions.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var execute = enumerator.Current.Value;
                if (this.UpdateAction(execute, execute.UpdateMode is ActionUpdateModes.ScaledDeltaTime ? dt : unDt))
                {
                    mToActionRemove.Add(execute);
                }
            }
            enumerator.Dispose();

            //将完成的队列移出
            if (mToActionRemove.Count > 0)
            {
                for (int i = 0; i < mToActionRemove.Count; i++)
                {
                    var controller = mToActionRemove[i];
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
    }
}