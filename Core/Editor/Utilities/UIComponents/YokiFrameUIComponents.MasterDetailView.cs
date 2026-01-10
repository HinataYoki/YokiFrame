#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - Master-Detail 布局视图
    /// 提供左侧列表 + 右侧详情的标准布局模式
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region MasterDetailView

        /// <summary>
        /// Master-Detail 视图配置
        /// </summary>
        public class MasterDetailViewConfig
        {
            /// <summary>
            /// 左侧面板固定宽度（默认 280）
            /// </summary>
            public float LeftPanelWidth { get; set; } = 280f;

            /// <summary>
            /// 列表项固定高度（用于虚拟化）
            /// </summary>
            public float ItemHeight { get; set; } = 48f;

            /// <summary>
            /// 左侧面板标题
            /// </summary>
            public string LeftPanelTitle { get; set; } = "列表";

            /// <summary>
            /// 是否显示左侧面板标题
            /// </summary>
            public bool ShowLeftPanelHeader { get; set; } = true;
        }

        /// <summary>
        /// Master-Detail 视图结果
        /// </summary>
        /// <typeparam name="T">列表项数据类型</typeparam>
        public class MasterDetailViewResult<T>
        {
            /// <summary>
            /// 根容器元素
            /// </summary>
            public VisualElement Root { get; set; }

            /// <summary>
            /// 分割视图
            /// </summary>
            public TwoPaneSplitView SplitView { get; set; }

            /// <summary>
            /// 左侧面板容器
            /// </summary>
            public VisualElement LeftPanel { get; set; }

            /// <summary>
            /// 左侧列表视图
            /// </summary>
            public ListView ListView { get; set; }

            /// <summary>
            /// 右侧详情面板容器
            /// </summary>
            public VisualElement DetailPanel { get; set; }

            /// <summary>
            /// 刷新列表数据
            /// </summary>
            /// <param name="items">数据源</param>
            public void RefreshList(IList<T> items)
            {
                ListView.itemsSource = items as System.Collections.IList;
                ListView.RefreshItems();
            }

            /// <summary>
            /// 清除选择
            /// </summary>
            public void ClearSelection()
            {
                ListView.ClearSelection();
            }

            /// <summary>
            /// 设置选中项
            /// </summary>
            /// <param name="index">索引</param>
            public void SetSelection(int index)
            {
                ListView.SetSelection(index);
            }
        }

        /// <summary>
        /// 创建 Master-Detail 布局视图
        /// 左侧为可虚拟化的 ListView，右侧为详情面板容器
        /// </summary>
        /// <typeparam name="T">列表项数据类型</typeparam>
        /// <param name="makeItem">创建列表项模板的回调</param>
        /// <param name="bindItem">绑定列表项数据的回调</param>
        /// <param name="onSelectionChanged">选择变化回调，参数为选中的数据项（可能为 null）</param>
        /// <param name="config">视图配置（可选）</param>
        /// <returns>Master-Detail 视图结果</returns>
        /// <example>
        /// <code>
        /// var result = YokiFrameUIComponents.CreateMasterDetailView&lt;PoolDebugInfo&gt;(
        ///     makeItem: () => new Label(),
        ///     bindItem: (element, index, item) => ((Label)element).text = item.Name,
        ///     onSelectionChanged: item => UpdateDetailPanel(item),
        ///     config: new MasterDetailViewConfig { LeftPanelTitle = "对象池列表" }
        /// );
        /// root.Add(result.Root);
        /// result.RefreshList(poolList);
        /// </code>
        /// </example>
        public static MasterDetailViewResult<T> CreateMasterDetailView<T>(
            Func<VisualElement> makeItem,
            Action<VisualElement, int, T> bindItem,
            Action<T> onSelectionChanged,
            MasterDetailViewConfig config = null)
        {
            config ??= new MasterDetailViewConfig();

            var result = new MasterDetailViewResult<T>();

            // 根容器
            result.Root = new VisualElement();
            result.Root.AddToClassList("yoki-master-detail");
            result.Root.style.flexGrow = 1;
            result.Root.style.flexDirection = FlexDirection.Row;

            // 分割视图
            result.SplitView = new TwoPaneSplitView(
                0,
                config.LeftPanelWidth,
                TwoPaneSplitViewOrientation.Horizontal);
            result.SplitView.style.flexGrow = 1;
            result.Root.Add(result.SplitView);

            // 左侧面板
            result.LeftPanel = new VisualElement();
            result.LeftPanel.AddToClassList("yoki-master-detail__left");
            result.LeftPanel.style.minWidth = 200;
            result.SplitView.Add(result.LeftPanel);

            // 左侧标题
            if (config.ShowLeftPanelHeader)
            {
                var header = CreateSectionHeader(config.LeftPanelTitle);
                result.LeftPanel.Add(header);
            }

            // 列表视图
            result.ListView = new ListView
            {
                fixedItemHeight = config.ItemHeight,
                makeItem = makeItem,
                bindItem = (element, index) =>
                {
                    if (result.ListView.itemsSource is IList<T> list && index < list.Count)
                    {
                        bindItem(element, index, list[index]);
                    }
                }
            };
            result.ListView.AddToClassList("yoki-master-detail__list");
            result.ListView.style.flexGrow = 1;

            // 选择变化事件
#if UNITY_2022_1_OR_NEWER
            result.ListView.selectionChanged += selection =>
            {
                foreach (var item in selection)
                {
                    if (item is T typedItem)
                    {
                        onSelectionChanged?.Invoke(typedItem);
                        return;
                    }
                }
                onSelectionChanged?.Invoke(default);
            };
#else
            result.ListView.onSelectionChange += selection =>
            {
                foreach (var item in selection)
                {
                    if (item is T typedItem)
                    {
                        onSelectionChanged?.Invoke(typedItem);
                        return;
                    }
                }
                onSelectionChanged?.Invoke(default);
            };
#endif
            result.LeftPanel.Add(result.ListView);

            // 右侧详情面板
            result.DetailPanel = new VisualElement();
            result.DetailPanel.AddToClassList("yoki-master-detail__detail");
            result.DetailPanel.style.flexGrow = 1;
            result.DetailPanel.style.minWidth = 300;
            result.SplitView.Add(result.DetailPanel);

            return result;
        }

        /// <summary>
        /// 创建带工具栏的 Master-Detail 布局视图
        /// </summary>
        /// <typeparam name="T">列表项数据类型</typeparam>
        /// <param name="makeItem">创建列表项模板的回调</param>
        /// <param name="bindItem">绑定列表项数据的回调</param>
        /// <param name="onSelectionChanged">选择变化回调</param>
        /// <param name="toolbarBuilder">工具栏构建回调（传入工具栏容器）</param>
        /// <param name="config">视图配置（可选）</param>
        /// <returns>Master-Detail 视图结果</returns>
        public static MasterDetailViewResult<T> CreateMasterDetailViewWithToolbar<T>(
            Func<VisualElement> makeItem,
            Action<VisualElement, int, T> bindItem,
            Action<T> onSelectionChanged,
            Action<VisualElement> toolbarBuilder,
            MasterDetailViewConfig config = null)
        {
            config ??= new MasterDetailViewConfig();

            // 创建外层容器
            var outerContainer = new VisualElement();
            outerContainer.style.flexGrow = 1;
            outerContainer.style.flexDirection = FlexDirection.Column;

            // 工具栏
            var toolbar = CreateToolbar();
            toolbarBuilder?.Invoke(toolbar);
            outerContainer.Add(toolbar);

            // 内容区域
            var contentArea = new VisualElement();
            contentArea.AddToClassList("content-area");
            contentArea.style.flexGrow = 1;
            outerContainer.Add(contentArea);

            // 创建 Master-Detail 视图
            var result = CreateMasterDetailView(makeItem, bindItem, onSelectionChanged, config);
            contentArea.Add(result.Root);

            // 替换根容器为外层容器
            result.Root = outerContainer;

            return result;
        }

        #endregion
    }
}
#endif
