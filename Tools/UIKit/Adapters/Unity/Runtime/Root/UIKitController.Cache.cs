#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    internal sealed partial class UIKitController
    {
        /// <summary>
        /// 异步确保面板实例已初始化但保持 inactive，不注册层级、栈或模态。
        /// </summary>
        internal async Task<bool> PreloadAsync(
            Type panelType,
            UILevel level,
            PanelCachePolicy policy,
            CancellationToken token)
        {
            EnsureAvailable();
            token.ThrowIfCancellationRequested();
            ValidatePreloadPolicy(policy);
            PanelEntry entry = await GetOrCreateAsync(panelType, level, policy, null, token);
            if (entry.State == PanelState.Preloaded && entry.CachePolicy == PanelCachePolicy.Reusable)
                AddReusableEntry(entry);
            return entry != null && entry.Panel != default;
        }

        /// <summary>
        /// 判断指定类型已有有效实例，包括预加载、活动、隐藏和关闭保留状态。
        /// </summary>
        internal bool IsLoaded(Type panelType)
        {
            EnsureAvailable();
            ValidatePanelType(panelType);
            return TryGetLiveEntry(panelType, out _);
        }

        /// <summary>
        /// 判断指定类型仍处于从未打开的预加载状态。
        /// </summary>
        internal bool IsPreloaded(Type panelType)
        {
            EnsureAvailable();
            ValidatePanelType(panelType);
            return TryGetLiveEntry(panelType, out PanelEntry entry)
                && !entry.HasOpened
                && entry.State == PanelState.Preloaded;
        }

        /// <summary>
        /// 复制已加载类型并按完整类型名稳定排序。
        /// </summary>
        internal IReadOnlyCollection<Type> GetLoadedTypes()
        {
            EnsureAvailable();
            var result = new List<Type>(mEntries.Count);
            foreach (PanelEntry entry in mEntries.Values)
            {
                if (entry.Panel != default) result.Add(entry.PanelType);
            }

            result.Sort(static (left, right) => string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
            return result;
        }

        /// <summary>
        /// 复制全部有效实例并按完整类型名稳定排序。
        /// </summary>
        internal IReadOnlyList<IPanel> GetLoadedPanels()
        {
            EnsureAvailable();
            var entries = new List<PanelEntry>(mEntries.Values);
            entries.Sort(static (left, right) => string.Compare(
                left.PanelType.FullName, right.PanelType.FullName, StringComparison.Ordinal));
            var result = new List<IPanel>(entries.Count);
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index].Panel != default) result.Add(entries[index].Panel);
            }

            return result;
        }

        /// <summary>
        /// 卸载预加载或关闭保留实例；活动和转换中的面板不会被隐式关闭。
        /// </summary>
        internal bool Unload(Type panelType)
        {
            EnsureAvailable();
            ValidatePanelType(panelType);
            if (!TryGetLiveEntry(panelType, out PanelEntry entry)) return false;
            if (entry.State != PanelState.Preloaded && entry.State != PanelState.Cached) return false;
            DisposeEntry(entry);
            return true;
        }

        /// <summary>
        /// 销毁全部 inactive Reusable 项，不影响活动项或 Persistent 项。
        /// </summary>
        internal int ClearReusableCache()
        {
            EnsureAvailable();
            var entries = new List<PanelEntry>(mReusableLru);
            for (var index = 0; index < entries.Count; index++) DisposeEntry(entries[index]);
            return entries.Count;
        }

        /// <summary>
        /// 把关闭的 Reusable 项加入 LRU 尾部并立即执行容量约束。
        /// </summary>
        private void AddReusableEntry(PanelEntry entry)
        {
            RemoveReusableEntry(entry);
            entry.ReusableNode = mReusableLru.AddLast(entry);
            TrimReusableCache();
        }

        /// <summary>
        /// 从 Reusable LRU 移除指定 entry。
        /// </summary>
        private void RemoveReusableEntry(PanelEntry entry)
        {
            if (entry.ReusableNode == null) return;
            mReusableLru.Remove(entry.ReusableNode);
            entry.ReusableNode = null;
        }

        /// <summary>
        /// 淘汰最早关闭的 Reusable 项直到满足当前容量。
        /// </summary>
        private void TrimReusableCache()
        {
            while (mReusableLru.Count > mReusableCapacity)
            {
                PanelEntry oldest = mReusableLru.First.Value;
                DisposeEntry(oldest);
            }
        }

        /// <summary>
        /// 预加载只接受关闭后仍可保留实例的策略。
        /// </summary>
        private static void ValidatePreloadPolicy(PanelCachePolicy policy)
        {
            if (policy == PanelCachePolicy.Transient)
                throw new ArgumentException("Preload requires Reusable or Persistent cache policy.", nameof(policy));
        }
    }
}
#endif
