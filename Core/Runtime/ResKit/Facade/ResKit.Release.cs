using System;

namespace YokiFrame
{
    public static partial class ResKit
    {
        /// <summary>释放指定 handle 当前持有的一次本地引用；重复释放已归零 handle 无副作用。</summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="handle">需要释放的独立资源 handle；null 会被忽略。</param>
        public static void Release<T>(ResHandle<T> handle) where T : class
        {
            if (handle == null)
            {
                return;
            }

            handle.Release();
        }

        /// <summary>消费引用该对象的一个已登记 lease；未知对象不会转交给当前 Provider。</summary>
        /// <param name="asset">由 ResKit 返回的资源对象。</param>
        public static void Release(object asset)
        {
            if (asset == null)
            {
                return;
            }

            ResReleaseWork release = default;
            lock (sLock)
            {
                ResLease lease = FindObjectLeaseLocked(asset, true)
                    ?? FindObjectLeaseLocked(asset, false);
                if (lease == null || !lease.Entry.TryRelease(lease))
                {
                    return;
                }

                if (lease.Entry.RefCount == 0)
                {
                    release = DetachZeroLeaseEntryLocked(lease.Entry);
                }
                else
                {
#if UNITY_EDITOR || (GODOT && TOOLS)
                    BumpDiagnosticVersionLocked();
#endif
                }
            }

            ReleaseImmediately(release);
        }

        /// <summary>释放一个 handle lease 引用，并在条目归零时调用其原始 Provider。</summary>
        internal static void ReleaseLease(ResLease lease)
        {
            if (lease == null)
            {
                return;
            }

            ResReleaseWork release = default;
            lock (sLock)
            {
                if (!lease.Entry.TryRelease(lease))
                {
                    return;
                }

                if (lease.Entry.RefCount == 0)
                {
                    release = DetachZeroLeaseEntryLocked(lease.Entry);
                }
                else
                {
#if UNITY_EDITOR || (GODOT && TOOLS)
                    BumpDiagnosticVersionLocked();
#endif
                }
            }

            ReleaseImmediately(release);
        }

        /// <summary>获取有效 lease 的路径；已释放或被 ClearAll 撤销时返回 null。</summary>
        internal static string GetLeasePath(ResLease lease)
        {
            lock (sLock)
            {
                return IsLeaseActiveLocked(lease) ? lease.Entry.Key.Path : null;
            }
        }

        /// <summary>获取有效 lease 的强类型资源；失效或类型不匹配时返回 null。</summary>
        internal static T GetLeaseAsset<T>(ResLease lease) where T : class
        {
            lock (sLock)
            {
                return IsLeaseActiveLocked(lease) ? lease.Entry.Asset as T : null;
            }
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>获取创建有效 lease 底层条目的 Provider 名称。</summary>
        internal static string GetLeaseProviderName(ResLease lease)
        {
            lock (sLock)
            {
                return IsLeaseActiveLocked(lease) ? lease.Entry.ProviderName : null;
            }
        }

        /// <summary>获取有效 lease 所属共享条目的总引用数；失效时返回零。</summary>
        internal static int GetLeaseRefCount(ResLease lease)
        {
            lock (sLock)
            {
                return IsLeaseActiveLocked(lease) ? lease.Entry.RefCount : 0;
            }
        }
#endif

        /// <summary>按对象引用查找一个活动 lease；调用方必须持有状态锁。</summary>
        private static ResLease FindObjectLeaseLocked(object asset, bool anonymousOnly)
        {
            foreach (ResCacheEntry entry in sCache.Values)
            {
                ResLease lease = entry.FindObjectReleaseLease(asset, anonymousOnly);
                if (lease != null)
                {
                    return lease;
                }
            }

            return null;
        }

        /// <summary>验证 lease 仍有本地引用且所属条目尚未失效。</summary>
        private static bool IsLeaseActiveLocked(ResLease lease)
        {
            return lease != null && lease.Count > 0 && lease.Entry.IsValid;
        }

        /// <summary>按 entry identity 从缓存移除零引用条目并生成锁外释放工作。</summary>
        private static ResReleaseWork DetachZeroLeaseEntryLocked(ResCacheEntry entry)
        {
            if (!sCache.TryGetValue(entry.Key, out ResCacheEntry current)
                || !ReferenceEquals(current, entry) || entry.RefCount != 0)
            {
                return default;
            }

            sCache.Remove(entry.Key);
#if UNITY_EDITOR || (GODOT && TOOLS)
            AddUnloadRecordLocked(entry);
#endif
            ResReleaseWork release = new(entry);
            entry.Invalidate();
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersionLocked();
#endif
            return release;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>记录条目卸载事件；调用方必须持有状态锁且不得格式化时间文本。</summary>
        private static void AddUnloadRecordLocked(ResCacheEntry entry)
        {
            string typeName = entry.Key.AssetType.FullName ?? entry.Key.AssetType.Name;
            sUnloadHistory.Add(new ResUnloadEvent(
                entry.Key.Path, typeName, entry.ProviderName, DateTime.UtcNow));
        }
#endif

        /// <summary>执行单项同步释放；状态已先移除，因此 Provider 异常不会留下脏缓存。</summary>
        private static void ReleaseImmediately(ResReleaseWork release)
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
                throw;
            }
        }
    }
}
