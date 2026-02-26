namespace YokiFrame
{
    /// <summary>
    /// UI 管理工具 - 静态门面
    /// 所有调用转发到 UIRoot 实例
    /// </summary>
    public partial class UIKit
    {
        #region 初始化

        static UIKit() => _ = UIRoot.Instance;

        /// <summary>
        /// 获取 UIRoot 实例（退出时返回 null）
        /// </summary>
        private static UIRoot Root => UIRoot.Instance;

        #endregion

        #region 配置

        /// <summary>
        /// 创建界面时赋予的热度值
        /// </summary>
        public static int OpenHot
        {
            get => Root?.OpenHot ?? 0;
            set { if (Root != default) Root.OpenHot = value; }
        }

        /// <summary>
        /// 获取界面时赋予的热度值
        /// </summary>
        public static int GetHot
        {
            get => Root?.GetHot ?? 0;
            set { if (Root != default) Root.GetHot = value; }
        }

        /// <summary>
        /// 每次行为造成的衰减热度值
        /// </summary>
        public static int Weaken
        {
            get => Root?.Weaken ?? 0;
            set { if (Root != default) Root.Weaken = value; }
        }

        #endregion

        #region 焦点

        /// <summary>
        /// 焦点系统是否启用
        /// </summary>
        public static bool FocusSystemEnabled
        {
            get => Root?.FocusSystemEnabled ?? false;
            set { if (Root != default) Root.FocusSystemEnabled = value; }
        }

        /// <summary>
        /// 当前输入模式
        /// </summary>
        public static UIInputMode GetInputMode() => Root?.CurrentInputMode ?? UIInputMode.Pointer;

        /// <summary>
        /// 设置焦点
        /// </summary>
        public static void SetFocus(UnityEngine.GameObject target) => Root?.SetFocus(target);

        /// <summary>
        /// 设置焦点
        /// </summary>
        public static void SetFocus(UnityEngine.UI.Selectable selectable) => Root?.SetFocus(selectable);

        /// <summary>
        /// 清除焦点
        /// </summary>
        public static void ClearFocus() => Root?.ClearFocus();

        /// <summary>
        /// 获取当前焦点
        /// </summary>
        public static UnityEngine.GameObject GetCurrentFocus() => Root?.CurrentFocus;

        #endregion
    }
}
