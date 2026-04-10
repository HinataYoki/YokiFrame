#if UNITY_EDITOR
using System;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// No-op disposable singleton used when no cleanup is required.
    /// </summary>
    public sealed class EmptyDisposable : IDisposable
    {
        /// <summary>
        /// Shared singleton instance.
        /// </summary>
        public static readonly EmptyDisposable Instance = new();

        private EmptyDisposable() { }

        /// <summary>
        /// Performs no action.
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// Disposable wrapper that executes a delegate once.
    /// </summary>
    public sealed class ActionDisposable : IDisposable
    {
        private Action mDisposeAction;
        private bool mIsDisposed;

        /// <summary>
        /// Creates a delegate-backed disposable.
        /// </summary>
        public ActionDisposable(Action disposeAction)
        {
            mDisposeAction = disposeAction;
        }

        /// <summary>
        /// Executes the stored cleanup delegate once.
        /// </summary>
        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;
            mDisposeAction?.Invoke();
            mDisposeAction = null;
        }
    }

    /// <summary>
    /// Helper factory for common disposable patterns.
    /// </summary>
    public static class Disposable
    {
        /// <summary>
        /// Shared no-op disposable.
        /// </summary>
        public static IDisposable Empty => EmptyDisposable.Instance;

        /// <summary>
        /// Creates a disposable that runs the supplied cleanup action.
        /// </summary>
        public static IDisposable Create(Action disposeAction)
        {
            if (disposeAction is null) return Empty;
            return new ActionDisposable(disposeAction);
        }
    }
}
#endif
