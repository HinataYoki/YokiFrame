using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UI 层级管理器 - 管理各层级面板列表、排序和模态阻断
    /// </summary>
    internal static partial class UILevelManager
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

    }
}
