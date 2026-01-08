#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 文档页面 - 右侧本页导航面板
    /// </summary>
    public partial class DocumentationToolPage
    {
        /// <summary>
        /// 响应式布局：根据窗口宽度显示/隐藏右侧导航
        /// </summary>
        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (mOnThisPagePanel == null) return;
            
            bool shouldShow = evt.newRect.width >= ON_THIS_PAGE_MIN_WIDTH;
            mOnThisPagePanel.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        /// <summary>
        /// 创建右侧"本页导航"面板
        /// </summary>
        private VisualElement CreateOnThisPagePanel()
        {
            mOnThisPagePanel = new VisualElement();
            mOnThisPagePanel.style.width = 200;
            mOnThisPagePanel.style.minWidth = 180;
            mOnThisPagePanel.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.12f));
            mOnThisPagePanel.style.borderLeftWidth = 1;
            mOnThisPagePanel.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            mOnThisPagePanel.style.paddingTop = 24;
            mOnThisPagePanel.style.paddingLeft = 20;
            mOnThisPagePanel.style.paddingRight = 16;
            mOnThisPagePanel.style.display = DisplayStyle.None;
            
            // 标题
            var title = new Label("本页目录");
            title.style.fontSize = 12;
            title.style.color = new StyleColor(Theme.TextMuted);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 1f;
            title.style.marginBottom = 20;
            mOnThisPagePanel.Add(title);
            
            // 导航项容器
            mOnThisPageContainer = new VisualElement();
            mOnThisPagePanel.Add(mOnThisPageContainer);
            
            return mOnThisPagePanel;
        }
        
        /// <summary>
        /// 刷新右侧本页导航
        /// </summary>
        private void RefreshOnThisPage()
        {
            if (mOnThisPageContainer == null) return;
            
            mOnThisPageContainer.Clear();
            mSelectedHeadingItem = null;
            mHeadingNavMap.Clear();
            
            bool isFirst = true;
            foreach (var (headingTitle, element, level) in mCurrentHeadings)
            {
                var item = CreateOnThisPageItem(headingTitle, element, level, isFirst);
                mOnThisPageContainer.Add(item);
                
                mHeadingNavMap.Add((item, element));
                
                if (isFirst)
                {
                    mSelectedHeadingItem = item;
                    isFirst = false;
                }
            }
        }
        
        /// <summary>
        /// 内容滚动时同步更新右侧导航高亮
        /// </summary>
        private void OnContentScrollChanged(float scrollValue)
        {
            if (mIsScrollingByClick || mHeadingNavMap.Count == 0) return;
            
            var scrollViewRect = mContentScrollView.contentContainer.worldBound;
            float viewportTop = scrollViewRect.y + scrollValue;
            float threshold = 80f;
            
            VisualElement activeNavItem = null;
            
            for (int i = mHeadingNavMap.Count - 1; i >= 0; i--)
            {
                var (navItem, contentElement) = mHeadingNavMap[i];
                var elementRect = contentElement.worldBound;
                
                if (elementRect.y <= viewportTop + threshold)
                {
                    activeNavItem = navItem;
                    break;
                }
            }
            
            if (activeNavItem == null && mHeadingNavMap.Count > 0)
            {
                activeNavItem = mHeadingNavMap[0].navItem;
            }
            
            if (activeNavItem != null && activeNavItem != mSelectedHeadingItem)
            {
                UpdateHeadingHighlight(activeNavItem);
            }
        }
        
        /// <summary>
        /// 更新右侧导航的高亮状态
        /// </summary>
        private void UpdateHeadingHighlight(VisualElement newActiveItem)
        {
            if (mSelectedHeadingItem != null)
            {
                mSelectedHeadingItem.style.borderLeftColor = new StyleColor(Color.clear);
                mSelectedHeadingItem.style.backgroundColor = new StyleColor(Color.clear);
                var prevLabel = mSelectedHeadingItem.Q<Label>();
                if (prevLabel != null) prevLabel.style.color = new StyleColor(Theme.TextMuted);
            }
            
            mSelectedHeadingItem = newActiveItem;
            newActiveItem.style.borderLeftColor = new StyleColor(Theme.AccentBlue);
            newActiveItem.style.backgroundColor = new StyleColor(new Color(0.24f, 0.37f, 0.58f, 0.35f));
            var newLabel = newActiveItem.Q<Label>();
            if (newLabel != null) newLabel.style.color = new StyleColor(Theme.TextPrimary);
        }
        
        /// <summary>
        /// 创建本页导航项
        /// </summary>
        private VisualElement CreateOnThisPageItem(string title, VisualElement targetElement, int level, bool isActive = false)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingTop = 8;
            item.style.paddingBottom = 8;
            item.style.paddingLeft = level == 2 ? 14 : 6;
            item.style.paddingRight = 6;
            item.style.marginTop = 2;
            item.style.marginBottom = 2;
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;
            item.style.borderLeftWidth = 2;
            item.style.borderLeftColor = isActive ? new StyleColor(Theme.AccentBlue) : new StyleColor(Color.clear);
            item.style.backgroundColor = isActive ? new StyleColor(new Color(0.24f, 0.37f, 0.58f, 0.35f)) : new StyleColor(Color.clear);
            item.style.transitionProperty = new List<StylePropertyName> { new("border-left-color"), new("background-color") };
            item.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond), new(150, TimeUnit.Millisecond) };
            
            var label = new Label(title);
            label.style.fontSize = level == 1 ? 14 : 13;
            label.style.color = isActive ? new StyleColor(Theme.TextPrimary) : new StyleColor(Theme.TextMuted);
            label.style.transitionProperty = new List<StylePropertyName> { new("color") };
            label.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            item.Add(label);
            
            // 悬停效果
            item.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (item != mSelectedHeadingItem)
                {
                    label.style.color = new StyleColor(Theme.TextSecondary);
                    item.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.25f, 0.6f));
                }
            });
            item.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (item != mSelectedHeadingItem)
                {
                    label.style.color = new StyleColor(Theme.TextMuted);
                    item.style.backgroundColor = new StyleColor(Color.clear);
                }
            });
            
            // 点击滚动到对应位置
            item.RegisterCallback<ClickEvent>(evt =>
            {
                mIsScrollingByClick = true;
                UpdateHeadingHighlight(item);
                mContentScrollView.ScrollTo(targetElement);
                item.schedule.Execute(() => mIsScrollingByClick = false).ExecuteLater(300);
            });
            
            return item;
        }
    }
}
#endif
