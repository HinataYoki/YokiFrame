#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 文档页面 - 左侧目录面板
    /// </summary>
    public partial class DocumentationToolPage
    {
        private VisualElement CreateTocPanel()
        {
            var panel = new VisualElement();
            panel.style.width = 260;
            panel.style.minWidth = 240;
            panel.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.11f));
            panel.style.borderRightWidth = 1;
            panel.style.borderRightColor = new StyleColor(new Color(0.18f, 0.18f, 0.22f, 0.6f));
            
            mTocScrollView = new ScrollView();
            mTocScrollView.style.flexGrow = 1;
            mTocScrollView.style.paddingTop = 16;
            mTocScrollView.style.paddingBottom = 16;
            panel.Add(mTocScrollView);
            
            // 创建高亮指示器（独立元素，用于平滑移动动画）
            mHighlightIndicator = new VisualElement();
            mHighlightIndicator.style.position = Position.Absolute;
            mHighlightIndicator.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.25f));
            mHighlightIndicator.style.borderTopLeftRadius = 6;
            mHighlightIndicator.style.borderTopRightRadius = 6;
            mHighlightIndicator.style.borderBottomLeftRadius = 6;
            mHighlightIndicator.style.borderBottomRightRadius = 6;
            mHighlightIndicator.style.opacity = 0;
            mHighlightIndicator.pickingMode = PickingMode.Ignore;
            // 添加过渡动画
            mHighlightIndicator.style.transitionProperty = new List<StylePropertyName> 
            { 
                new("top"), 
                new("left"), 
                new("width"), 
                new("height"),
                new("opacity")
            };
            mHighlightIndicator.style.transitionDuration = new List<TimeValue> 
            { 
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(150, TimeUnit.Millisecond)
            };
            mHighlightIndicator.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut)
            };
            
            RefreshToc();
            return panel;
        }
        
        private void RefreshToc()
        {
            mTocScrollView.Clear();
            mTocItemMap.Clear();
            
            // 创建内容容器（用于放置高亮指示器）
            mTocItemsContainer = new VisualElement();
            mTocItemsContainer.style.position = Position.Relative;
            mTocScrollView.Add(mTocItemsContainer);
            
            // 添加高亮指示器到容器
            mTocItemsContainer.Add(mHighlightIndicator);
            
            string currentCategory = null;
            VisualElement categoryGroup = null;
            
            for (int i = 0; i < mModules.Count; i++)
            {
                var module = mModules[i];
                var moduleIndex = i;
                
                if (module.Category != currentCategory)
                {
                    currentCategory = module.Category;
                    
                    // 创建分类组容器
                    categoryGroup = new VisualElement();
                    categoryGroup.style.marginTop = i == 0 ? 0 : 16;
                    categoryGroup.style.marginLeft = 8;
                    categoryGroup.style.marginRight = 8;
                    categoryGroup.style.marginBottom = 4;
                    
                    var categoryColor = GetCategoryColor(currentCategory);
                    
                    // 分类标题栏
                    var categoryHeader = new VisualElement();
                    categoryHeader.style.flexDirection = FlexDirection.Row;
                    categoryHeader.style.alignItems = Align.Center;
                    categoryHeader.style.paddingLeft = 8;
                    categoryHeader.style.paddingRight = 8;
                    categoryHeader.style.paddingTop = 8;
                    categoryHeader.style.paddingBottom = 8;
                    
                    // 分类图标
                    var categoryIcon = new Image { image = KitIcons.GetTexture(GetCategoryIcon(currentCategory)) };
                    categoryIcon.style.width = 12;
                    categoryIcon.style.height = 12;
                    categoryIcon.style.marginRight = 6;
                    categoryHeader.Add(categoryIcon);
                    
                    // 分类标签
                    var categoryLabel = new Label(currentCategory);
                    categoryLabel.style.fontSize = 11;
                    categoryLabel.style.color = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.8f));
                    categoryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    categoryLabel.style.flexGrow = 1;
                    categoryLabel.style.letterSpacing = 1f;
                    categoryHeader.Add(categoryLabel);
                    
                    // 分类徽章（显示数量）
                    var countBadge = new Label(GetCategoryModuleCount(currentCategory).ToString());
                    countBadge.style.fontSize = 10;
                    countBadge.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
                    countBadge.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
                    countBadge.style.paddingLeft = 6;
                    countBadge.style.paddingRight = 6;
                    countBadge.style.paddingTop = 2;
                    countBadge.style.paddingBottom = 2;
                    countBadge.style.borderTopLeftRadius = 8;
                    countBadge.style.borderTopRightRadius = 8;
                    countBadge.style.borderBottomLeftRadius = 8;
                    countBadge.style.borderBottomRightRadius = 8;
                    categoryHeader.Add(countBadge);
                    
                    categoryGroup.Add(categoryHeader);
                    mTocItemsContainer.Add(categoryGroup);
                }
                
                var item = CreateTocItem(module, moduleIndex);
                categoryGroup.Add(item);
            }
        }
        
        private VisualElement CreateTocItem(DocModule module, int index)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = 10;
            item.style.paddingRight = 8;
            item.style.paddingTop = 8;
            item.style.paddingBottom = 8;
            item.style.marginLeft = 4;
            item.style.marginRight = 4;
            item.style.marginTop = 1;
            item.style.marginBottom = 1;
            item.style.borderTopLeftRadius = 6;
            item.style.borderTopRightRadius = 6;
            item.style.borderBottomLeftRadius = 6;
            item.style.borderBottomRightRadius = 6;
            item.style.borderLeftWidth = 3;
            item.style.borderLeftColor = new StyleColor(Color.clear);
            
            // 添加过渡动画
            item.style.transitionProperty = new List<StylePropertyName>
            {
                new("background-color"),
                new("border-left-color")
            };
            item.style.transitionDuration = new List<TimeValue>
            {
                new(150, TimeUnit.Millisecond),
                new(150, TimeUnit.Millisecond)
            };
            item.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut)
            };
            
            // 图标
            var icon = new Image { image = KitIcons.GetTexture(module.Icon) };
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 8;
            icon.style.transitionProperty = new List<StylePropertyName> { new("scale") };
            icon.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            item.Add(icon);
            
            var label = new Label(module.Name);
            label.style.fontSize = 13;
            label.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.75f));
            label.style.flexGrow = 1;
            label.style.transitionProperty = new List<StylePropertyName> { new("color") };
            label.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            item.Add(label);
            
            // 箭头指示器
            var arrow = new Label("›");
            arrow.style.fontSize = 15;
            arrow.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.45f));
            arrow.name = "arrow";
            arrow.style.transitionProperty = new List<StylePropertyName> { new("color") };
            arrow.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            item.Add(arrow);
            
            mTocItemMap[item] = index;
            
            item.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (item != mSelectedTocItem)
                {
                    item.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
                    
                    var arrowLabel = item.Q<Label>("arrow");
                    if (arrowLabel != null) arrowLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
                    
                    var iconImage = item.ElementAt(0) as Image;
                    if (iconImage != null) iconImage.style.scale = new Scale(new Vector3(1.1f, 1.1f, 1f));
                }
            });
            item.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (item != mSelectedTocItem)
                {
                    item.style.backgroundColor = StyleKeyword.Null;
                    item.style.borderLeftColor = new StyleColor(Color.clear);
                    
                    var arrowLabel = item.Q<Label>("arrow");
                    if (arrowLabel != null) arrowLabel.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.45f));
                    
                    var textLabel = item.ElementAt(1) as Label;
                    if (textLabel != null) textLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.75f));
                    
                    var iconImage = item.ElementAt(0) as Image;
                    if (iconImage != null) iconImage.style.scale = new Scale(Vector3.one);
                }
            });
            item.RegisterCallback<ClickEvent>(evt => SelectModule(index));
            
            return item;
        }
        
        private void SelectModule(int index)
        {
            if (index < 0 || index >= mModules.Count) return;
            
            foreach (var kvp in mTocItemMap)
            {
                var item = kvp.Key;
                var arrow = item.Q<Label>("arrow");
                var textLabel = item.ElementAt(1) as Label;
                
                if (kvp.Value == index)
                {
                    mSelectedTocItem = item;
                    item.schedule.Execute(() => MoveHighlightToItem(item)).ExecuteLater(1);
                    
                    item.style.borderLeftColor = new StyleColor(Theme.AccentBlue);
                    if (arrow != null) arrow.style.color = new StyleColor(Theme.AccentBlue);
                    if (textLabel != null) 
                    {
                        textLabel.style.color = new StyleColor(Theme.AccentBlue);
                        textLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    }
                }
                else
                {
                    item.style.backgroundColor = StyleKeyword.Null;
                    item.style.borderLeftColor = new StyleColor(Color.clear);
                    
                    if (arrow != null) arrow.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.45f));
                    if (textLabel != null) 
                    {
                        textLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.75f));
                        textLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                    }
                }
            }
            
            mContentScrollView.scrollOffset = Vector2.zero;
            RenderContent(mModules[index]);
        }
        
        /// <summary>
        /// 将高亮指示器平滑移动到目标项
        /// </summary>
        private void MoveHighlightToItem(VisualElement targetItem)
        {
            if (targetItem == null || mHighlightIndicator == null || mTocItemsContainer == null) return;
            
            var targetRect = targetItem.worldBound;
            var containerRect = mTocItemsContainer.worldBound;
            
            float relativeTop = targetRect.y - containerRect.y + mTocScrollView.scrollOffset.y;
            float relativeLeft = targetRect.x - containerRect.x;
            
            mHighlightIndicator.style.top = relativeTop;
            mHighlightIndicator.style.left = relativeLeft;
            mHighlightIndicator.style.width = targetRect.width;
            mHighlightIndicator.style.height = targetRect.height;
            mHighlightIndicator.style.opacity = 1;
        }
        
        private Color GetCategoryColor(string category) => category switch
        {
            "CORE" => Theme.CategoryCore,
            "CORE KIT" => Theme.CategoryKit,
            "TOOLS" => Theme.CategoryTools,
            _ => Theme.AccentBlue
        };
        
        private Color GetCategoryBgColor(string category) => category switch
        {
            "CORE" => Theme.CategoryCoreBg,
            "CORE KIT" => Theme.CategoryKitBg,
            "TOOLS" => Theme.CategoryToolsBg,
            _ => Theme.BgTertiary
        };
        
        private string GetCategoryIcon(string category) => category switch
        {
            "CORE" => KitIcons.CATEGORY_CORE,
            "CORE KIT" => KitIcons.CATEGORY_COREKIT,
            "TOOLS" => KitIcons.CATEGORY_TOOLS,
            _ => KitIcons.CATEGORY_CORE
        };
        
        private int GetCategoryModuleCount(string category)
        {
            int count = 0;
            foreach (var module in mModules)
            {
                if (module.Category == category) count++;
            }
            return count;
        }
    }
}
#endif
