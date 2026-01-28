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

        /// <summary>
        /// 模态遮罩对象池（懒加载）
        /// 修复：避免静态构造函数中创建 GameObject
        /// </summary>
        private static SimplePoolKit<GameObject> sModalBlockerPool;

        /// <summary>
        /// 获取模态遮罩对象池（延迟初始化）
        /// </summary>
        private static SimplePoolKit<GameObject> ModalBlockerPool
        {
            get
            {
                if (sModalBlockerPool == default)
                {
                    sModalBlockerPool = new SimplePoolKit<GameObject>(
                        factoryMethod: static () =>
                        {
                            var obj = new GameObject("ModalBlocker");
                            var rect = obj.AddComponent<RectTransform>();
                            var image = obj.AddComponent<Image>();
                            image.color = new Color(0, 0, 0, 0.5f);
                            image.raycastTarget = true;
                            return obj;
                        },
                        resetMethod: static obj =>
                        {
                            if (obj != default)
                            {
                                obj.SetActive(false);
                                obj.transform.SetParent(null);
                            }
                        },
                        initCount: 4
                    );
                }
                return sModalBlockerPool;
            }
        }

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
        /// <param name="panel">要注册的面板</param>
        internal void RegisterPanelToLevel(IPanel panel)
        {
            if (panel == default || panel.Handler == default) return;

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
        /// <param name="panel">要注销的面板</param>
        internal void UnregisterPanelFromLevel(IPanel panel)
        {
            if (panel == default || panel.Handler == default) return;

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
        /// <param name="panel">面板</param>
        /// <param name="newLevel">新层级</param>
        /// <param name="subLevel">子层级</param>
        public void SetPanelLevel(IPanel panel, UILevel newLevel, int subLevel = 0)
        {
            if (panel == default || panel.Handler == default) return;

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
        /// <param name="panel">面板</param>
        /// <param name="subLevel">子层级</param>
        public void SetPanelSubLevel(IPanel panel, int subLevel)
        {
            if (panel == default || panel.Handler == default) return;
            panel.Handler.SubLevel = subLevel;
            SortLevel(panel.Handler.Level);
        }

        /// <summary>
        /// 获取指定层级的顶部面板
        /// </summary>
        /// <param name="level">层级</param>
        /// <returns>顶部面板，没有则返回 null</returns>
        public IPanel GetTopPanelAtLevel(UILevel level)
        {
            if (!mLevelPanels.TryGetValue(level, out var list) || list.Count == 0)
            {
                return null;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var panel = list[i];
                if (panel != default && panel.Transform != default && panel.Transform.gameObject.activeInHierarchy)
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
                if (panel != default && panel.Transform != default)
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
        /// <param name="panel">面板</param>
        /// <param name="isModal">是否为模态</param>
        public void SetPanelModal(IPanel panel, bool isModal)
        {
            if (panel == default || panel.Handler == default) return;

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
            if (panel == default || panel.Transform == default) return;
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
                    // 归还到对象池
                    ModalBlockerPool.Recycle(blocker);
                }
                mModalBlockers.Remove(panel);
                UpdateModalBlockerInteractivity();
            }
        }

        private GameObject CreateModalBlocker(IPanel panel)
        {
            var parent = panel.Transform.parent;
            if (parent == default) return null;

            // 从对象池获取遮罩
            var blocker = ModalBlockerPool.Allocate();
            blocker.SetActive(true);
            var rect = blocker.GetComponent<RectTransform>();
            rect.SetParent(parent);

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;

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
                    if (panel == default || panel.Transform == default) continue;

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

            // 归还所有模态遮罩到对象池
            foreach (var blocker in mModalBlockers.Values)
            {
                if (blocker != default && blocker != null)
                {
                    ModalBlockerPool.Recycle(blocker);
                }
            }
            mModalBlockers.Clear();
        }

        #endregion
    }
}
