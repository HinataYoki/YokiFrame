using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UI 层级管理器 - 管理各层级面板列表、排序和模态阻断
    /// </summary>
    internal static class UILevelManager
    {
        #region 层级面板列表

        /// <summary>
        /// 各层级的面板列表（按 SubLevel 和 OpenTimestamp 排序）
        /// </summary>
        private static readonly Dictionary<UILevel, List<IPanel>> sLevelPanels = new();

        /// <summary>
        /// 模态遮罩对象池
        /// </summary>
        private static readonly Dictionary<IPanel, GameObject> sModalBlockers = new();

        /// <summary>
        /// 模态遮罩预制体
        /// </summary>
        private static GameObject sModalBlockerPrefab;

        #endregion

        #region 初始化

        static UILevelManager()
        {
            foreach (UILevel level in Enum.GetValues(typeof(UILevel)))
            {
                sLevelPanels[level] = new List<IPanel>();
            }
        }

        #endregion

        #region 面板注册

        /// <summary>
        /// 注册面板到层级
        /// </summary>
        public static void Register(IPanel panel)
        {
            if (panel?.Handler == null) return;

            var level = panel.Handler.Level;
            if (!sLevelPanels.TryGetValue(level, out var list))
            {
                list = new List<IPanel>();
                sLevelPanels[level] = list;
            }

            if (!list.Contains(panel))
            {
                panel.Handler.OpenTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                list.Add(panel);
                SortLevel(level);
            }

            // 处理模态面板
            if (panel.Handler.IsModal)
            {
                ShowModalBlocker(panel);
            }
        }

        /// <summary>
        /// 从层级注销面板
        /// </summary>
        public static void Unregister(IPanel panel)
        {
            if (panel?.Handler == null) return;

            var level = panel.Handler.Level;
            if (sLevelPanels.TryGetValue(level, out var list))
            {
                list.Remove(panel);
            }

            // 移除模态遮罩
            HideModalBlocker(panel);
        }

        #endregion

        #region 层级排序

        /// <summary>
        /// 对指定层级的面板进行排序
        /// </summary>
        public static void SortLevel(UILevel level)
        {
            if (!sLevelPanels.TryGetValue(level, out var list) || list.Count == 0) return;

            // 按 SubLevel 升序，相同 SubLevel 按 OpenTimestamp 升序
            list.Sort((a, b) =>
            {
                var subLevelCompare = a.Handler.SubLevel.CompareTo(b.Handler.SubLevel);
                if (subLevelCompare != 0) return subLevelCompare;
                return a.Handler.OpenTimestamp.CompareTo(b.Handler.OpenTimestamp);
            });

            // 更新 sibling index
            for (int i = 0; i < list.Count; i++)
            {
                var panel = list[i];
                if (panel?.Transform != null)
                {
                    panel.Transform.SetSiblingIndex(i);
                }
            }
        }

        /// <summary>
        /// 对所有层级进行排序
        /// </summary>
        public static void SortAllLevels()
        {
            foreach (var level in sLevelPanels.Keys)
            {
                SortLevel(level);
            }
        }

        #endregion

        #region 层级切换

        /// <summary>
        /// 设置面板层级
        /// </summary>
        public static void SetPanelLevel(IPanel panel, UILevel newLevel, int subLevel = 0)
        {
            if (panel?.Handler == null) return;

            var oldLevel = panel.Handler.Level;
            
            // 从旧层级移除
            if (sLevelPanels.TryGetValue(oldLevel, out var oldList))
            {
                oldList.Remove(panel);
            }

            // 更新 Handler
            panel.Handler.Level = newLevel;
            panel.Handler.SubLevel = subLevel;
            panel.Handler.OpenTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 移动到新层级（仅在运行时有 UIRoot 时）
            if (Application.isPlaying && UIRoot.Instance != null)
            {
                UIRoot.Instance.SetLevelOfPanel(newLevel, panel);
            }

            // 添加到新层级列表
            if (!sLevelPanels.TryGetValue(newLevel, out var newList))
            {
                newList = new List<IPanel>();
                sLevelPanels[newLevel] = newList;
            }
            newList.Add(panel);

            // 重新排序
            SortLevel(oldLevel);
            SortLevel(newLevel);
        }

        /// <summary>
        /// 设置面板子层级
        /// </summary>
        public static void SetPanelSubLevel(IPanel panel, int subLevel)
        {
            if (panel?.Handler == null) return;

            panel.Handler.SubLevel = subLevel;
            SortLevel(panel.Handler.Level);
        }

        #endregion

        #region 查询

        /// <summary>
        /// 获取指定层级的顶部面板
        /// </summary>
        public static IPanel GetTopPanelAtLevel(UILevel level)
        {
            if (!sLevelPanels.TryGetValue(level, out var list) || list.Count == 0)
            {
                return null;
            }

            // 返回最后一个（最顶部）
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var panel = list[i];
                if (panel?.Transform != null && panel.Transform.gameObject.activeInHierarchy)
                {
                    return panel;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取指定层级的所有面板
        /// </summary>
        public static IReadOnlyList<IPanel> GetPanelsAtLevel(UILevel level)
        {
            if (sLevelPanels.TryGetValue(level, out var list))
            {
                return list;
            }
            return Array.Empty<IPanel>();
        }

        /// <summary>
        /// 获取全局顶部面板
        /// </summary>
        public static IPanel GetGlobalTopPanel()
        {
            // 从最高层级开始查找
            var levels = new[] { UILevel.AlwayTop, UILevel.Pop, UILevel.Common, UILevel.Bg, UILevel.AlwayBottom };
            foreach (var level in levels)
            {
                var panel = GetTopPanelAtLevel(level);
                if (panel != null) return panel;
            }
            return null;
        }

        /// <summary>
        /// 获取指定层级的面板数量
        /// </summary>
        public static int GetPanelCountAtLevel(UILevel level)
        {
            if (sLevelPanels.TryGetValue(level, out var list))
            {
                return list.Count;
            }
            return 0;
        }

        #endregion

        #region 模态阻断

        /// <summary>
        /// 显示模态遮罩
        /// </summary>
        private static void ShowModalBlocker(IPanel panel)
        {
            if (panel?.Transform == null) return;
            if (sModalBlockers.ContainsKey(panel)) return;

            var blocker = CreateModalBlocker(panel);
            if (blocker != null)
            {
                sModalBlockers[panel] = blocker;
                UpdateModalBlockerInteractivity();
            }
        }

        /// <summary>
        /// 隐藏模态遮罩
        /// </summary>
        private static void HideModalBlocker(IPanel panel)
        {
            if (panel == null) return;
            
            if (sModalBlockers.TryGetValue(panel, out var blocker))
            {
                if (blocker != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEngine.Object.DestroyImmediate(blocker);
                    }
                    else
#endif
                    {
                        UnityEngine.Object.Destroy(blocker);
                    }
                }
                sModalBlockers.Remove(panel);
                UpdateModalBlockerInteractivity();
            }
        }

        /// <summary>
        /// 创建模态遮罩
        /// </summary>
        private static GameObject CreateModalBlocker(IPanel panel)
        {
            var parent = panel.Transform.parent;
            if (parent == null) return null;

            var blocker = new GameObject($"ModalBlocker_{panel.Handler.Type.Name}");
            var rect = blocker.AddComponent<RectTransform>();
            rect.SetParent(parent);
            
            // 设置为全屏
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;

            // 添加 Image 组件用于阻断射线
            var image = blocker.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.5f); // 半透明黑色
            image.raycastTarget = true;

            // 设置在面板下方
            var panelIndex = panel.Transform.GetSiblingIndex();
            rect.SetSiblingIndex(Mathf.Max(0, panelIndex));

            return blocker;
        }

        /// <summary>
        /// 更新所有模态遮罩的交互状态
        /// </summary>
        private static void UpdateModalBlockerInteractivity()
        {
            // 禁用所有非顶部模态面板下方的交互
            foreach (var kvp in sLevelPanels)
            {
                var list = kvp.Value;
                bool hasModalAbove = false;

                // 从顶部向下遍历
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var panel = list[i];
                    if (panel?.Transform == null) continue;

                    var raycaster = panel.Transform.GetComponent<GraphicRaycaster>();
                    if (raycaster != null)
                    {
                        raycaster.enabled = !hasModalAbove;
                    }

                    // 禁用 CanvasGroup 交互
                    var canvasGroup = panel.Transform.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
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

        /// <summary>
        /// 设置面板为模态
        /// </summary>
        public static void SetModal(IPanel panel, bool isModal)
        {
            if (panel?.Handler == null) return;

            var wasModal = panel.Handler.IsModal;
            panel.Handler.IsModal = isModal;

            if (isModal && !wasModal)
            {
                ShowModalBlocker(panel);
            }
            else if (!isModal && wasModal)
            {
                HideModalBlocker(panel);
            }
        }

        /// <summary>
        /// 检查是否有模态面板阻断
        /// </summary>
        public static bool HasModalBlocker()
        {
            return sModalBlockers.Count > 0;
        }

        #endregion

        #region 清理

        /// <summary>
        /// 清理所有层级数据
        /// </summary>
        public static void ClearAll()
        {
            foreach (var list in sLevelPanels.Values)
            {
                list.Clear();
            }

            foreach (var blocker in sModalBlockers.Values)
            {
                if (blocker != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEngine.Object.DestroyImmediate(blocker);
                    }
                    else
#endif
                    {
                        UnityEngine.Object.Destroy(blocker);
                    }
                }
            }
            sModalBlockers.Clear();
        }

        /// <summary>
        /// 清理指定层级
        /// </summary>
        public static void ClearLevel(UILevel level)
        {
            if (sLevelPanels.TryGetValue(level, out var list))
            {
                // 移除该层级面板的模态遮罩
                foreach (var panel in list)
                {
                    HideModalBlocker(panel);
                }
                list.Clear();
            }
        }

        #endregion
    }
}
