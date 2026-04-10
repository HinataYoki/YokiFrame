#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Aggregates multiple disposables and manages them as a single lifecycle unit.
    /// </summary>
    public sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> mDisposables;
        private bool mIsDisposed;

        /// <summary>
        /// Creates a composite container with the desired initial capacity.
        /// </summary>
        public CompositeDisposable(int capacity = 8)
        {
            mDisposables = new List<IDisposable>(capacity);
        }

        /// <summary>
        /// Current number of tracked disposables.
        /// </summary>
        public int Count => mDisposables.Count;

        /// <summary>
        /// Whether the composite itself has been disposed.
        /// </summary>
        public bool IsDisposed => mIsDisposed;

        /// <summary>
        /// Adds a disposable to the composite.
        /// </summary>
        /// <remarks>
        /// If the composite has already been disposed, the incoming disposable is disposed immediately.
        /// </remarks>
        public void Add(IDisposable disposable)
        {
            if (disposable is null) return;

            if (mIsDisposed)
            {
                disposable.Dispose();
                return;
            }

            mDisposables.Add(disposable);
        }

        /// <summary>
        /// Removes and disposes a tracked disposable.
        /// </summary>
        /// <returns><see langword="true"/> when the item existed and was removed.</returns>
        public bool Remove(IDisposable disposable)
        {
            if (disposable is null || mIsDisposed) return false;

            var removed = mDisposables.Remove(disposable);
            if (removed)
            {
                disposable.Dispose();
            }

            return removed;
        }

        /// <summary>
        /// Disposes all tracked items without marking the composite itself as disposed.
        /// </summary>
        /// <remarks>
        /// New disposables can still be added after calling this method.
        /// </remarks>
        public void Clear()
        {
            if (mIsDisposed) return;

            for (int i = mDisposables.Count - 1; i >= 0; i--)
            {
                mDisposables[i]?.Dispose();
            }

            mDisposables.Clear();
        }

        /// <summary>
        /// Disposes all tracked items and permanently closes the composite.
        /// </summary>
        public void Dispose()
        {
            if (mIsDisposed) return;
            Clear();
            mIsDisposed = true;
        }
    }
}
#endif
