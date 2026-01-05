using System;
using System.Collections;
using System.Threading.Tasks;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    public class ActionKit
    {
        internal static ulong ID_GENERATOR = 0;

        public static ISequence Sequence()
        {
            return YokiFrame.Sequence.Allocate();
        }

        public static IParallel Parallel(bool waitAll = true)
        {
            return YokiFrame.Parallel.Allocate(waitAll);
        }

        public static IRepeat Repeat(int repeatCount = -1, Func<bool> condition = null)
        {
            return YokiFrame.Repeat.Allocate(repeatCount, condition);
        }

        public static IAction Delay(float seconds, Action callback)
        {
            return YokiFrame.Delay.Allocate(seconds, callback);
        }

        public static IAction DelayFrame(int frameCount, Action onDelayFinish)
        {
            return YokiFrame.DelayFrame.Allocate(frameCount, onDelayFinish);
        }

        public static IAction NextFrame(Action onNextFrame)
        {
            return YokiFrame.DelayFrame.Allocate(1, onNextFrame);
        }

        public static IAction Lerp(float a, float b, float duration, Action<float> onLerp, Action onLerpFinish = null)
        {
            return YokiFrame.Lerp.Allocate(a, b, duration, onLerp, onLerpFinish);
        }

        public static IAction Callback(Action callback)
        {
            return YokiFrame.Callback.Allocate(callback);
        }

        public static IAction Coroutine(Func<IEnumerator> coroutineGetter)
        {
            return YokiFrame.Coroutine.Allocate(coroutineGetter);
        }

        public static IAction Task(Func<Task> taskGetter)
        {
            return TaskAction.Allocate(taskGetter);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// [UniTask] 创建 UniTask Action
        /// </summary>
        public static IAction UniTask(Func<UniTask> taskGetter)
        {
            return UniTaskAction.Allocate(taskGetter);
        }

        /// <summary>
        /// [UniTask] 创建支持取消的 UniTask Action
        /// </summary>
        public static IAction UniTask(Func<CancellationToken, UniTask> taskGetter)
        {
            return UniTaskAction.Allocate(taskGetter);
        }

        /// <summary>
        /// [UniTask] 延迟指定秒数（使用 UniTask.Delay）
        /// </summary>
        public static IAction DelayUniTask(float seconds, Action callback = null, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            return UniTaskAction.Allocate(async ct =>
            {
                await Cysharp.Threading.Tasks.UniTask.Delay((int)(seconds * 1000), delayTiming: timing, cancellationToken: ct);
                callback?.Invoke();
            });
        }

        /// <summary>
        /// [UniTask] 延迟指定帧数（使用 UniTask.DelayFrame）
        /// </summary>
        public static IAction DelayFrameUniTask(int frameCount, Action callback = null, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            return UniTaskAction.Allocate(async ct =>
            {
                await Cysharp.Threading.Tasks.UniTask.DelayFrame(frameCount, timing, ct);
                callback?.Invoke();
            });
        }

        /// <summary>
        /// [UniTask] 等待下一帧
        /// </summary>
        public static IAction NextFrameUniTask(Action callback = null)
        {
            return DelayFrameUniTask(1, callback);
        }

        /// <summary>
        /// [UniTask] 等待直到条件为真
        /// </summary>
        public static IAction WaitUntil(Func<bool> predicate, Action callback = null, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            return UniTaskAction.Allocate(async ct =>
            {
                await Cysharp.Threading.Tasks.UniTask.WaitUntil(predicate, timing, ct);
                callback?.Invoke();
            });
        }

        /// <summary>
        /// [UniTask] 等待直到条件为假
        /// </summary>
        public static IAction WaitWhile(Func<bool> predicate, Action callback = null, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            return UniTaskAction.Allocate(async ct =>
            {
                await Cysharp.Threading.Tasks.UniTask.WaitWhile(predicate, timing, ct);
                callback?.Invoke();
            });
        }
#endif
    }
}