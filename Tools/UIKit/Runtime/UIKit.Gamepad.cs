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
        public static UIInputMode CurrentInputMode
        {
            get
            {
                var focusSystem = FocusSystem;
                return focusSystem != default ? focusSystem.CurrentInputMode : UIInputMode.Pointer;
            }
        }

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
            var focusSystem = FocusSystem;
            if (focusSystem != default)
            {
                focusSystem.GamepadEnabled = true;
            }
        }

        /// <summary>
        /// 禁用手柄支持
        /// </summary>
        public static void DisableGamepad()
        {
            var focusSystem = FocusSystem;
            if (focusSystem != default)
            {
                focusSystem.GamepadEnabled = false;
            }
        }

        /// <summary>
        /// 显示焦点高亮
        /// </summary>
        public static void ShowFocusHighlight()
        {
            var focusSystem = FocusSystem;
            if (focusSystem != default)
            {
                var highlight = focusSystem.FocusHighlight;
                if (highlight != default)
                {
                    highlight.Show();
                }
            }
        }

        /// <summary>
        /// 隐藏焦点高亮
        /// </summary>
        public static void HideFocusHighlight()
        {
            var focusSystem = FocusSystem;
            if (focusSystem != default)
            {
                var highlight = focusSystem.FocusHighlight;
                if (highlight != default)
                {
                    highlight.Hide();
                }
            }
        }

        #endregion

        #region 导航配置

        /// <summary>
        /// 配置网格导航
        /// </summary>
        public static void ConfigureGridNavigation(UINavigationGrid grid)
        {
            if (grid != default)
            {
                grid.ConfigureNavigation();
            }
        }

        /// <summary>
        /// 配置组导航
        /// </summary>
        public static void ConfigureGroupNavigation(SelectableGroup group)
        {
            if (group != default)
            {
                group.ConfigureNavigation();
            }
        }

        #endregion
    }
}
