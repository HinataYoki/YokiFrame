#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>只通过 UIKit 查询 API 构建 Editor 观察快照，查询过程不会创建 UIRoot。</summary>
    internal static class UIKitSnapshotBuilder
    {
        /// <summary>采集当前 UIKit 的面板、栈、Root、缓存和模态状态。</summary>
        /// <returns>与采集时刻对应的稳定排序快照。</returns>
        internal static UIKitInteractionSnapshot Create()
        {
            var snapshot = new UIKitInteractionSnapshot
            {
                Root = BuildRoot(),
                Panels = BuildPanels(),
                Stacks = BuildStacks()
            };
            snapshot.Stats = BuildStats(snapshot.Panels, snapshot.Stacks);
            snapshot.Cache = BuildCache(snapshot.Panels);
            snapshot.Modal = BuildModal(snapshot.Panels);
            return snapshot;
        }

        /// <summary>通过无副作用的布尔查询判断当前 UIKit 是否已经创建。</summary>
        /// <returns>Root 存在性快照。</returns>
        private static UIKitRootSnapshot BuildRoot()
        {
            return new UIKitRootSnapshot { Exists = UIKit.HasRoot };
        }

        /// <summary>复制全部有效面板并按完整运行时类型名稳定排序。</summary>
        /// <returns>不包含 Data、Transform 或 Unity instanceId 的面板列表。</returns>
        private static List<UIKitPanelSnapshot> BuildPanels()
        {
            IReadOnlyList<IPanel> loaded = UIKit.GetLoadedPanels();
            var panels = new List<UIKitPanelSnapshot>(loaded.Count);
            for (var index = 0; index < loaded.Count; index++)
            {
                IPanel panel = loaded[index];
                if (!IsPanelAlive(panel)) continue;
                panels.Add(BuildPanel(panel));
            }

            panels.Sort(static (left, right) =>
                string.Compare(left.Type, right.Type, StringComparison.Ordinal));
            return panels;
        }

        /// <summary>从面板公开只读契约复制单项状态。</summary>
        /// <param name="panel">仍由 UIKit 管理的有效面板。</param>
        /// <returns>剔除业务 Data 与 Unity 标识后的快照。</returns>
        private static UIKitPanelSnapshot BuildPanel(IPanel panel)
        {
            Type panelType = panel.GetType();
            UILevel level = panel.Level;
            return new UIKitPanelSnapshot
            {
                Type = panelType.FullName ?? panelType.Name,
                Name = panel.PanelName,
                State = panel.State.ToString(),
                Level = level.ToString(),
                LevelOrder = level.Order,
                SubLevel = panel.SubLevel,
                CachePolicy = panel.CachePolicy.ToString(),
                IsModal = panel.IsModal,
                StackName = panel.StackName
            };
        }

        /// <summary>使用 Unity fake-null 语义过滤采集期间刚被销毁的面板。</summary>
        /// <param name="panel">候选面板引用。</param>
        /// <returns>面板仍有有效 Unity 对象时返回 true。</returns>
        private static bool IsPanelAlive(IPanel panel)
        {
            if (panel == null) return false;
            var unityObject = panel as UnityEngine.Object;
            return unityObject != default;
        }

        /// <summary>复制全部非空命名栈并按 ordinal 名称稳定排序。</summary>
        /// <returns>命名栈摘要列表。</returns>
        private static List<UIKitStackSnapshot> BuildStacks()
        {
            IReadOnlyCollection<string> names = UIKit.GetAllStackNames();
            var sortedNames = new List<string>(names);
            sortedNames.Sort(StringComparer.Ordinal);
            var stacks = new List<UIKitStackSnapshot>(sortedNames.Count);
            for (var index = 0; index < sortedNames.Count; index++)
            {
                stacks.Add(BuildStack(sortedNames[index]));
            }

            return stacks;
        }

        /// <summary>读取一个命名栈的深度和顶部面板，不改变焦点或可见性。</summary>
        /// <param name="stackName">已由 UIKit 返回的非空栈名称。</param>
        /// <returns>栈摘要。</returns>
        private static UIKitStackSnapshot BuildStack(string stackName)
        {
            IPanel top = UIKit.PeekPanel(stackName);
            Type topType = IsPanelAlive(top) ? top.GetType() : null;
            return new UIKitStackSnapshot
            {
                Name = stackName,
                Depth = UIKit.GetStackDepth(stackName),
                TopPanelType = topType == null ? null : topType.FullName ?? topType.Name,
                TopPanelName = topType == null ? null : top.PanelName
            };
        }

        /// <summary>汇总面板生命周期和栈成员数量。</summary>
        /// <param name="panels">稳定面板快照。</param>
        /// <param name="stacks">稳定命名栈快照。</param>
        /// <returns>数量汇总。</returns>
        private static UIKitStatsSnapshot BuildStats(
            List<UIKitPanelSnapshot> panels,
            List<UIKitStackSnapshot> stacks)
        {
            var stats = new UIKitStatsSnapshot
            {
                PanelCount = panels.Count,
                StackCount = stacks.Count
            };
            for (var index = 0; index < panels.Count; index++) CountState(stats, panels[index].State);
            for (var index = 0; index < stacks.Count; index++)
                stats.StackMembershipCount += stacks[index].Depth;
            return stats;
        }

        /// <summary>把单个面板生命周期状态计入固定状态桶。</summary>
        /// <param name="stats">待更新汇总。</param>
        /// <param name="state">公开 PanelState 名称。</param>
        private static void CountState(UIKitStatsSnapshot stats, string state)
        {
            switch (state)
            {
                case nameof(PanelState.Preloaded): stats.PreloadedCount++; return;
                case nameof(PanelState.Opening): stats.OpeningCount++; return;
                case nameof(PanelState.Open): stats.OpenCount++; return;
                case nameof(PanelState.Hiding): stats.HidingCount++; return;
                case nameof(PanelState.Hide): stats.HiddenCount++; return;
                case nameof(PanelState.Closing): stats.ClosingCount++; return;
                case nameof(PanelState.Cached): stats.CachedCount++; return;
                case nameof(PanelState.Close): stats.ClosedCount++; return;
            }
        }

        /// <summary>汇总显式缓存策略及 Reusable 已关闭缓存数量。</summary>
        /// <param name="panels">稳定面板快照。</param>
        /// <returns>缓存策略汇总。</returns>
        private static UIKitCacheSnapshot BuildCache(List<UIKitPanelSnapshot> panels)
        {
            var cache = new UIKitCacheSnapshot { Capacity = UIKit.ReusableCacheCapacity };
            for (var index = 0; index < panels.Count; index++)
            {
                UIKitPanelSnapshot panel = panels[index];
                if (panel.CachePolicy == nameof(PanelCachePolicy.Transient)) cache.TransientCount++;
                else if (panel.CachePolicy == nameof(PanelCachePolicy.Reusable)) cache.ReusableCount++;
                else if (panel.CachePolicy == nameof(PanelCachePolicy.Persistent)) cache.PersistentCount++;
                if (panel.CachePolicy == nameof(PanelCachePolicy.Reusable)
                    && panel.State == nameof(PanelState.Cached)) cache.ReusableCachedCount++;
            }

            return cache;
        }

        /// <summary>汇总模态面板数量和当前 blocker 存在性。</summary>
        /// <param name="panels">稳定面板快照。</param>
        /// <returns>模态状态汇总。</returns>
        private static UIKitModalSnapshot BuildModal(List<UIKitPanelSnapshot> panels)
        {
            var modal = new UIKitModalSnapshot { BlockerActive = UIKit.HasModalBlocker() };
            for (var index = 0; index < panels.Count; index++)
            {
                if (panels[index].IsModal) modal.PanelCount++;
            }

            return modal;
        }
    }
}
#endif
