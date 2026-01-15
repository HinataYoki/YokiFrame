using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UIRoot - 层级子系统
    /// </summary>
    public partial class UIRoot
    {
        #region 层级数据

        /// <summary>
        /// 各层级的面板列表
        /// </summary>
        private readonly Dictionary<UILevel, List<IPanel>> mLevelPanels = new();

        /// <summary>
        /// 模态遮罩
        /// </summary>
        private readonly Dictionary<IPanel, GameObject> mModalBlockers = new();

        #endregion

        #region 层级初始化

        private void InitializeLevelPanels()
        {
            foreach (UILevel level in Enum.GetValues(typeof(UILevel)))
            {
                mLevelPanels[level] = new List<IPanel>();
            }
        }

        #endregion

        #region 面板注册

        /// <summary>
        /// 注册面板到层级
        /// </summary>
        internal void RegisterPanelToLevel(IPanel panel)
        {
            if (panel?.Handler == default) return;

            var level = panel.Handler.Level;
            if (!mLevelPanels.TryGetValue(level, out var list))
            {
                list = new List<IPanel>();
                mLevelPanels[level] = list;
            }

            if (!list.Contains(panel))
            {
                panel.Handler.OpenTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                list.Add(panel);
                SortLevel(level);
            }

            if (panel.Handler.IsModal) ShowModalBlocker(panel);
        }

        /// <summary>
        /// 从层级注销面板
        /// </summary>
        internal void UnregisterPanelFromLevel(IPanel panel)
        {
            if (panel?.Handler == default) return;

            var level = panel.Handler.Level;
            if (mLevelPanels.TryGetValue(level, out var list))
            {
                list.Remove(panel);
            }

            HideModalBlocker(panel);
        }

        #endregion

        #region 层级操作

        /// <summary>
        /// 设置面板层级
        /// </summary>
        public void SetPanelLevel(IPanel panel, UILevel newLevel, int subLevel = 0)
        {
            if (panel?.Handler == default) return;

            var oldLevel = panel.Handler.Level;

            // 从旧层级移除
            if (mLevelPanels.TryGetValue(oldLevel, out var oldList))
            {
                oldList.Remove(panel);
            }

            // 更新 Handler
            panel.Handler.Level = newLevel;
            panel.Handler.SubLevel = subLevel;
            panel.Handler.OpenTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 移动到新层级节点
            if (Application.isPlaying)
            {
                SetLevelOfPanel(newLevel, panel);
            }

            // 添加到新层级列表
            if (!mLevelPanels.TryGetValue(newLevel, out var newList))
            {
                newList = new List<IPanel>();
                mLevelPanels[newLevel] = newList;
            }
            newList.Add(panel);

            SortLevel(oldLevel);
            SortLevel(newLevel);
        }

        /// <summary>
        /// 设置面板子层级
        /// </summary>
        public void SetPanelSubLevel(IPanel panel, int subLevel)
        {
            if (panel?.Handler == default) return;
            panel.Handler.SubLevel = subLevel;
            SortLevel(panel.Handler.Level);
        }

        /// <summary>
        /// 获取指定层级的顶部面板
        /// </summary>
        public IPanel GetTopPanelAtLevel(UILevel level)
        {
            if (!mLevelPanels.TryGetValue(level, out var list) || list.Count == 0)
            {
                return null;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var panel = list[i];
                if (panel?.Transform != default && panel.Transform.gameObject.activeInHierarchy)
                {
                    return panel;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取全局顶部面板
        /// </summary>
        public IPanel GetGlobalTopPanel()
        {
            var levels = new[] { UILevel.AlwayTop, UILevel.Pop, UILevel.Common, UILevel.Bg, UILevel.AlwayBottom };
            for (int i = 0; i < levels.Length; i++)
            {
                var panel = GetTopPanelAtLevel(levels[i]);
                if (panel != default) return panel;
            }
            return null;
        }

        /// <summary>
        /// 获取指定层级的所有面板
        /// </summary>
        public IReadOnlyList<IPanel> GetPanelsAtLevel(UILevel level)
        {
            if (mLevelPanels.TryGetValue(level, out var list)) return list;
            return Array.Empty<IPanel>();
        }

        #endregion

        #region 层级排序

        private void SortLevel(UILevel level)
        {
            if (!mLevelPanels.TryGetValue(level, out var list) || list.Count == 0) return;

            list.Sort(static (a, b) =>
            {
                var subLevelCompare = a.Handler.SubLevel.CompareTo(b.Handler.SubLevel);
                if (subLevelCompare != 0) return subLevelCompare;
                return a.Handler.OpenTimestamp.CompareTo(b.Handler.OpenTimestamp);
            });

            for (int i = 0; i < list.Count; i++)
            {
                var panel = list[i];
                if (panel?.Transform != default)
                {
                    panel.Transform.SetSiblingIndex(i);
                }
            }
        }

        #endregion

        #region 模态遮罩

        /// <summary>
        /// 设置面板为模态
        /// </summary>
        public void SetPanelModal(IPanel panel, bool isModal)
        {
            if (panel?.Handler == default) return;

            var wasModal = panel.Handler.IsModal;
            panel.Handler.IsModal = isModal;

            if (isModal && !wasModal) ShowModalBlocker(panel);
            else if (!isModal && wasModal) HideModalBlocker(panel);
        }

        /// <summary>
        /// 检查是否有模态面板
        /// </summary>
        public bool HasModalBlocker() => mModalBlockers.Count > 0;

        private void ShowModalBlocker(IPanel panel)
        {
            if (panel?.Transform == default) return;
            if (mModalBlockers.ContainsKey(panel)) return;

            var blocker = CreateModalBlocker(panel);
            if (blocker != default)
            {
                mModalBlockers[panel] = blocker;
                UpdateModalBlockerInteractivity();
            }
        }

        private void HideModalBlocker(IPanel panel)
        {
            if (panel == default) return;

            if (mModalBlockers.TryGetValue(panel, out var blocker))
            {
                if (blocker != default)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        UnityEngine.Object.DestroyImmediate(blocker);
                    else
#endif
                    UnityEngine.Object.Destroy(blocker);
                }
                mModalBlockers.Remove(panel);
                UpdateModalBlockerInteractivity();
            }
        }

        private GameObject CreateModalBlocker(IPanel panel)
        {
            var parent = panel.Transform.parent;
            if (parent == default) return null;

            var blocker = new GameObject($"ModalBlocker_{panel.Handler.Type.Name}");
            var rect = blocker.AddComponent<RectTransform>();
            rect.SetParent(parent);

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;

            var image = blocker.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.5f);
            image.raycastTarget = true;

            var panelIndex = panel.Transform.GetSiblingIndex();
            rect.SetSiblingIndex(Mathf.Max(0, panelIndex));

            return blocker;
        }

        private void UpdateModalBlockerInteractivity()
        {
            foreach (var kvp in mLevelPanels)
            {
                var list = kvp.Value;
                bool hasModalAbove = false;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var panel = list[i];
                    if (panel?.Transform == default) continue;

                    if (panel.Transform.TryGetComponent<GraphicRaycaster>(out var raycaster))
                    {
                        raycaster.enabled = !hasModalAbove;
                    }

                    if (panel.Transform.TryGetComponent<CanvasGroup>(out var canvasGroup))
                    {
                        canvasGroup.interactable = !hasModalAbove;
                        canvasGroup.blocksRaycasts = !hasModalAbove || panel.Handler.IsModal;
                    }

                    if (panel.Handler.IsModal && panel.Transform.gameObject.activeInHierarchy)
                    {
                        hasModalAbove = true;
                    }
                }
            }
        }

        #endregion

        #region 清理

        internal void ClearAllLevels()
        {
            foreach (var list in mLevelPanels.Values) list.Clear();

            foreach (var blocker in mModalBlockers.Values)
            {
                if (blocker != default)
                {
                    // OnDestroy 中必须使用 DestroyImmediate 避免延迟销毁警告
                    UnityEngine.Object.DestroyImmediate(blocker);
                }
            }
            mModalBlockers.Clear();
        }

        #endregion
    }
}
