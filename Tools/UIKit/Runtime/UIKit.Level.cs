using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// UI 管理工具 - 层级管理
    /// </summary>
    public partial class UIKit
    {
        #region 层级

        /// <summary>
        /// 设置面板层级
        /// </summary>
        public static void SetPanelLevel(IPanel panel, UILevel level, int subLevel = 0)
        {
            var root = Root;
            if (root == default) return;
            root.SetPanelLevel(panel, level, subLevel);
        }

        /// <summary>
        /// 设置面板子层级
        /// </summary>
        public static void SetPanelSubLevel(IPanel panel, int subLevel)
        {
            var root = Root;
            if (root == default) return;
            root.SetPanelSubLevel(panel, subLevel);
        }

        /// <summary>
        /// 获取指定层级的顶部面板
        /// </summary>
        public static IPanel GetTopPanelAtLevel(UILevel level)
        {
            var root = Root;
            if (root == default) return null;
            return root.GetTopPanelAtLevel(level);
        }

        /// <summary>
        /// 获取全局顶部面板
        /// </summary>
        public static IPanel GetGlobalTopPanel()
        {
            var root = Root;
            if (root == default) return null;
            return root.GetGlobalTopPanel();
        }

        /// <summary>
        /// 获取指定层级的所有面板
        /// </summary>
        public static IReadOnlyList<IPanel> GetPanelsAtLevel(UILevel level)
        {
            var root = Root;
            return root != default ? root.GetPanelsAtLevel(level) : Array.Empty<IPanel>();
        }

        /// <summary>
        /// 设置面板为模态
        /// </summary>
        public static void SetPanelModal(IPanel panel, bool isModal)
        {
            var root = Root;
            if (root == default) return;
            root.SetPanelModal(panel, isModal);
        }

        /// <summary>
        /// 检查是否有模态面板
        /// </summary>
        public static bool HasModalBlocker()
        {
            var root = Root;
            return root != default ? root.HasModalBlocker() : false;
        }

        #endregion
    }
}
