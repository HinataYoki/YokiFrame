#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    internal sealed partial class UIKitController
    {
        private readonly Dictionary<int, List<PanelEntry>> mLevelEntries = new();

        /// <summary>
        /// 设置受管面板的层级和子层级，并同步父节点与模态 blocker。
        /// </summary>
        internal bool SetLevel(IPanel panel, UILevel level, int subLevel)
        {
            EnsureAvailable();
            if (!TryGetOwnedEntry(panel, out PanelEntry entry)) return false;
            ChangeEntryLevel(entry, level, subLevel);
            OnStateChanged();
            return true;
        }

        /// <summary>
        /// 只调整受管面板在当前 UILevel 内的子层级。
        /// </summary>
        internal bool SetSubLevel(IPanel panel, int subLevel)
        {
            EnsureAvailable();
            if (!TryGetOwnedEntry(panel, out PanelEntry entry)) return false;
            entry.SubLevel = subLevel;
            if (entry.IsLevelRegistered) SortLevel(entry.Level);
            OnStateChanged();
            return true;
        }

        /// <summary>
        /// 获取指定层级当前最顶部的可见面板。
        /// </summary>
        internal IPanel GetTopAtLevel(UILevel level)
        {
            EnsureAvailable();
            if (!mLevelEntries.TryGetValue(level.Order, out List<PanelEntry> entries)) return null;
            SortEntries(entries);
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                if (entries[index].State == PanelState.Open && entries[index].Panel != default)
                    return entries[index].Panel;
            }

            return null;
        }

        /// <summary>
        /// 获取所有层级中排序最高的可见面板。
        /// </summary>
        internal IPanel GetGlobalTop()
        {
            EnsureAvailable();
            PanelEntry top = null;
            foreach (List<PanelEntry> entries in mLevelEntries.Values)
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    PanelEntry candidate = entries[index];
                    if (candidate.State != PanelState.Open || candidate.Panel == default) continue;
                    if (top == null || CompareEntries(top, candidate) < 0) top = candidate;
                }
            }

            return top == null ? null : top.Panel;
        }

        /// <summary>
        /// 复制指定层级仍处于打开轮次的面板，并按渲染顺序排列。
        /// </summary>
        internal IReadOnlyList<IPanel> GetPanelsAtLevel(UILevel level)
        {
            EnsureAvailable();
            if (!mLevelEntries.TryGetValue(level.Order, out List<PanelEntry> entries))
                return Array.Empty<IPanel>();
            SortEntries(entries);
            var result = new List<IPanel>(entries.Count);
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index].IsLogicallyOpen && entries[index].Panel != default)
                    result.Add(entries[index].Panel);
            }

            return result;
        }

        /// <summary>
        /// 把 entry 登记到当前 UILevel，并移动到对应 Canvas 容器。
        /// </summary>
        private void RegisterAtLevel(PanelEntry entry)
        {
            if (!entry.IsLevelRegistered)
            {
                List<PanelEntry> entries = GetOrCreateLevelEntries(entry.Level);
                if (!entries.Contains(entry)) entries.Add(entry);
                entry.IsLevelRegistered = true;
            }

            RectTransform parent = mRoot.GetOrCreateLevelRoot(entry.Level);
            entry.Panel.transform.SetParent(parent, false);
            SortLevel(entry.Level);
        }

        /// <summary>
        /// 从旧层级移除并应用新的层级、子层级和父节点。
        /// </summary>
        private void ChangeEntryLevel(PanelEntry entry, UILevel level, int subLevel)
        {
            bool registered = entry.IsLevelRegistered;
            bool recreateModal = entry.ModalBlocker != default;
            if (recreateModal) RemoveModalBlocker(entry);
            if (registered) UnregisterFromLevel(entry);
            entry.Level = level;
            entry.SubLevel = subLevel;
            if (registered)
            {
                RegisterAtLevel(entry);
                if (recreateModal) EnsureModalBlocker(entry);
                SortLevel(entry.Level);
            }
        }

        /// <summary>
        /// 从层级索引移除 entry，并清理空列表。
        /// </summary>
        private void UnregisterFromLevel(PanelEntry entry)
        {
            if (!entry.IsLevelRegistered) return;
            if (mLevelEntries.TryGetValue(entry.Level.Order, out List<PanelEntry> entries))
            {
                entries.Remove(entry);
                if (entries.Count == 0) mLevelEntries.Remove(entry.Level.Order);
            }

            entry.IsLevelRegistered = false;
        }

        /// <summary>
        /// 把 inactive entry 移回禁用暂存根，不保留层级注册。
        /// </summary>
        private void MoveToStorage(PanelEntry entry)
        {
            UnregisterFromLevel(entry);
            RemoveModalBlocker(entry);
            if (entry.Panel == default) return;
            entry.Panel.gameObject.SetActive(false);
            entry.Panel.transform.SetParent(mRoot.StorageRoot, false);
        }

        /// <summary>
        /// 获取或创建指定 UILevel 的运行时排序列表。
        /// </summary>
        private List<PanelEntry> GetOrCreateLevelEntries(UILevel level)
        {
            if (mLevelEntries.TryGetValue(level.Order, out List<PanelEntry> entries)) return entries;
            entries = new List<PanelEntry>();
            mLevelEntries.Add(level.Order, entries);
            return entries;
        }

        /// <summary>
        /// 对指定层级排序，并让每个 modal blocker 紧邻对应 Panel 下方。
        /// </summary>
        private void SortLevel(UILevel level)
        {
            if (!mLevelEntries.TryGetValue(level.Order, out List<PanelEntry> entries)) return;
            SortEntries(entries);
            var siblingIndex = 0;
            for (var index = 0; index < entries.Count; index++)
            {
                PanelEntry entry = entries[index];
                if (entry.ModalBlocker != default) entry.ModalBlocker.transform.SetSiblingIndex(siblingIndex++);
                if (entry.Panel != default) entry.Panel.transform.SetSiblingIndex(siblingIndex++);
            }
        }

        /// <summary>
        /// 按 Level、SubLevel、打开序号和类型名形成确定性顺序。
        /// </summary>
        private static int CompareEntries(PanelEntry left, PanelEntry right)
        {
            int level = left.Level.CompareTo(right.Level);
            if (level != 0) return level;
            int subLevel = left.SubLevel.CompareTo(right.SubLevel);
            if (subLevel != 0) return subLevel;
            int sequence = left.OpenSequence.CompareTo(right.OpenSequence);
            return sequence != 0
                ? sequence
                : string.Compare(left.PanelType.FullName, right.PanelType.FullName, StringComparison.Ordinal);
        }

        /// <summary>
        /// 原地排序一个层级的 entry 列表。
        /// </summary>
        private static void SortEntries(List<PanelEntry> entries)
        {
            entries.Sort(CompareEntries);
        }
    }
}
#endif
