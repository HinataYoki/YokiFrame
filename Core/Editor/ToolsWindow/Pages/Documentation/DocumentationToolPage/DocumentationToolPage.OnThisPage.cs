#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 文档页面右侧“本页导航”区域。
    /// </summary>
    public partial class DocumentationToolPage
    {
        private HighlightIndicator mOnThisPageHighlight;

        /// <summary>
        /// 根据页面宽度调整右侧导航的显示状态。
        /// </summary>
        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (mOnThisPagePanel == null) return;

            bool shouldShow = evt.newRect.width >= ON_THIS_PAGE_MIN_WIDTH;
            mOnThisPagePanel.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;

            if (mSelectedTocItem != null && mHighlightIndicator != null)
            {
                mSelectedTocItem.schedule.Execute(() => MoveHighlightToItem(mSelectedTocItem)).ExecuteLater(50);
            }

            mOnThisPageHighlight?.RefreshDelayed(50);
        }

        /// <summary>
        /// 创建右侧“本页导航”面板。
        /// </summary>
        private VisualElement CreateOnThisPagePanel()
        {
            mOnThisPagePanel = new VisualElement();
            mOnThisPagePanel.style.minWidth = 180;
            mOnThisPagePanel.style.maxWidth = 280;
            mOnThisPagePanel.style.flexShrink = 0;
            mOnThisPagePanel.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.12f));
            mOnThisPagePanel.style.borderLeftWidth = 1;
            mOnThisPagePanel.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            mOnThisPagePanel.style.paddingTop = 24;
            mOnThisPagePanel.style.paddingLeft = 20;
            mOnThisPagePanel.style.paddingRight = 20;
            mOnThisPagePanel.style.display = DisplayStyle.None;

            var title = CreateSectionHeader("本页导航", KitIcons.TARGET);
            title.style.marginBottom = 20;
            title.style.whiteSpace = WhiteSpace.NoWrap;

            var titleLabel = title.Q<Label>();
            if (titleLabel != null)
            {
                titleLabel.style.fontSize = 12;
                titleLabel.style.color = new StyleColor(Theme.TextMuted);
                titleLabel.style.letterSpacing = 1f;
            }

            mOnThisPagePanel.Add(title);

            mOnThisPageContainer = new VisualElement();
            mOnThisPageContainer.style.position = Position.Relative;
            mOnThisPagePanel.Add(mOnThisPageContainer);

            mOnThisPageHighlight = new HighlightIndicator(
                mOnThisPageContainer,
                new Color(0.24f, 0.37f, 0.58f, 0.35f),
                4f);

            return mOnThisPagePanel;
        }

        /// <summary>
        /// 根据当前文档内容重建右侧标题导航。
        /// </summary>
        private void RefreshOnThisPage()
        {
            if (mOnThisPageContainer == null) return;

            var highlightElement = mOnThisPageHighlight?.Element;
            mOnThisPageContainer.Clear();
            if (highlightElement != null)
            {
                mOnThisPageContainer.Add(highlightElement);
            }

            mSelectedHeadingItem = null;
            mHeadingNavMap.Clear();
            mOnThisPageHighlight?.Clear();

            bool isFirst = true;
            foreach (var (headingTitle, element, level) in mCurrentHeadings)
            {
                if (string.IsNullOrEmpty(headingTitle) || element == null)
                {
                    continue;
                }

                var item = CreateOnThisPageItem(headingTitle, element, level);
                mOnThisPageContainer.Add(item);
                mHeadingNavMap.Add((item, element));

                if (isFirst)
                {
                    mSelectedHeadingItem = item;
                    mOnThisPageHighlight?.MoveToDelayed(item, 50);
                    UpdateHeadingItemStyle(item, true);
                    isFirst = false;
                }
            }
        }

        /// <summary>
        /// 根据内容滚动位置同步右侧标题高亮。
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
        /// 更新当前激活标题的高亮状态。
        /// </summary>
        private void UpdateHeadingHighlight(VisualElement newActiveItem)
        {
            for (int i = 0; i < mHeadingNavMap.Count; i++)
            {
                var (navItem, _) = mHeadingNavMap[i];
                UpdateHeadingItemStyle(navItem, navItem == newActiveItem);
            }

            mSelectedHeadingItem = newActiveItem;
            mOnThisPageHighlight?.MoveTo(newActiveItem);
        }

        /// <summary>
        /// 更新单个标题导航项的选中样式。
        /// </summary>
        private void UpdateHeadingItemStyle(VisualElement item, bool isActive)
        {
            item.style.borderLeftColor = isActive ? new StyleColor(Theme.AccentBlue) : new StyleColor(Color.clear);

            var label = item.Q<Label>();
            if (label != null)
            {
                label.style.color = isActive ? new StyleColor(Theme.TextPrimary) : new StyleColor(Theme.TextMuted);
            }
        }

        /// <summary>
        /// 创建单个右侧标题导航项。
        /// </summary>
        private VisualElement CreateOnThisPageItem(string title, VisualElement targetElement, int level)
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
            item.style.borderLeftColor = new StyleColor(Color.clear);
            item.style.transitionProperty = new List<StylePropertyName> { new("border-left-color") };
            item.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };

            var label = new Label(title);
            label.style.fontSize = level == 1 ? 14 : 13;
            label.style.color = new StyleColor(Theme.TextMuted);
            label.style.transitionProperty = new List<StylePropertyName> { new("color") };
            label.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Visible;
            item.Add(label);

            item.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (item != mSelectedHeadingItem)
                {
                    label.style.color = new StyleColor(Theme.TextSecondary);
                }
            });

            item.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (item != mSelectedHeadingItem)
                {
                    label.style.color = new StyleColor(Theme.TextMuted);
                }
            });

            item.RegisterCallback<ClickEvent>(_ =>
            {
                mIsScrollingByClick = true;
                UpdateHeadingHighlight(item);
                ScrollToElement(targetElement);
                item.schedule.Execute(() => mIsScrollingByClick = false).ExecuteLater(300);
            });

            return item;
        }

        /// <summary>
        /// 将内容区域滚动到指定元素位置。
        /// </summary>
        private void ScrollToElement(VisualElement targetElement)
        {
            if (targetElement == null || mContentScrollView == null) return;

            var contentContainer = mContentScrollView.contentContainer;
            float targetY = 0f;

            var current = targetElement;
            while (current != null && current != contentContainer)
            {
                targetY += current.layout.y;
                current = current.parent;
            }

            const float TOP_OFFSET = 20f;
            targetY = Mathf.Max(0f, targetY - TOP_OFFSET);
            mContentScrollView.scrollOffset = new Vector2(0, targetY);
        }
    }
}
#endif
