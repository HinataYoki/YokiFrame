using System;
using System.Threading;
using System.Threading.Tasks;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    public static partial class ResKit
    {
#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>异步加载资源对象，并登记一个可通过 <see cref="Release(object)"/> 消费的匿名 lease。</summary>
        /// <typeparam name="T">调用方期望的资源对象类型。</typeparam>
        /// <param name="path">Provider 可识别的资源路径。</param>
        /// <param name="token">仅取消当前等待者；存在其它等待者时不取消共享底层加载。</param>
        /// <returns>加载成功的资源对象；Provider 返回 null 时返回 null。</returns>
        public static async UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#else
        /// <summary>异步加载资源对象，并登记一个可通过 <see cref="Release(object)"/> 消费的匿名 lease。</summary>
        /// <typeparam name="T">调用方期望的资源对象类型。</typeparam>
        /// <param name="path">Provider 可识别的资源路径。</param>
        /// <param name="token">仅取消当前等待者；存在其它等待者时不取消共享底层加载。</param>
        /// <returns>加载成功的资源对象；Provider 返回 null 时返回 null。</returns>
        public static async Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            ResLease lease = await LoadLeaseAsync<T>(path, true, token);
#else
            ResLease lease = await LoadLeaseAsync<T>(path, true, token);
#endif
            return lease == null ? null : GetLeaseAsset<T>(lease);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>异步加载资源并返回本次获取独占释放权的 handle。</summary>
        /// <typeparam name="T">调用方期望的资源对象类型。</typeparam>
        /// <param name="path">Provider 可识别的资源路径。</param>
        /// <param name="token">仅取消当前等待者；存在其它等待者时不取消共享底层加载。</param>
        /// <returns>独立资源 handle；Provider 返回 null 时返回 null。</returns>
        public static async UniTask<ResHandle<T>> LoadAssetAsync<T>(
#else
        /// <summary>异步加载资源并返回本次获取独占释放权的 handle。</summary>
        /// <typeparam name="T">调用方期望的资源对象类型。</typeparam>
        /// <param name="path">Provider 可识别的资源路径。</param>
        /// <param name="token">仅取消当前等待者；存在其它等待者时不取消共享底层加载。</param>
        /// <returns>独立资源 handle；Provider 返回 null 时返回 null。</returns>
        public static async Task<ResHandle<T>> LoadAssetAsync<T>(
#endif
            string path,
            CancellationToken token = default) where T : class
        {
#if YOKIFRAME_UNITASK_SUPPORT
            ResLease lease = await LoadLeaseAsync<T>(path, false, token);
#else
            ResLease lease = await LoadLeaseAsync<T>(path, false, token);
#endif
            return lease == null ? null : new ResHandle<T>(lease);
        }

        /// <summary>为仅使用 Task 契约的 Kit 提供内部异步桥接，避免消费者直接依赖可选 UniTask 元数据。</summary>
        /// <typeparam name="T">调用方期望的资源对象类型。</typeparam>
        /// <param name="path">Provider 可识别的资源路径。</param>
        /// <param name="token">仅取消当前等待者；不会提前中断其它共享等待者。</param>
        /// <returns>加载成功的资源对象；Provider 返回空时返回空。</returns>
        internal static async Task<T> LoadTaskAsync<T>(
            string path,
            CancellationToken token = default) where T : class
        {
            ResLease lease = await LoadLeaseAsync<T>(path, true, token);
            return lease == null ? null : GetLeaseAsset<T>(lease);
        }

        /// <summary>加入或创建同 key 的共享加载，并为当前调用方维护独立取消计数。</summary>
        private static async Task<ResLease> LoadLeaseAsync<T>(
            string path,
            bool anonymous,
            CancellationToken token) where T : class
        {
            EnsurePath(path);
            token.ThrowIfCancellationRequested();
#if UNITY_EDITOR || (GODOT && TOOLS)
            ResLoadSource source = CaptureLoadSource();
#endif
            ResCacheKey key = new(typeof(T), path);
            ResPendingLoad pending;
            bool startProvider = false;
            lock (sLock)
            {
                if (sCache.TryGetValue(key, out ResCacheEntry cached))
                {
                    ResLease cachedLease = AcquireLeaseLocked(cached, anonymous);
#if UNITY_EDITOR || (GODOT && TOOLS)
                    cachedLease.SetSource(source);
#endif
                    return cachedLease;
                }

                if (!sPendingLoads.TryGetValue(key, out pending))
                {
                    IResourceProvider provider = EnsureProviderLocked();
                    pending = new ResPendingLoad(
                        key, provider
#if UNITY_EDITOR || (GODOT && TOOLS)
                        , sProviderName
#endif
                        , sProviderGeneration, sCacheEpoch, false);
                    sPendingLoads.Add(key, pending);
                    startProvider = true;
                }

                pending.WaiterCount++;
#if UNITY_EDITOR || (GODOT && TOOLS)
                BumpDiagnosticVersionLocked();
#endif
            }

            if (startProvider)
            {
                _ = RunSharedProviderLoadAsync<T>(pending);
            }

            ResLease lease = await AwaitPendingLeaseAsync<T>(pending, anonymous, token);
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (lease != null) lease.SetSource(source);
#endif
            return lease;
        }

        /// <summary>等待共享完成信号后从同一条目创建当前调用方自己的 lease。</summary>
        private static async Task<ResLease> AwaitPendingLeaseAsync<T>(
            ResPendingLoad pending,
            bool anonymous,
            CancellationToken token) where T : class
        {
            try
            {
                await AwaitWithCancellationAsync(pending.Completion, token);
                token.ThrowIfCancellationRequested();
                lock (sLock)
                {
                    if (!IsPendingGenerationCurrentLocked(pending))
                    {
                        throw CreateStaleLoadException(pending);
                    }

                    if (pending.ReturnedNull)
                    {
                        return null;
                    }

                    ResCacheEntry entry = pending.Entry;
                    if (entry == null || !entry.IsValid || !sCache.TryGetValue(pending.Key, out ResCacheEntry current)
                        || !ReferenceEquals(entry, current))
                    {
                        throw CreateStaleLoadException(pending);
                    }

                    return AcquireLeaseLocked(entry, anonymous);
                }
            }
            finally
            {
                ReleasePendingWaiter(pending);
            }
        }

        /// <summary>验证已完成 pending 的 Provider 与缓存代次仍是调用方当前可见代次。</summary>
        private static bool IsPendingGenerationCurrentLocked(ResPendingLoad pending)
        {
            return ReferenceEquals(sProvider, pending.Provider)
                && sProviderGeneration == pending.ProviderGeneration
                && sCacheEpoch == pending.CacheEpoch;
        }

        /// <summary>让单个等待者响应自己的取消令牌，而不取消共享 completion。</summary>
        private static async Task AwaitWithCancellationAsync(Task completion, CancellationToken token)
        {
            if (!token.CanBeCanceled)
            {
                await completion.ConfigureAwait(false);
                return;
            }

            token.ThrowIfCancellationRequested();
            TaskCompletionSource<bool> cancellation =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(
                static state => ((TaskCompletionSource<bool>)state).TrySetResult(true), cancellation))
            {
                Task finished = await Task.WhenAny(completion, cancellation.Task).ConfigureAwait(false);
                if (ReferenceEquals(finished, cancellation.Task))
                {
                    throw new OperationCanceledException(token);
                }

                await completion.ConfigureAwait(false);
            }
        }

        /// <summary>调用固定 Provider 完成底层加载，所有异常都转换为 pending 终态。</summary>
        private static async Task RunSharedProviderLoadAsync<T>(ResPendingLoad pending) where T : class
        {
            T asset;
            try
            {
#if YOKIFRAME_UNITASK_SUPPORT
                asset = await pending.Provider.LoadAsync<T>(
                    pending.Key.Path, pending.LoadCancellation.Token);
#else
                asset = await pending.Provider.LoadAsync<T>(
                    pending.Key.Path, pending.LoadCancellation.Token);
#endif
            }
            catch (Exception exception)
            {
                CompleteAsynchronousFailure(pending, exception);
                pending.Dispose();
                return;
            }

            CompleteAsynchronousSuccess(pending, asset);
            pending.Dispose();
        }

        /// <summary>原子发布共享异步结果；跨代或无人等待的结果不会进入缓存。</summary>
        private static void CompleteAsynchronousSuccess<T>(ResPendingLoad pending, T asset) where T : class
        {
            bool accepted;
            lock (sLock)
            {
                accepted = IsCurrentPendingLocked(pending) && pending.WaiterCount > 0;
                pending.Completed = true;
                if (accepted)
                {
                    sPendingLoads.Remove(pending.Key);
                    pending.ReturnedNull = asset == null;
                    if (asset != null)
                    {
                        pending.Entry = CreateEntryLocked(pending, asset);
                    }

#if UNITY_EDITOR || (GODOT && TOOLS)
                    BumpDiagnosticVersionLocked();
#endif
                }
            }

            if (accepted)
            {
                pending.SignalSuccess();
                return;
            }

            CompleteRejectedAsynchronousResult(pending, asset);
        }

        /// <summary>释放被拒绝的旧结果，并在释放完成后向仍存在的等待者报告终态。</summary>
        private static void CompleteRejectedAsynchronousResult<T>(ResPendingLoad pending, T asset) where T : class
        {
            Exception releaseFailure = null;
            if (asset != null)
            {
                try
                {
                    pending.Provider.Release(asset);
                }
                catch (Exception exception)
                {
                    releaseFailure = exception;
                }
            }

            if (pending.Abandoned)
            {
                if (releaseFailure != null)
                {
                    RecordBackgroundFailure(releaseFailure);
                }

                pending.SignalAbandoned();
                return;
            }

            if (releaseFailure != null)
            {
                RecordBackgroundFailure(releaseFailure);
            }

            if (releaseFailure == null)
            {
                pending.SignalStale();
                return;
            }

            pending.SignalFailure(new AggregateException(CreateStaleLoadException(pending), releaseFailure));
        }

        /// <summary>将 Provider 异常提交给当前等待者；跨代时统一报告 stale。</summary>
        private static void CompleteAsynchronousFailure(ResPendingLoad pending, Exception exception)
        {
            bool current;
            lock (sLock)
            {
                current = IsCurrentPendingLocked(pending);
                pending.Completed = true;
                if (current)
                {
                    sPendingLoads.Remove(pending.Key);
#if UNITY_EDITOR || (GODOT && TOOLS)
                    BumpDiagnosticVersionLocked();
#endif
                }
            }

            if (pending.Abandoned)
            {
                pending.SignalAbandoned();
            }
            else if (!current)
            {
                pending.SignalStale();
            }
            else
            {
                pending.SignalFailure(exception);
            }
        }

        /// <summary>减少一个等待者，并在全部取消时取消底层或回收零 lease 结果。</summary>
        private static void ReleasePendingWaiter(ResPendingLoad pending)
        {
            bool abandon = false;
            ResReleaseWork release = default;
            lock (sLock)
            {
                if (pending.WaiterCount > 0)
                {
                    pending.WaiterCount--;
                }

                if (pending.WaiterCount == 0 && !pending.Completed && !pending.IsSynchronous
                    && IsCurrentPendingLocked(pending))
                {
                    sPendingLoads.Remove(pending.Key);
                    pending.Abandoned = true;
                    abandon = true;
#if UNITY_EDITOR || (GODOT && TOOLS)
                    BumpDiagnosticVersionLocked();
#endif
                }
                else if (pending.WaiterCount == 0 && pending.Entry != null
                    && pending.Entry.IsValid && pending.Entry.RefCount == 0)
                {
                    release = DetachZeroLeaseEntryLocked(pending.Entry);
                }
            }

            if (abandon)
            {
                AbandonPendingLoad(pending);
            }

            ReleaseInBackground(release);
        }

        /// <summary>撤销已无等待者的异步加载并使共享 completion 进入取消终态。</summary>
        private static void AbandonPendingLoad(ResPendingLoad pending)
        {
            pending.SignalAbandoned();
            try
            {
                pending.CancelLoad();
            }
            catch (Exception exception)
            {
                RecordBackgroundFailure(exception);
            }
        }

        /// <summary>在没有同步调用方可承接异常时执行底层释放并记录失败。</summary>
        private static void ReleaseInBackground(ResReleaseWork release)
        {
            if (!release.IsValid)
            {
                return;
            }

            try
            {
                release.Provider.Release(release.Asset);
            }
            catch (Exception exception)
            {
                RecordBackgroundFailure(exception);
            }
        }
    }
}
