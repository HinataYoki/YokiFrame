#if UNITY_EDITOR
using System;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 空 Disposable 单例 - 用于表示无需清理的订阅
    /// </summary>
    public sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();
        private EmptyDisposable() { }
        public void Dispose() { }
    }

    /// <summary>
    /// 委托 Disposable - 执行指定的清理动作
    /// </summary>
    public sealed class ActionDisposable : IDisposable
    {
        private Action mDisposeAction;
        private bool mIsDisposed;

        public ActionDisposable(Action disposeAction)
        {
            mDisposeAction = disposeAction;
        }

        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;
            mDisposeAction?.Invoke();
            mDisposeAction = null;
        }
    }

    /// <summary>
    /// Disposable 工具类
    /// </summary>
    public static class Disposable
    {
        /// <summary>
        /// 空 Disposable 单例
        /// </summary>
        public static IDisposable Empty => EmptyDisposable.Instance;

        /// <summary>
        /// 创建执行指定动作的 Disposable
        /// </summary>
        public static IDisposable Create(Action disposeAction)
        {
            if (disposeAction is null) return Empty;
            return new ActionDisposable(disposeAction);
        }
    }
}
#endif
