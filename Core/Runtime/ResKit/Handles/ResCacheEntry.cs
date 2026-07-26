using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>持有一个缓存 key 的底层资源、所有者 Provider 和全部活动 lease。</summary>
    internal sealed class ResCacheEntry
    {
        private readonly List<ResLease> mLeases = new();

        /// <summary>创建尚未被调用方获取的缓存条目，并固化底层资源的释放所有者。</summary>
        internal ResCacheEntry(
            ResCacheKey key,
            object asset,
            IResourceProvider provider
#if UNITY_EDITOR || (GODOT && TOOLS)
            ,
            string providerName)
#else
            )
#endif
        {
            Key = key;
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
#if UNITY_EDITOR || (GODOT && TOOLS)
            ProviderName = providerName ?? string.Empty;
#endif
            IsValid = true;
        }

        internal ResCacheKey Key { get; }
        internal object Asset { get; private set; }
        internal IResourceProvider Provider { get; private set; }
#if UNITY_EDITOR || (GODOT && TOOLS)
        internal string ProviderName { get; }
        internal long ProviderGeneration { get; private set; }
#endif
        internal int RefCount { get; private set; }
        internal bool IsValid { get; private set; }
#if UNITY_EDITOR || (GODOT && TOOLS)
        internal IReadOnlyList<ResLease> Leases => mLeases;
#endif

        /// <summary>创建一次独立获取并增加条目总引用数；调用方必须持有 ResKit 状态锁。</summary>
        internal ResLease Acquire(bool anonymous)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Cannot acquire an invalid ResKit cache entry.");
            }

            ResLease lease = new(this, anonymous);
            mLeases.Add(lease);
            RefCount++;
            return lease;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>记录创建当前条目的 Provider 代次，仅供 Editor/Tools 诊断快照读取。</summary>
        /// <param name="providerGeneration">创建条目时的 Provider 代次。</param>
        internal void SetProviderGeneration(long providerGeneration)
        {
            ProviderGeneration = providerGeneration;
        }
#endif

        /// <summary>释放指定 lease 的一次引用，并在该 lease 归零时移出活动集合。</summary>
        internal bool TryRelease(ResLease lease)
        {
            if (!IsValid || lease == null || !lease.TryDecrement())
            {
                return false;
            }

            RefCount--;
            if (lease.Count == 0)
            {
                mLeases.Remove(lease);
            }

            return true;
        }

        /// <summary>寻找引用指定对象的活动 lease；可限定为匿名对象式获取。</summary>
        internal ResLease FindObjectReleaseLease(object asset, bool anonymousOnly)
        {
            if (!IsValid || !ReferenceEquals(Asset, asset))
            {
                return null;
            }

            for (var index = 0; index < mLeases.Count; index++)
            {
                ResLease lease = mLeases[index];
                if (lease.Count > 0 && (!anonymousOnly || lease.Anonymous))
                {
                    return lease;
                }
            }

            return null;
        }

        /// <summary>撤销条目和全部 lease；底层 Provider 释放必须在 Core 锁外执行。</summary>
        internal void Invalidate()
        {
            if (!IsValid)
            {
                return;
            }

            IsValid = false;
            for (var index = 0; index < mLeases.Count; index++)
            {
                mLeases[index].Invalidate();
            }

            mLeases.Clear();
            RefCount = 0;
            Asset = null;
            Provider = null;
        }
    }

    /// <summary>表示一次调用方所有的独立获取，可由 handle 或匿名对象 API 持有。</summary>
    internal sealed class ResLease
    {
        /// <summary>创建初始引用数为一的 lease。</summary>
        internal ResLease(ResCacheEntry entry, bool anonymous)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Anonymous = anonymous;
            Count = 1;
        }

        internal ResCacheEntry Entry { get; }
        internal bool Anonymous { get; }
#if UNITY_EDITOR || (GODOT && TOOLS)
        internal ResLoadSource Source { get; private set; }
#endif
        internal int Count { get; private set; }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>附加当前 lease 的 Editor/Tools 调用来源。</summary>
        /// <param name="source">本次获取的调用来源。</param>
        internal void SetSource(ResLoadSource source)
        {
            Source = source;
        }
#endif

        /// <summary>尝试减少当前 lease 的本地引用数，重复释放不会影响其它 lease。</summary>
        internal bool TryDecrement()
        {
            if (Count <= 0)
            {
                return false;
            }

            Count--;
            return true;
        }

        /// <summary>由所属条目强制撤销剩余引用。</summary>
        internal void Invalidate() => Count = 0;
    }

    /// <summary>协调相同 key 的共享底层加载和独立等待者取消。</summary>
    internal sealed class ResPendingLoad : IDisposable
    {
        private readonly TaskCompletionSource<bool> mCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object mCancellationLifetimeLock = new();
        private int mActiveCancellationCalls;
        private bool mDisposeRequested;
        private bool mCancellationDisposed;

        /// <summary>创建属于固定 Provider 与缓存代次的加载占位。</summary>
        internal ResPendingLoad(
            ResCacheKey key,
            IResourceProvider provider
#if UNITY_EDITOR || (GODOT && TOOLS)
            ,
            string providerName,
#else
            ,
#endif
            long providerGeneration,
            long cacheEpoch,
            bool synchronous)
        {
            Key = key;
            Provider = provider;
#if UNITY_EDITOR || (GODOT && TOOLS)
            ProviderName = providerName;
#endif
            ProviderGeneration = providerGeneration;
            CacheEpoch = cacheEpoch;
            IsSynchronous = synchronous;
            LoadCancellation = synchronous ? null : new CancellationTokenSource();
        }

        internal ResCacheKey Key { get; }
        internal IResourceProvider Provider { get; }
#if UNITY_EDITOR || (GODOT && TOOLS)
        internal string ProviderName { get; }
#endif
        internal long ProviderGeneration { get; }
        internal long CacheEpoch { get; }
        internal bool IsSynchronous { get; }
        internal CancellationTokenSource LoadCancellation { get; }
        internal Task Completion => mCompletion.Task;
        internal ResCacheEntry Entry { get; set; }
        internal int WaiterCount { get; set; }
        internal bool Completed { get; set; }
        internal bool ReturnedNull { get; set; }
        internal bool Abandoned { get; set; }
        internal string StaleReason { get; set; }

        /// <summary>通知全部等待者重新读取当前 pending 的结果。</summary>
        internal void SignalSuccess() => mCompletion.TrySetResult(true);

        /// <summary>将底层失败传播给全部等待者，并标记异常已观察以覆盖零等待者竞争。</summary>
        internal void SignalFailure(Exception exception)
        {
            mCompletion.TrySetException(exception);
            _ = mCompletion.Task.Exception;
        }

        /// <summary>将 Provider 或缓存代次变化传播给全部等待者。</summary>
        internal void SignalStale()
        {
            string message = string.IsNullOrEmpty(StaleReason)
                ? "ResKit load became stale before completion."
                : StaleReason;
            mCompletion.TrySetException(new InvalidOperationException(message));
            _ = mCompletion.Task.Exception;
        }

        /// <summary>结束已全部取消且无人观察的共享加载。</summary>
        internal void SignalAbandoned() => mCompletion.TrySetCanceled();

        /// <summary>取消底层 Provider 调用，并与完成线程的 Dispose 竞争安全地协调。</summary>
        internal void CancelLoad()
        {
            if (LoadCancellation == null)
            {
                return;
            }

            lock (mCancellationLifetimeLock)
            {
                if (mCancellationDisposed)
                {
                    return;
                }

                mActiveCancellationCalls++;
            }

            try
            {
                LoadCancellation.Cancel(false);
            }
            finally
            {
                CompleteCancellationCall();
            }
        }

        /// <summary>结束一次锁外取消调用，并在 Provider 已退出时安全释放取消源。</summary>
        private void CompleteCancellationCall()
        {
            bool dispose;
            lock (mCancellationLifetimeLock)
            {
                mActiveCancellationCalls--;
                dispose = mDisposeRequested && mActiveCancellationCalls == 0;
                if (dispose)
                {
                    mCancellationDisposed = true;
                }
            }

            if (dispose)
            {
                LoadCancellation?.Dispose();
            }
        }

        /// <summary>请求释放取消源；正在执行的取消回调退出后再完成实际 Dispose。</summary>
        public void Dispose()
        {
            bool dispose;
            lock (mCancellationLifetimeLock)
            {
                if (mDisposeRequested)
                {
                    return;
                }

                mDisposeRequested = true;
                dispose = mActiveCancellationCalls == 0;
                if (dispose)
                {
                    mCancellationDisposed = true;
                }
            }

            if (dispose)
            {
                LoadCancellation?.Dispose();
            }
        }
    }

    /// <summary>描述一次已从缓存状态分离、等待在锁外释放的底层资源。</summary>
    internal readonly struct ResReleaseWork
    {
        /// <summary>创建底层资源释放工作，并保留创建该资源的 Provider。</summary>
        internal ResReleaseWork(ResCacheEntry entry)
        {
            Provider = entry.Provider;
            Asset = entry.Asset;
            Key = entry.Key;
        }

        internal IResourceProvider Provider { get; }
        internal object Asset { get; }
        internal ResCacheKey Key { get; }
        internal bool IsValid => Provider != null && Asset != null;
    }
}
