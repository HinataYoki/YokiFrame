namespace YokiFrame
{
    /// <summary>
    /// UIKit 手柄支持扩展
    /// </summary>
    public partial class UIKit
    {
        #region 手柄 API

        /// <summary>
        /// 当前输入模式
        /// </summary>
        public static UIInputMode CurrentInputMode => FocusSystem?.CurrentInputMode ?? UIInputMode.Pointer;

        /// <summary>
        /// 是否处于手柄/键盘导航模式
        /// </summary>
        public static bool IsNavigationMode => CurrentInputMode == UIInputMode.Navigation;

        /// <summary>
        /// 是否处于鼠标/触摸模式
        /// </summary>
        public static bool IsPointerMode => CurrentInputMode == UIInputMode.Pointer;

        /// <summary>
        /// 启用手柄支持
        /// </summary>
        public static void EnableGamepad()
        {
            if (FocusSystem != null)
            {
                FocusSystem.GamepadEnabled = true;
            }
        }

        /// <summary>
        /// 禁用手柄支持
        /// </summary>
        public static void DisableGamepad()
        {
            if (FocusSystem != null)
            {
                FocusSystem.GamepadEnabled = false;
            }
        }

        /// <summary>
        /// 显示焦点高亮
        /// </summary>
        public static void ShowFocusHighlight()
        {
            FocusSystem?.FocusHighlight?.Show();
        }

        /// <summary>
        /// 隐藏焦点高亮
        /// </summary>
        public static void HideFocusHighlight()
        {
            FocusSystem?.FocusHighlight?.Hide();
        }

        #endregion

        #region 导航配置

        /// <summary>
        /// 配置网格导航
        /// </summary>
        public static void ConfigureGridNavigation(UINavigationGrid grid)
        {
            grid?.ConfigureNavigation();
        }

        /// <summary>
        /// 配置组导航
        /// </summary>
        public static void ConfigureGroupNavigation(SelectableGroup group)
        {
            group?.ConfigureNavigation();
        }

        #endregion
    }
}
