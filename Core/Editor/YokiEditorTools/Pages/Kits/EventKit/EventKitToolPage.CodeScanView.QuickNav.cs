#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 代码扫描视图 - 右侧快速导航面板
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 快速导航私有字段

        private VisualElement mQuickNavPanel;
        private VisualElement mQuickNavContainer;
        private readonly List<(VisualElement navItem, VisualElement targetElement)> mNavItemMap = new(32);
        private VisualElement mSelectedNavItem;

        // 响应式布局阈值
        private const float QUICK_NAV_MIN_WIDTH = 900f;

        #endregion

        #region 创建快速导航面板

        /// <summary>
        /// 创建右侧快速导航面板
        /// </summary>
        private VisualElement CreateQuickNavPanel()
        {
            mQuickNavPanel = new VisualElement();
            // 使用 2:1 比例（约 33%）
            mQuickNavPanel.style.flexGrow = 1;
            mQuickNavPanel.style.flexBasis = 0;
            mQuickNavPanel.style.minWidth = 300;
            mQuickNavPanel.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.12f));
            mQuickNavPanel.style.borderLeftWidth = 1;
            mQuickNavPanel.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            mQuickNavPanel.style.paddingTop = 16;
            mQuickNavPanel.style.paddingLeft = 12;
            mQuickNavPanel.style.paddingRight = 12;
            mQuickNavPanel.style.display = DisplayStyle.None;

            // 标题
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 16;

            var titleIcon = new Image { image = EditorTools.KitIcons.GetTexture(EditorTools.KitIcons.TARGET) };
            titleIcon.style.width = 12;
            titleIcon.style.height = 12;
            titleIcon.style.marginRight = 6;
            titleRow.Add(titleIcon);

            var title = new Label("快速导航");
            title.style.fontSize = 12;
            title.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 1f;
            titleRow.Add(title);

            mQuickNavPanel.Add(titleRow);

            // 导航项容器（可滚动）
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            mQuickNavPanel.Add(scrollView);

            mQuickNavContainer = new VisualElement();
            scrollView.Add(mQuickNavContainer);

            return mQuickNavPanel;
        }

        /// <summary>
        /// 刷新快速导航内容
        /// </summary>
        private void RefreshQuickNav(Dictionary<string, Dictionary<string, EventFlowData>> flows)
        {
            if (mQuickNavContainer == null) return;

            mQuickNavContainer.Clear();
            mNavItemMap.Clear();
            mSelectedNavItem = null;

            var typeOrder = new[] { "Enum", "Type", "String" };
            bool isFirst = true;

            foreach (var eventType in typeOrder)
            {
                if (!flows.TryGetValue(eventType, out var typeDict) || typeDict.Count == 0)
                    continue;

                // 类型分组标题
                var groupHeader = CreateNavGroupHeader(eventType, typeDict.Count);
                mQuickNavContainer.Add(groupHeader);

                // 事件项
                foreach (var kvp in typeDict)
                {
                    var navItem = CreateNavItem(kvp.Key, kvp.Value, isFirst);
                    mQuickNavContainer.Add(navItem);

                    if (isFirst)
                    {
                        mSelectedNavItem = navItem;
                        isFirst = false;
                    }
                }
            }
        }

        /// <summary>
        /// 创建导航分组标题
        /// </summary>
        private VisualElement CreateNavGroupHeader(string eventType, int count)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginTop = 12;
            header.style.marginBottom = 6;
            header.style.paddingLeft = 4;

            var (_, borderColor, textColor) = GetEventTypeColors(eventType);

            // 颜色点
            var dot = new VisualElement();
            dot.style.width = 8;
            dot.style.height = 8;
            dot.style.borderTopLeftRadius = 4;
            dot.style.borderTopRightRadius = 4;
            dot.style.borderBottomLeftRadius = 4;
            dot.style.borderBottomRightRadius = 4;
            dot.style.backgroundColor = new StyleColor(borderColor);
            dot.style.marginRight = 6;
            header.Add(dot);

            var label = new Label($"{eventType} ({count})");
            label.style.fontSize = 11;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new StyleColor(textColor);
            header.Add(label);

            return header;
        }

        /// <summary>
        /// 创建导航项
        /// </summary>
        private VisualElement CreateNavItem(string eventKey, EventFlowData flow, bool isActive)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingTop = 6;
            item.style.paddingBottom = 6;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 6;
            item.style.marginTop = 1;
            item.style.marginBottom = 1;
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;
            item.style.borderLeftWidth = 2;
            item.style.borderLeftColor = isActive 
                ? new StyleColor(new Color(0.34f, 0.61f, 0.84f)) 
                : new StyleColor(Color.clear);
            item.style.backgroundColor = isActive 
                ? new StyleColor(new Color(0.24f, 0.37f, 0.58f, 0.35f)) 
                : new StyleColor(Color.clear);

            // 健康状态图标
            var health = flow.GetHealthStatus();
            var statusIcon = new Image { image = EditorTools.KitIcons.GetTexture(GetHealthIcon(health)) };
            statusIcon.style.width = 10;
            statusIcon.style.height = 10;
            statusIcon.style.marginRight = 6;
            statusIcon.tintColor = GetHealthColor(health);
            item.Add(statusIcon);

            // 事件名称（不再截断，500px 宽度足够显示完整名称）
            var label = new Label(eventKey);
            label.style.fontSize = 11;
            label.style.color = isActive 
                ? new StyleColor(new Color(0.95f, 0.95f, 0.95f)) 
                : new StyleColor(new Color(0.6f, 0.6f, 0.65f));
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.tooltip = eventKey;
            item.Add(label);

            // 悬停效果
            item.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (item != mSelectedNavItem)
                {
                    label.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.85f));
                    item.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.25f, 0.6f));
                }
            });
            item.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (item != mSelectedNavItem)
                {
                    label.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
                    item.style.backgroundColor = new StyleColor(Color.clear);
                }
            });

            // 点击滚动到对应位置
            item.RegisterCallback<ClickEvent>(_ =>
            {
                // 查找对应的内容元素
                var targetElement = FindEventFlowElement(eventKey);
                if (targetElement != null)
                {
                    UpdateNavHighlight(item);
                    mScanResultsScrollView.ScrollTo(targetElement);
                }
            });

            // 存储映射（稍后在渲染时关联）
            item.userData = eventKey;

            return item;
        }

        /// <summary>
        /// 更新导航高亮状态
        /// </summary>
        private void UpdateNavHighlight(VisualElement newActiveItem)
        {
            if (mSelectedNavItem != null)
            {
                mSelectedNavItem.style.borderLeftColor = new StyleColor(Color.clear);
                mSelectedNavItem.style.backgroundColor = new StyleColor(Color.clear);
                var prevLabel = mSelectedNavItem.Q<Label>();
                if (prevLabel != null) 
                    prevLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
            }

            mSelectedNavItem = newActiveItem;
            newActiveItem.style.borderLeftColor = new StyleColor(new Color(0.34f, 0.61f, 0.84f));
            newActiveItem.style.backgroundColor = new StyleColor(new Color(0.24f, 0.37f, 0.58f, 0.35f));
            var newLabel = newActiveItem.Q<Label>();
            if (newLabel != null) 
                newLabel.style.color = new StyleColor(new Color(0.95f, 0.95f, 0.95f));
        }

        /// <summary>
        /// 查找事件流元素（通过 eventKey）
        /// </summary>
        private VisualElement FindEventFlowElement(string eventKey)
        {
            foreach (var (navItem, targetElement) in mNavItemMap)
            {
                if (navItem.userData as string == eventKey)
                    return targetElement;
            }
            return null;
        }

        /// <summary>
        /// 注册导航项与内容元素的映射
        /// </summary>
        private void RegisterNavMapping(string eventKey, VisualElement contentElement)
        {
            // 查找对应的导航项
            foreach (var child in mQuickNavContainer.Children())
            {
                if (child.userData as string == eventKey)
                {
                    mNavItemMap.Add((child, contentElement));
                    break;
                }
            }
        }

        /// <summary>
        /// 响应式布局：根据窗口宽度显示/隐藏快速导航
        /// </summary>
        private void OnCodeScanViewGeometryChanged(GeometryChangedEvent evt)
        {
            if (mQuickNavPanel == null) return;

            bool shouldShow = evt.newRect.width >= QUICK_NAV_MIN_WIDTH;
            mQuickNavPanel.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion

        #region 辅助方法

        private static string GetHealthIcon(HealthStatus health) => health switch
        {
            HealthStatus.Healthy => EditorTools.KitIcons.SUCCESS,
            HealthStatus.Orphan => EditorTools.KitIcons.WARNING,
            HealthStatus.LeakRisk => EditorTools.KitIcons.ERROR,
            HealthStatus.NoSender => EditorTools.KitIcons.WARNING,
            _ => EditorTools.KitIcons.INFO
        };

        private static Color GetHealthColor(HealthStatus health) => health switch
        {
            HealthStatus.Healthy => new Color(0.4f, 0.9f, 0.5f),
            HealthStatus.Orphan => new Color(1f, 0.8f, 0.3f),
            HealthStatus.LeakRisk => new Color(1f, 0.4f, 0.4f),
            HealthStatus.NoSender => new Color(0.9f, 0.9f, 0.4f),
            _ => new Color(0.6f, 0.6f, 0.6f)
        };

        #endregion
    }
}
#endif
