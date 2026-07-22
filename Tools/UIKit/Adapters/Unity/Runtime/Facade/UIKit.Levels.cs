#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public static partial class UIKit
    {
        /// <summary>
        /// 设置受管面板的 UILevel 与层内子层级。
        /// </summary>
        public static void SetPanelLevel(IPanel panel, UILevel level, int subLevel = 0)
        {
            UIKitController controller = GetExistingController();
            if (controller != null && panel != null) controller.SetLevel(panel, level, subLevel);
        }

        /// <summary>
        /// 设置受管面板在当前 UILevel 内的子层级。
        /// </summary>
        public static void SetPanelSubLevel(IPanel panel, int subLevel)
        {
            UIKitController controller = GetExistingController();
            if (controller != null && panel != null) controller.SetSubLevel(panel, subLevel);
        }

        /// <summary>
        /// 获取指定 UILevel 当前最顶部的可见面板。
        /// </summary>
        public static IPanel GetTopPanelAtLevel(UILevel level)
        {
            UIKitController controller = GetExistingController();
            return controller == null ? null : controller.GetTopAtLevel(level);
        }

        /// <summary>
        /// 获取全局排序最高的可见面板。
        /// </summary>
        public static IPanel GetGlobalTopPanel()
        {
            UIKitController controller = GetExistingController();
            return controller == null ? null : controller.GetGlobalTop();
        }

        /// <summary>
        /// 获取指定 UILevel 仍处于打开轮次的面板快照。
        /// </summary>
        public static IReadOnlyList<IPanel> GetPanelsAtLevel(UILevel level)
        {
            UIKitController controller = GetExistingController();
            return controller == null ? Array.Empty<IPanel>() : controller.GetPanelsAtLevel(level);
        }

        /// <summary>
        /// 设置面板的模态状态；blocker 仅由 UIKit owner 创建和销毁。
        /// </summary>
        public static void SetPanelModal(IPanel panel, bool isModal)
        {
            UIKitController controller = GetExistingController();
            if (controller != null && panel != null) controller.SetModal(panel, isModal);
        }

        /// <summary>
        /// 判断当前是否存在有效模态 blocker。
        /// </summary>
        public static bool HasModalBlocker()
        {
            UIKitController controller = GetExistingController();
            return controller != null && controller.HasModalBlocker();
        }
    }
}
#endif
