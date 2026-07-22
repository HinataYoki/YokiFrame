#if UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>把 Unity 原生 Coroutine yield 语义映射到 ActionKit 生命周期。</summary>
    internal sealed class UnityCoroutineAction : ActionBase, IEnumerator, IDisposable, IPooledAction
    {
        private static readonly ObjectPool<UnityCoroutineAction> sPool = PoolKit.Create(
            static () => new UnityCoroutineAction(), null, static action => action.ResetForPool(), ActionPoolSettings.Default);
        private Func<IEnumerator> mEnumeratorFactory;
        private IEnumerator mEnumerator;
        private UnityCoroutineRunner mRunner;
        private Coroutine mCoroutine;
        private ExceptionDispatchInfo mPendingFailure;
        private object mCurrent;
        private bool mCompleted;
        private bool mNativeRunning;
        private bool mRoundStarted;
        private bool mClosingRound;
        private bool mDirectEnumerator;
        private bool mDirectEnumeratorConsumed;
        private bool mEnumeratorDisposed;

        /// <summary>限制实例只能由静态 Allocate 和 Core PoolKit 创建。</summary>
        private UnityCoroutineAction() { }

        /// <summary>分配每轮调用 factory 的 Unity Coroutine Action。</summary>
        /// <param name="enumeratorFactory">每轮创建 Unity IEnumerator 的 factory。</param>
        /// <returns>新的 Unity Coroutine Action 租约。</returns>
        internal static UnityCoroutineAction Allocate(Func<IEnumerator> enumeratorFactory)
        {
            if (enumeratorFactory == null) throw new ArgumentNullException(nameof(enumeratorFactory));
            UnityCoroutineAction action = AllocateCore();
            action.mEnumeratorFactory = enumeratorFactory;
            return action;
        }

        /// <summary>直接包装一个一次性 Unity IEnumerator，不创建捕获闭包。</summary>
        /// <param name="enumerator">待交给 Unity Coroutine 的一次性枚举器。</param>
        /// <returns>新的 Unity Coroutine Action 租约。</returns>
        internal static UnityCoroutineAction Allocate(IEnumerator enumerator)
        {
            if (enumerator == null) throw new ArgumentNullException(nameof(enumerator));
            UnityCoroutineAction action = AllocateCore();
            action.mEnumerator = enumerator;
            action.mDirectEnumerator = true;
            return action;
        }

        /// <summary>获取当前底层 IEnumerator 产出的 Unity yield 值。</summary>
        public object Current => mCurrent;

        /// <summary>Repeat 新一轮前停止并释放上一轮原生 Coroutine，禁止跨轮继续回调。</summary>
        public override void OnInit()
        {
            base.OnInit();
            if (!mRoundStarted) return;
            CloseRound();
            mRoundStarted = false;
        }

        /// <summary>创建枚举器并提交 Unity Coroutine；同步 MoveNext 故障会在当前 Start 内形成 Faulted。</summary>
        public override void OnStart()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Unity Coroutine Action can only start while Unity is playing.");
            if (mDirectEnumerator)
            {
                if (mDirectEnumeratorConsumed)
                    throw new InvalidOperationException(
                        "A direct Unity IEnumerator can only be consumed once; use a factory inside Repeat.");
                mDirectEnumeratorConsumed = true;
            }
            else
            {
                mEnumerator = mEnumeratorFactory();
            }

            if (mEnumerator == null) throw new InvalidOperationException("Unity Coroutine factory returned null.");
            mEnumeratorDisposed = false;
            mCompleted = false;
            mRoundStarted = true;
            StartNativeCoroutine();
            ObserveTerminal();
        }

        /// <summary>宿主 Tick 只观察 Unity Coroutine 写入的完成或异常状态。</summary>
        /// <param name="dt">Unity Coroutine 不直接消费的 ActionKit 时间步长。</param>
        public override void OnExecute(float dt) => ObserveTerminal();

        /// <summary>取消、故障或宿主重置时停止 Coroutine，并可靠 Dispose 一次底层枚举器。</summary>
        public override void OnDeinit()
        {
            CloseRound();
            mEnumeratorFactory = null;
            mDirectEnumerator = false;
            mDirectEnumeratorConsumed = false;
            mRoundStarted = false;
        }

#if UNITY_EDITOR
        /// <summary>创建不访问 Unity Coroutine 内部状态的按需诊断摘要。</summary>
        /// <returns>当前原生 Coroutine 是否仍在运行。</returns>
        public override string GetDebugInfo() => "UnityCoroutineAction(running=" + mNativeRunning + ")";
#endif

        /// <summary>由 Unity Coroutine 推进底层 IEnumerator，并捕获用户异常供 ActionKit Tick 终结。</summary>
        /// <returns>底层枚举器仍有下一步时返回 true。</returns>
        public bool MoveNext()
        {
            if (mCompleted || mPendingFailure != null || mEnumerator == null) return false;
            try
            {
                if (mEnumerator.MoveNext())
                {
                    mCurrent = mEnumerator.Current;
                    return true;
                }

                mNativeRunning = false;
                mCurrent = null;
                ReleaseEnumerator();
                mCompleted = true;
                return false;
            }
            catch (Exception exception)
            {
                CaptureFailure(exception);
                mNativeRunning = false;
                mCurrent = null;
                ReleaseEnumerator();
                return false;
            }
        }

        /// <summary>Unity IEnumerator 不支持重置，Repeat 必须通过 factory 创建新实例。</summary>
        public void Reset() => throw new NotSupportedException();

        /// <summary>接收 Unity 可能触发的 Dispose，并把清理异常延迟交给 ActionKit Tick。</summary>
        public void Dispose()
        {
            if (!mClosingRound && mRoundStarted && !mCompleted && mPendingFailure == null)
            {
                CaptureFailure(new InvalidOperationException(
                    "Unity Coroutine stopped outside the ActionKit lifecycle."));
            }
            mNativeRunning = false;
            ReleaseEnumerator();
        }

        /// <summary>把完成清理的租约交还零保留 PoolKit。</summary>
        void IPooledAction.ReturnToPool() => sPool.Recycle(this);

        /// <summary>从局部 PoolKit 分配实例并安装新的 Action ID。</summary>
        /// <returns>已重置公共租约状态的 Action。</returns>
        private static UnityCoroutineAction AllocateCore()
        {
            UnityCoroutineAction action = sPool.Allocate();
            action.PreparePooled(ActionKitScheduler.NextActionId());
            return action;
        }

        /// <summary>取得 MonoSingleton Runner 并启动当前 Action 自身作为无额外包装分配的 IEnumerator。</summary>
        private void StartNativeCoroutine()
        {
            mRunner = UnityCoroutineRunner.Instance;
            if (mRunner == null) throw new InvalidOperationException("Unity Coroutine runner is unavailable.");

            mNativeRunning = true;
            Coroutine coroutine = mRunner.Run(this);
            if (mNativeRunning && coroutine == null)
            {
                mNativeRunning = false;
                throw new InvalidOperationException("Unity Coroutine runner returned no handle.");
            }
            if (mNativeRunning) mCoroutine = coroutine;
        }

        /// <summary>将原生 Coroutine 记录的异常或完成转换为 ActionKit 生命周期信号。</summary>
        private void ObserveTerminal()
        {
            if (mNativeRunning && mRunner == null)
            {
                mNativeRunning = false;
                throw new InvalidOperationException("Unity Coroutine runner was destroyed while the Action was active.");
            }

            ExceptionDispatchInfo failure = mPendingFailure;
            if (failure != null)
            {
                mPendingFailure = null;
                failure.Throw();
            }

            if (mCompleted) this.Finish();
        }

        /// <summary>停止 Unity Coroutine、释放枚举器并把清理异常抛给统一终态边界。</summary>
        private void CloseRound()
        {
            mClosingRound = true;
            try
            {
                StopNativeCoroutine();
                ReleaseEnumerator();
                mCurrent = null;
                mCompleted = false;
                mNativeRunning = false;
            }
            finally { mClosingRound = false; }

            ExceptionDispatchInfo failure = mPendingFailure;
            mPendingFailure = null;
            if (failure != null) failure.Throw();
        }

        /// <summary>停止仍活动的 Unity Coroutine handle；Runner 已销毁时只清除本地租约。</summary>
        private void StopNativeCoroutine()
        {
            Coroutine coroutine = mCoroutine;
            mCoroutine = null;
            if (mRunner != null && coroutine != null) mRunner.Stop(coroutine);
            mRunner = null;
            mNativeRunning = false;
        }

        /// <summary>最多一次 Dispose 底层枚举器，并保存首个异常供 ActionKit 终结。</summary>
        private void ReleaseEnumerator()
        {
            if (mEnumeratorDisposed) return;
            mEnumeratorDisposed = true;
            IEnumerator enumerator = mEnumerator;
            mEnumerator = null;
            if (!(enumerator is IDisposable disposable)) return;
            try { disposable.Dispose(); }
            catch (Exception exception) { CaptureFailure(exception); }
        }

        /// <summary>保留当前轮首个异常，后续清理异常不得覆盖直接故障原因。</summary>
        /// <param name="exception">用户 MoveNext 或 Dispose 抛出的异常。</param>
        private void CaptureFailure(Exception exception)
        {
            if (mPendingFailure == null)
                mPendingFailure = ExceptionDispatchInfo.Capture(exception);
        }

        /// <summary>回池前清除 Unity 对象、枚举器、factory 与终态引用。</summary>
        private void ResetForPool()
        {
            mEnumeratorFactory = null;
            mEnumerator = null;
            mRunner = null;
            mCoroutine = null;
            mPendingFailure = null;
            mCurrent = null;
            mCompleted = false;
            mNativeRunning = false;
            mRoundStarted = false;
            mClosingRound = false;
            mDirectEnumerator = false;
            mDirectEnumeratorConsumed = false;
            mEnumeratorDisposed = false;
        }
    }
}
#endif
