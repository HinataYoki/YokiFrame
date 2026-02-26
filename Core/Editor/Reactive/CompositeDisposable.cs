#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 组合 Disposable - 统一管理多个订阅的生命周期
    /// 适用于需要同时清理多个订阅的场景
    /// </summary>
    public sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> mDisposables;
        private bool mIsDisposed;

        public CompositeDisposable(int capacity = 8)
        {
            mDisposables = new(capacity);
        }

        /// <summary>
        /// 当前管理的订阅数量
        /// </summary>
        public int Count => mDisposables.Count;

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed => mIsDisposed;

        /// <summary>
        /// 添加订阅到管理列表
        /// 如果已释放，则立即释放传入的订阅
        /// </summary>
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
        /// 移除并释放指定订阅
        /// </summary>
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
        /// 清空所有订阅（释放但不标记为已释放）
        /// 可继续添加新订阅
        /// </summary>
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
        /// 释放所有订阅并标记为已释放
        /// 之后添加的订阅会被立即释放
        /// </summary>
        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;
            Clear();
        }
    }
}
#endif
