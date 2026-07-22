using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace YokiFrame
{
    /// <summary>
    /// 每次宿主 Tick 推进 IEnumerator，并把嵌套 IEnumerator 作为子调用栈处理。
    /// </summary>
    internal sealed class CoroutineAction : ActionBase, IPooledAction
    {
        private const int MAX_NESTED_DEPTH = 64;
        private static readonly ObjectPool<CoroutineAction> sPool = PoolKit.Create(
            static () => new CoroutineAction(), null, static action => action.ResetForPool(), ActionPoolSettings.Default);
        private readonly List<IEnumerator> mStack = new(4);
        private Func<IEnumerator> mEnumeratorFactory;
        private IEnumerator mEnumerator;
        private bool mUsesDirectEnumerator;
        private bool mDirectEnumeratorConsumed;

        /// <summary>限制实例只能由静态 Allocate 和 PoolKit 创建。</summary>
        private CoroutineAction() { }

        /// <summary>
        /// 分配一个延迟创建 IEnumerator 的 Action。
        /// </summary>
        /// <param name="enumeratorFactory">首次执行时创建枚举器的委托。</param>
        /// <returns>新的 CoroutineAction 租约。</returns>
        internal static CoroutineAction Allocate(Func<IEnumerator> enumeratorFactory)
        {
            if (enumeratorFactory == null) throw new ArgumentNullException(nameof(enumeratorFactory));
            CoroutineAction action = AllocateCore();
            action.mEnumeratorFactory = enumeratorFactory;
            action.mUsesDirectEnumerator = false;
            action.mDirectEnumeratorConsumed = false;
            return action;
        }

        /// <summary>
        /// 直接包装已有 IEnumerator，不创建捕获闭包。
        /// </summary>
        /// <param name="enumerator">待推进枚举器。</param>
        /// <returns>新的 CoroutineAction 租约。</returns>
        internal static CoroutineAction Allocate(IEnumerator enumerator)
        {
            if (enumerator == null) throw new ArgumentNullException(nameof(enumerator));
            CoroutineAction action = AllocateCore();
            action.mEnumerator = enumerator;
            action.mUsesDirectEnumerator = true;
            action.mDirectEnumeratorConsumed = false;
            return action;
        }

        /// <summary>Repeat 新一轮前关闭上一轮未结束的枚举器；直接枚举器首次初始化除外。</summary>
        public override void OnInit()
        {
            base.OnInit();
            if (mUsesDirectEnumerator && !mDirectEnumeratorConsumed) return;
            Exception disposeException = ReleaseEnumerators();
            if (disposeException != null) ExceptionDispatchInfo.Capture(disposeException).Throw();
        }

        /// <summary>创建或采用枚举器，并在 Start 调用内推进第一步。</summary>
        public override void OnStart()
        {
            if (mEnumerator == null)
            {
                if (mUsesDirectEnumerator && mDirectEnumeratorConsumed)
                {
                    this.Finish();
                    return;
                }

                mEnumerator = mEnumeratorFactory();
            }
            if (mEnumerator == null) throw new InvalidOperationException("Coroutine factory returned null.");
            if (mUsesDirectEnumerator) mDirectEnumeratorConsumed = true;
            if (Advance()) this.Finish();
        }

        /// <summary>每个后续 Tick 推进一步，忽略非 IEnumerator yield 值的具体类型。</summary>
        public override void OnExecute(float dt)
        {
            if (Advance()) this.Finish();
        }

        /// <summary>取消、故障或完成后释放当前及父枚举器，保证 iterator finally 得到执行。</summary>
        public override void OnDeinit()
        {
            Exception disposeException = ReleaseEnumerators();
            mEnumeratorFactory = null;
            mUsesDirectEnumerator = false;
            mDirectEnumeratorConsumed = false;
            if (disposeException != null) ExceptionDispatchInfo.Capture(disposeException).Throw();
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>创建当前嵌套深度和 factory 目标按需诊断文本。</summary>
        public override string GetDebugInfo()
        {
            if (mEnumeratorFactory == null) return "Coroutine(depth=" + mStack.Count + ")";
            return "Coroutine -> " + mEnumeratorFactory.Method.DeclaringType + "." + mEnumeratorFactory.Method.Name;
        }
#endif

        /// <summary>把已释放实例归还 CoroutineAction 局部池。</summary>
        void IPooledAction.ReturnToPool() => sPool.Recycle(this);

        /// <summary>从局部池分配公共状态，并安装新的运行 ID。</summary>
        private static CoroutineAction AllocateCore()
        {
            CoroutineAction action = sPool.Allocate();
            action.PreparePooled(ActionKitScheduler.NextActionId());
            return action;
        }

        /// <summary>推进当前枚举器，连续展开嵌套枚举器并在父级完成后恢复调用栈。</summary>
        private bool Advance()
        {
            while (mEnumerator != null)
            {
                if (!mEnumerator.MoveNext())
                {
                    IEnumerator completed = mEnumerator;
                    bool hasParent = TryPopParent();
                    DisposeEnumerator(completed);
                    if (!hasParent) return true;
                    continue;
                }

                if (!(mEnumerator.Current is IEnumerator nested)) return false;
                PushNested(nested);
            }

            return true;
        }

        /// <summary>恢复最近父枚举器；调用栈为空时表示整个 Coroutine 已结束。</summary>
        private bool TryPopParent()
        {
            if (mStack.Count == 0)
            {
                mEnumerator = null;
                return false;
            }

            int lastIndex = mStack.Count - 1;
            mEnumerator = mStack[lastIndex];
            mStack.RemoveAt(lastIndex);
            return true;
        }

        /// <summary>验证嵌套深度与环后压入当前枚举器。</summary>
        private void PushNested(IEnumerator nested)
        {
            if (nested == null) return;
            if (ReferenceEquals(nested, mEnumerator) || ContainsInStack(nested))
                throw new InvalidOperationException("Coroutine contains a cyclic nested IEnumerator.");
            if (mStack.Count >= MAX_NESTED_DEPTH)
            {
                Exception disposeException = null;
                TryDisposeEnumerator(nested, ref disposeException);
                throw new InvalidOperationException(
                    "Coroutine nested IEnumerator depth exceeds the supported limit.",
                    disposeException);
            }

            mStack.Add(mEnumerator);
            mEnumerator = nested;
        }

        /// <summary>按引用检查候选枚举器是否已经在父调用栈中。</summary>
        private bool ContainsInStack(IEnumerator candidate)
        {
            for (var index = 0; index < mStack.Count; index++)
                if (ReferenceEquals(mStack[index], candidate)) return true;
            return false;
        }

        /// <summary>释放实现 IDisposable 的枚举器；异常交给 Scheduler 转换为 Faulted。</summary>
        private static void DisposeEnumerator(IEnumerator enumerator)
        {
            if (!(enumerator is IDisposable disposable)) return;
            disposable.Dispose();
        }

        /// <summary>关闭当前枚举器和全部父栈，保留首个异常并继续释放其余资源。</summary>
        /// <returns>首个 Dispose 异常；全部成功时返回 null。</returns>
        private Exception ReleaseEnumerators()
        {
            Exception firstException = null;
            TryDisposeEnumerator(mEnumerator, ref firstException);
            mEnumerator = null;
            for (var index = mStack.Count - 1; index >= 0; index--)
                TryDisposeEnumerator(mStack[index], ref firstException);
            mStack.Clear();
            return firstException;
        }

        /// <summary>尽力释放单个枚举器，并把首个异常返回给统一清理终态。</summary>
        private static void TryDisposeEnumerator(IEnumerator enumerator, ref Exception firstException)
        {
            try
            {
                DisposeEnumerator(enumerator);
            }
            catch (Exception exception)
            {
                if (firstException == null) firstException = exception;
                ActionKitFailureReporter.TryLog("[ActionKit] IEnumerator.Dispose failed: ", exception);
            }
        }

        /// <summary>回池前清除枚举器、factory 和嵌套栈引用。</summary>
        private void ResetForPool()
        {
            mEnumeratorFactory = null;
            mEnumerator = null;
            mUsesDirectEnumerator = false;
            mDirectEnumeratorConsumed = false;
            mStack.Clear();
            if (mStack.Capacity > MAX_NESTED_DEPTH) mStack.Capacity = MAX_NESTED_DEPTH;
        }
    }
}
