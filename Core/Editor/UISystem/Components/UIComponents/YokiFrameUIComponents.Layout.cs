#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 布局容器
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 基础行容器

        /// <summary>
        /// 创建水平行容器
        /// </summary>
        public static VisualElement CreateRow(Align alignItems = Align.Center, Justify justifyContent = Justify.FlexStart)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = alignItems;
            row.style.justifyContent = justifyContent;
            return row;
        }

        /// <summary>
        /// 创建弹性空间（用于推开元素）
        /// </summary>
        public static VisualElement CreateFlexSpacer()
        {
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            return spacer;
        }

        /// <summary>
        /// 创建列表项行容器
        /// </summary>
        public static VisualElement CreateListItemRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = Spacing.XS;
            row.style.paddingBottom = Spacing.XS;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            return row;
        }

        /// <summary>
        /// 创建按钮行容器（右对齐）
        /// </summary>
        public static VisualElement CreateButtonRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = Spacing.SM;
            return row;
        }

        #endregion

        #region 工具栏与过滤栏

        /// <summary>
        /// 创建工具栏容器
        /// </summary>
        public static VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.paddingLeft = Spacing.SM;
            toolbar.style.paddingRight = Spacing.SM;
            toolbar.style.paddingTop = Spacing.XS;
            toolbar.style.paddingBottom = Spacing.XS;
            toolbar.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            toolbar.AddToClassList("toolbar");
            return toolbar;
        }

        /// <summary>
        /// 创建过滤栏容器（比工具栏稍暗）
        /// </summary>
        public static VisualElement CreateFilterBar()
        {
            var filterBar = new VisualElement();
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.paddingLeft = Spacing.SM;
            filterBar.style.paddingRight = Spacing.SM;
            filterBar.style.paddingTop = Spacing.XS;
            filterBar.style.paddingBottom = Spacing.XS;
            filterBar.style.backgroundColor = new StyleColor(Colors.LayerFilterBar);
            filterBar.style.borderBottomWidth = 1;
            filterBar.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            return filterBar;
        }

        /// <summary>
        /// 创建标签栏容器
        /// </summary>
        public static VisualElement CreateTabBar()
        {
            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.borderBottomWidth = 1;
            tabBar.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            tabBar.style.backgroundColor = new StyleColor(Colors.LayerTabBar);
            return tabBar;
        }

        /// <summary>
        /// 创建统计信息行容器
        /// </summary>
        public static VisualElement CreateStatsRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = Spacing.SM;
            row.style.paddingBottom = Spacing.SM;
            row.style.paddingLeft = Spacing.SM;
            row.style.paddingRight = Spacing.SM;
            row.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
            row.style.borderTopLeftRadius = Radius.MD;
            row.style.borderTopRightRadius = Radius.MD;
            row.style.borderBottomLeftRadius = Radius.MD;
            row.style.borderBottomRightRadius = Radius.MD;
            row.style.marginBottom = Spacing.MD;
            return row;
        }

        #endregion

        #region 分隔线

        /// <summary>
        /// 创建分隔线
        /// </summary>
        public static VisualElement CreateDivider()
        {
            var divider = new VisualElement();
            divider.AddToClassList("divider");
            return divider;
        }

        /// <summary>
        /// 创建垂直分隔线
        /// </summary>
        public static VisualElement CreateVerticalDivider()
        {
            var divider = new VisualElement();
            divider.AddToClassList("divider-vertical");
            return divider;
        }

        #endregion
    }
}
#endif
