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
        /// 是否启用热度缓存机制（关闭后 Hot 模式面板关闭即销毁，不做热度保留）
        /// </summary>
        public static bool HotCacheEnabled
        {
            get
            {
                var root = Root;
                return root != default ? root.HotCacheEnabled : true;
            }
            set
            {
                var root = Root;
                if (root != default) root.HotCacheEnabled = value;
            }
        }

        /// <summary>
        /// 创建界面时赋予的热度值
        /// </summary>
        public static int OpenHot
        {
            get
            {
                var root = Root;
                return root != default ? root.OpenHot : 0;
            }
            set
            {
                var root = Root;
                if (root != default) root.OpenHot = value;
            }
        }

        /// <summary>
        /// 获取界面时赋予的热度值
        /// </summary>
        public static int GetHot
        {
            get
            {
                var root = Root;
                return root != default ? root.GetHot : 0;
            }
            set
            {
                var root = Root;
                if (root != default) root.GetHot = value;
            }
        }

        /// <summary>
        /// 每次行为造成的衰减热度值
        /// </summary>
        public static int Weaken
        {
            get
            {
                var root = Root;
                return root != default ? root.Weaken : 0;
            }
            set
            {
                var root = Root;
                if (root != default) root.Weaken = value;
            }
        }

        #endregion

        #region 焦点

        /// <summary>
        /// 焦点系统是否启用
        /// </summary>
        public static bool FocusSystemEnabled
        {
            get
            {
                var root = Root;
                return root != default ? root.FocusSystemEnabled : false;
            }
            set
            {
                var root = Root;
                if (root != default) root.FocusSystemEnabled = value;
            }
        }

        /// <summary>
        /// 当前输入模式
        /// </summary>
        public static UIInputMode GetInputMode()
        {
            var root = Root;
            return root != default ? root.CurrentInputMode : UIInputMode.Pointer;
        }

        /// <summary>
        /// 设置焦点
        /// </summary>
        public static void SetFocus(UnityEngine.GameObject target)
        {
            var root = Root;
            if (root != default) root.SetFocus(target);
        }

        /// <summary>
        /// 设置焦点
        /// </summary>
        public static void SetFocus(UnityEngine.UI.Selectable selectable)
        {
            var root = Root;
            if (root != default) root.SetFocus(selectable);
        }

        /// <summary>
        /// 清除焦点
        /// </summary>
        public static void ClearFocus()
        {
            var root = Root;
            if (root != default) root.ClearFocus();
        }

        /// <summary>
        /// 获取当前焦点
        /// </summary>
        public static UnityEngine.GameObject GetCurrentFocus()
        {
            var root = Root;
            return root != default ? root.CurrentFocus : null;
        }

        #endregion
    }
}
