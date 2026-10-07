using System;

namespace YokiFrame
{
    public static partial class ResKit
    {
        /// <summary>同步加载资源对象，并登记一个可通过 <see cref="Release(object)"/> 消费的匿名 lease。</summary>
        /// <typeparam name="T">调用方期望的资源对象类型。</typeparam>
        /// <param name="path">Provider 可识别的资源路径。</param>
        /// <returns>加载成功的资源对象；Provider 返回 null 时返回 null。</returns>
        public static T Load<T>(string path) where T : class
        {
            ResLease lease = LoadLease<T>(path, true);
            return lease == null ? null : GetLeaseAsset<T>(lease);
        }

        /// <summary>同步加载资源并返回本次获取独占释放权的 handle。</summary>
        /// <typeparam name="T">调用方期望的资源对象类型。</typeparam>
        /// <param name="path">Provider 可识别的资源路径。</param>
        /// <returns>独立资源 handle；Provider 返回 null 时返回 null。</returns>
        public static ResHandle<T> LoadAsset<T>(string path) where T : class
        {
            ResLease lease = LoadLease<T>(path, false);
            return lease == null ? null : new ResHandle<T>(lease);
        }

        /// <summary>执行同步缓存获取或创建底层加载占位，避免同 key 结果互相覆盖。</summary>
        private static ResLease LoadLease<T>(string path, bool anonymous) where T : class
        {
            EnsurePath(path);
#if UNITY_EDITOR || (GODOT && TOOLS)
            ResLoadSource source = CaptureLoadSource();
#endif
            ResCacheKey key = new(typeof(T), path);
            ResPendingLoad pending = AdmitLoad<T>(key, anonymous, true
#if UNITY_EDITOR || (GODOT && TOOLS)
                , source
#endif
                , out ResLease cachedLease, out _);
            if (cachedLease != null)
            {
                return cachedLease;
            }

            ResLease lease = InvokeSynchronousProvider<T>(pending, anonymous);
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (lease != null)
            {
                lock (sLock)
                {
                    lease.SetSource(source);
                }
            }
#endif
            return lease;
        }

        /// <summary>
        /// 取得已缓存 lease，或在 Provider 已安装时登记加载占位。Provider 缺失时在状态锁外创建后重试。
        /// </summary>
        /// <typeparam name="T">调用方期望的资源对象类型。</typeparam>
        /// <param name="key">资源缓存键。</param>
        /// <param name="anonymous">是否创建匿名 lease。</param>
        /// <param name="synchronous">同步加载遇到同 key 在途请求时失败；异步加载则加入。</param>
#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <param name="source">本次获取的调用来源。</param>
#endif
        /// <param name="cachedLease">命中缓存时的 lease；未命中时为 null。</param>
        /// <param name="startProvider">本次是否负责启动异步 Provider 调用。</param>
        /// <returns>新登记或加入的在途加载；命中缓存时返回 null。</returns>
        private static ResPendingLoad AdmitLoad<T>(
            ResCacheKey key,
            bool anonymous,
            bool synchronous,
#if UNITY_EDITOR || (GODOT && TOOLS)
            ResLoadSource source,
#endif
            out ResLease cachedLease,
            out bool startProvider) where T : class
        {
            while (true)
            {
                ResPendingLoad pending = TryAdmitLoadLocked<T>(key, anonymous, synchronous
#if UNITY_EDITOR || (GODOT && TOOLS)
                    , source
#endif
                    , out cachedLease, out startProvider, out bool needsProvider);
                if (!needsProvider)
                {
                    return pending;
                }

                EnsureProvider();
            }
        }

        /// <summary>在状态锁内完成缓存命中、在途加入或新占位登记。</summary>
        /// <typeparam name="T">调用方期望的资源对象类型。</typeparam>
        /// <param name="key">资源缓存键。</param>
        /// <param name="anonymous">是否创建匿名 lease。</param>
        /// <param name="synchronous">同步路径遇到在途加载时抛出。</param>
#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <param name="source">本次获取的调用来源。</param>
#endif
        /// <param name="cachedLease">命中缓存时的 lease。</param>
        /// <param name="startProvider">需要由调用方启动异步加载时为 true。</param>
        /// <param name="needsProvider">当前没有 Provider，调用方必须在锁外创建。</param>
        /// <returns>在途加载；命中缓存或需要创建 Provider 时返回 null。</returns>
        private static ResPendingLoad TryAdmitLoadLocked<T>(
            ResCacheKey key,
            bool anonymous,
            bool synchronous,
#if UNITY_EDITOR || (GODOT && TOOLS)
            ResLoadSource source,
#endif
            out ResLease cachedLease,
            out bool startProvider,
            out bool needsProvider) where T : class
        {
            cachedLease = null;
            startProvider = false;
            needsProvider = false;
            lock (sLock)
            {
                if (sCache.TryGetValue(key, out ResCacheEntry cached))
                {
                    cachedLease = AcquireLeaseLocked(cached, anonymous);
#if UNITY_EDITOR || (GODOT && TOOLS)
                    cachedLease.SetSource(source);
#endif
                    return null;
                }

                if (sPendingLoads.TryGetValue(key, out ResPendingLoad existing))
                {
                    if (synchronous)
                    {
                        throw new InvalidOperationException(
                            "A ResKit load for the same resource key is already in progress. Use the async API to join it.");
                    }

                    existing.WaiterCount++;
#if UNITY_EDITOR || (GODOT && TOOLS)
                    BumpDiagnosticVersionLocked();
#endif
                    return existing;
                }

                if (sProvider == null)
                {
                    needsProvider = true;
                    return null;
                }

                return CreateAdmittedPending(key, synchronous, out startProvider);
            }
        }

        /// <summary>在已持有状态锁且 Provider 存在时登记新的加载占位。</summary>
        /// <param name="key">资源缓存键。</param>
        /// <param name="synchronous">同步占位不增加等待者，也不标记启动 Provider。</param>
        /// <param name="startProvider">异步新占位需要调用方启动 Provider。</param>
        /// <returns>已登记的在途加载。</returns>
        private static ResPendingLoad CreateAdmittedPending(
            ResCacheKey key,
            bool synchronous,
            out bool startProvider)
        {
            ResPendingLoad pending = new(
                key, sProvider
#if UNITY_EDITOR || (GODOT && TOOLS)
                , sProviderName
#endif
                , sProviderGeneration, sCacheEpoch, synchronous);
            sPendingLoads.Add(key, pending);
            startProvider = !synchronous;
            if (!synchronous)
            {
                pending.WaiterCount++;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersionLocked();
#endif
            return pending;
        }

        /// <summary>在状态锁外调用 Provider，并将成功、失败或跨代结果提交给共享状态。</summary>
        private static ResLease InvokeSynchronousProvider<T>(
            ResPendingLoad pending,
            bool anonymous) where T : class
        {
            T asset;
            try
            {
                asset = pending.Provider.Load<T>(pending.Key.Path);
            }
            catch (Exception exception)
            {
                if (CompleteSynchronousFailure(pending, exception))
                {
                    throw CreateStaleLoadException(pending);
                }

                throw;
            }

            return CompleteSynchronousSuccess(pending, asset, anonymous);
        }

        /// <summary>原子提交同步加载结果，并在跨代时先由旧 Provider 释放再拒绝结果。</summary>
        private static ResLease CompleteSynchronousSuccess<T>(
            ResPendingLoad pending,
            T asset,
            bool anonymous) where T : class
        {
            ResLease lease = null;
            bool accepted;
            lock (sLock)
            {
                accepted = IsCurrentPendingLocked(pending);
                pending.Completed = true;
                if (accepted)
                {
                    sPendingLoads.Remove(pending.Key);
                    pending.ReturnedNull = asset == null;
                    if (asset != null)
                    {
                        pending.Entry = CreateEntryLocked(pending, asset);
                        lease = AcquireLeaseLocked(pending.Entry, anonymous);
                    }

#if UNITY_EDITOR || (GODOT && TOOLS)
                    BumpDiagnosticVersionLocked();
#endif
                }
            }

            if (accepted)
            {
                pending.SignalSuccess();
                pending.Dispose();
                return lease;
            }

            ThrowStaleSynchronousResult(pending, asset);
            return null;
        }

        /// <summary>完成同步 Provider 异常，并告知异步加入者相同失败或跨代拒绝。</summary>
        private static bool CompleteSynchronousFailure(ResPendingLoad pending, Exception exception)
        {
            bool stale;
            lock (sLock)
            {
                stale = !IsCurrentPendingLocked(pending);
                pending.Completed = true;
                if (!stale)
                {
                    sPendingLoads.Remove(pending.Key);
#if UNITY_EDITOR || (GODOT && TOOLS)
                    BumpDiagnosticVersionLocked();
#endif
                }
            }

            if (stale)
            {
                pending.SignalStale();
            }
            else
            {
                pending.SignalFailure(exception);
            }

            pending.Dispose();
            return stale;
        }

        /// <summary>释放跨代同步结果并构造可观察异常，释放失败也不会恢复缓存状态。</summary>
        private static void ThrowStaleSynchronousResult<T>(ResPendingLoad pending, T asset) where T : class
        {
            InvalidOperationException staleException = CreateStaleLoadException(pending);
            if (asset == null)
            {
                pending.SignalStale();
                pending.Dispose();
                throw staleException;
            }

            try
            {
                pending.Provider.Release(asset);
                pending.SignalStale();
                throw staleException;
            }
            catch (Exception releaseException) when (!ReferenceEquals(releaseException, staleException))
            {
                AggregateException aggregate = new(staleException, releaseException);
                RecordBackgroundFailure(aggregate);
                pending.SignalFailure(aggregate);
                throw aggregate;
            }
            finally
            {
                pending.Dispose();
            }
        }

        /// <summary>判断 pending 是否仍属于当前字典、Provider 代次和缓存代次。</summary>
        private static bool IsCurrentPendingLocked(ResPendingLoad pending)
        {
            return sPendingLoads.TryGetValue(pending.Key, out ResPendingLoad current)
                && ReferenceEquals(current, pending)
                && ReferenceEquals(sProvider, pending.Provider)
                && sProviderGeneration == pending.ProviderGeneration
                && sCacheEpoch == pending.CacheEpoch
                && !pending.Abandoned;
        }

        /// <summary>为已验证结果创建缓存条目；调用方必须持有状态锁。</summary>
        private static ResCacheEntry CreateEntryLocked(ResPendingLoad pending, object asset)
        {
            ResCacheEntry entry = new(
                pending.Key,
                asset,
                pending.Provider
#if UNITY_EDITOR || (GODOT && TOOLS)
                ,
                pending.ProviderName);
#else
                );
#endif
#if UNITY_EDITOR || (GODOT && TOOLS)
            entry.SetProviderGeneration(pending.ProviderGeneration);
#endif
            sCache.Add(pending.Key, entry);
            return entry;
        }

        /// <summary>创建独立 lease 并更新诊断版本；调用方必须持有状态锁。</summary>
        private static ResLease AcquireLeaseLocked(ResCacheEntry entry, bool anonymous)
        {
            ResLease lease = entry.Acquire(anonymous);
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersionLocked();
#endif
            return lease;
        }

        /// <summary>为跨 Provider 或 ClearAll 的旧结果创建统一异常。</summary>
        private static InvalidOperationException CreateStaleLoadException(ResPendingLoad pending)
        {
            string message = string.IsNullOrEmpty(pending.StaleReason)
                ? "ResKit rejected a stale load result."
                : pending.StaleReason;
            return new InvalidOperationException(message);
        }
    }
}
