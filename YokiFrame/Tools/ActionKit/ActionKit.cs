using System;
using System.Collections;
using System.Threading.Tasks;

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
    }
}