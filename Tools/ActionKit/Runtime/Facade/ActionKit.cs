using System;
using System.Collections;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// 提供 ActionKit 基础动作与组合容器的静态创建入口。
    /// </summary>
    public static class ActionKit
    {
        /// <summary>首次使用门面时把 Scheduler 注册到 Core FrameLoop。</summary>
        static ActionKit() => ActionKitScheduler.Initialize();

        /// <summary>创建空顺序容器。</summary>
        /// <returns>可继续 fluent 装配的 Sequence。</returns>
        public static ISequence Sequence() => YokiFrame.Sequence.Allocate();

        /// <summary>
        /// 创建空并行容器。
        /// </summary>
        /// <param name="waitAll">true 等待全部分支；false 任一分支完成即结束。</param>
        /// <returns>可继续 fluent 装配的 Parallel。</returns>
        public static IParallel Parallel(bool waitAll = true) => YokiFrame.Parallel.Allocate(waitAll);

        /// <summary>
        /// 创建空重复容器。
        /// </summary>
        /// <param name="repeatCount">目标轮数；小于等于零表示无限。</param>
        /// <param name="condition">每轮结束后决定是否继续的条件。</param>
        /// <returns>可继续 fluent 装配的 Repeat。</returns>
        public static IRepeat Repeat(int repeatCount = -1, Func<bool> condition = null) =>
            YokiFrame.Repeat.Allocate(repeatCount, condition);

        /// <summary>
        /// 创建秒级延迟。
        /// </summary>
        /// <param name="seconds">目标等待秒数。</param>
        /// <param name="callback">正常完成时调用的回调。</param>
        /// <returns>新的 Delay Action。</returns>
        public static IAction Delay(float seconds, Action callback = null) => YokiFrame.Delay.Allocate(seconds, callback);

        /// <summary>
        /// 创建帧级延迟。
        /// </summary>
        /// <param name="frameCount">需要跨过的实际调度帧数。</param>
        /// <param name="onDelayFinish">正常完成时调用的回调。</param>
        /// <returns>新的 DelayFrame Action。</returns>
        public static IAction DelayFrame(int frameCount, Action onDelayFinish = null) =>
            YokiFrame.DelayFrame.Allocate(frameCount, onDelayFinish);

        /// <summary>
        /// 创建下一调度帧完成的 Action。
        /// </summary>
        /// <param name="onNextFrame">正常完成时调用的回调。</param>
        /// <returns>等待一帧的 DelayFrame Action。</returns>
        public static IAction NextFrame(Action onNextFrame = null) => YokiFrame.DelayFrame.Allocate(1, onNextFrame);

        /// <summary>
        /// 创建 float 线性插值 Action。
        /// </summary>
        /// <param name="a">起始值。</param>
        /// <param name="b">目标值。</param>
        /// <param name="duration">持续秒数。</param>
        /// <param name="onLerp">每次输出当前值的回调。</param>
        /// <param name="onLerpFinish">正常完成回调。</param>
        /// <returns>新的 Lerp Action。</returns>
        public static IAction Lerp(
            float a,
            float b,
            float duration,
            Action<float> onLerp,
            Action onLerpFinish = null) => YokiFrame.Lerp.Allocate(a, b, duration, onLerp, onLerpFinish);

        /// <summary>
        /// 创建立即执行的回调 Action。
        /// </summary>
        /// <param name="callback">首次执行时调用的委托；null 表示空操作。</param>
        /// <returns>新的 Callback Action。</returns>
        public static IAction Callback(Action callback) => YokiFrame.Callback.Allocate(callback);

        /// <summary>
        /// 创建条件满足后完成的 Action。
        /// </summary>
        /// <param name="condition">每次调度检查的条件。</param>
        /// <returns>新的 Condition Action。</returns>
        public static IAction Condition(Func<bool> condition) => YokiFrame.Condition.Allocate(condition);

        /// <summary>
        /// 创建由 factory 提供 IEnumerator 的 Action。
        /// </summary>
        /// <param name="coroutineGetter">首次执行时创建枚举器的委托。</param>
        /// <returns>新的 CoroutineAction。</returns>
        public static IAction Coroutine(Func<IEnumerator> coroutineGetter) => CoroutineAction.Allocate(coroutineGetter);

        /// <summary>
        /// 直接包装已有 IEnumerator，不创建捕获闭包。
        /// </summary>
        /// <param name="enumerator">待推进枚举器。</param>
        /// <returns>新的 CoroutineAction。</returns>
        public static IAction Coroutine(IEnumerator enumerator) => CoroutineAction.Allocate(enumerator);

        /// <summary>
        /// 创建由 factory 提供 Task 的 Action。
        /// </summary>
        /// <param name="taskGetter">首次执行时创建 Task 的委托。</param>
        /// <returns>新的 TaskAction。</returns>
        public static IAction Task(Func<Task> taskGetter) => TaskAction.Allocate(taskGetter);

        /// <summary>
        /// 直接包装已有 Task，不创建捕获闭包。
        /// </summary>
        /// <param name="task">待观察 Task。</param>
        /// <returns>新的 TaskAction。</returns>
        public static IAction Task(Task task) => TaskAction.Allocate(task);
    }
}
