using System;
using System.Collections;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// ActionKit 静态门面，提供所有 Action 的工厂方法。
    /// </summary>
    public class ActionKit
    {
        internal static ulong sIdGenerator;

        /// <summary>
        /// 静态构造函数，初始化跨引擎调度器。
        /// </summary>
        static ActionKit()
        {
            ActionKitScheduler.Initialize();
        }

        /// <summary>
        /// 创建顺序执行容器。
        /// </summary>
        public static ISequence Sequence() => YokiFrame.Sequence.Allocate();

        /// <summary>
        /// 创建并行执行容器。
        /// </summary>
        /// <param name="waitAll">为 true 时等待所有子 Action 完成；为 false 时任一子 Action 完成即结束。</param>
        public static IParallel Parallel(bool waitAll = true) => YokiFrame.Parallel.Allocate(waitAll);

        /// <summary>
        /// 创建重复执行容器。
        /// </summary>
        /// <param name="repeatCount">重复次数；小于等于 0 表示不限制次数。</param>
        /// <param name="condition">继续重复的条件；为空时仅受重复次数限制。</param>
        public static IRepeat Repeat(int repeatCount = -1, Func<bool> condition = null) => YokiFrame.Repeat.Allocate(repeatCount, condition);

        /// <summary>
        /// 创建按秒延迟的 Action。
        /// </summary>
        /// <param name="seconds">延迟秒数。</param>
        /// <param name="callback">延迟完成时调用的回调。</param>
        public static IAction Delay(float seconds, Action callback) => YokiFrame.Delay.Allocate(seconds, callback);

        /// <summary>
        /// 创建按帧数延迟的 Action。
        /// </summary>
        /// <param name="frameCount">需要等待的帧数。</param>
        /// <param name="onDelayFinish">延迟完成时调用的回调。</param>
        public static IAction DelayFrame(int frameCount, Action onDelayFinish) => YokiFrame.DelayFrame.Allocate(frameCount, onDelayFinish);

        /// <summary>
        /// 创建下一帧执行的 Action。
        /// </summary>
        /// <param name="onNextFrame">下一帧调用的回调。</param>
        public static IAction NextFrame(Action onNextFrame) => YokiFrame.DelayFrame.Allocate(1, onNextFrame);

        /// <summary>
        /// 创建浮点插值 Action。
        /// </summary>
        /// <param name="a">起始值。</param>
        /// <param name="b">目标值。</param>
        /// <param name="duration">插值持续时间。</param>
        /// <param name="onLerp">每次更新时接收当前插值的回调。</param>
        /// <param name="onLerpFinish">插值完成时调用的回调。</param>
        public static IAction Lerp(float a, float b, float duration, Action<float> onLerp, Action onLerpFinish = null) =>
            YokiFrame.Lerp.Allocate(a, b, duration, onLerp, onLerpFinish);

        /// <summary>
        /// 创建立即调用回调并完成的 Action。
        /// </summary>
        /// <param name="callback">执行时调用的回调。</param>
        public static IAction Callback(Action callback) => YokiFrame.Callback.Allocate(callback);

        /// <summary>
        /// 创建由 <see cref="IEnumerator"/> 驱动的 Action。
        /// </summary>
        /// <param name="coroutineGetter">返回枚举器的委托。</param>
        public static IAction Coroutine(Func<IEnumerator> coroutineGetter) => CoroutineAction.Allocate(coroutineGetter);

        /// <summary>
        /// 创建由 <see cref="System.Threading.Tasks.Task"/> 驱动的 Action。
        /// </summary>
        /// <param name="taskGetter">返回任务的委托。</param>
        public static IAction Task(Func<Task> taskGetter) => TaskAction.Allocate(taskGetter);
    }
}
