#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 分类面板
    /// </summary>
    public partial class ResKitToolPage
    {
        private void CreateOrUpdateCategoryPanel(string typeName, List<ResDebugger.ResInfo> assets)
        {
            if (!mCategoryPanels.TryGetValue(typeName, out var panel))
            {
                panel = CreateCategoryPanel(typeName);
                mCategoryPanels[typeName] = panel;
                mCategoryContainer.Add(panel.Root);
            }

            panel.CountLabel.text = $"{assets.Count}";
            
            panel.ItemsContainer.Clear();
            foreach (var asset in assets)
            {
                var item = CreateAssetItem(asset);
                panel.ItemsContainer.Add(item);
            }
        }

        private CategoryPanel CreateCategoryPanel(string typeName)
        {
            var accentColor = GetTypeColor(typeName);
            var icon = GetTypeIcon(typeName);

            var root = new VisualElement();
            root.style.marginBottom = 8;
            root.style.backgroundColor = new StyleColor(new Color(0.16f, 0.16f, 0.18f));
            root.style.borderTopLeftRadius = root.style.borderTopRightRadius = 6;
            root.style.borderBottomLeftRadius = root.style.borderBottomRightRadius = 6;
            root.style.borderLeftWidth = 4;
            root.style.borderLeftColor = new StyleColor(accentColor);

            // 头部
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = 40;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            root.Add(header);

            // 展开按钮
            var expandBtn = new Button(() => ToggleCategoryExpand(typeName));
            expandBtn.style.width = 24;
            expandBtn.style.height = 24;
            expandBtn.style.backgroundColor = StyleKeyword.Null;
            expandBtn.style.borderLeftWidth = expandBtn.style.borderRightWidth = 0;
            expandBtn.style.borderTopWidth = expandBtn.style.borderBottomWidth = 0;
            expandBtn.style.paddingLeft = 4;
            expandBtn.style.paddingRight = 4;
            var expandIcon = new Image { image = KitIcons.GetTexture(KitIcons.CHEVRON_RIGHT) };
            expandIcon.style.width = 12;
            expandIcon.style.height = 12;
            expandBtn.Add(expandIcon);
            header.Add(expandBtn);

            // 图标和名称
            var nameLabel = new Label($"{icon} {typeName}");
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(new Color(0.90f, 0.90f, 0.92f));
            nameLabel.style.marginLeft = 8;
            nameLabel.style.flexGrow = 1;
            header.Add(nameLabel);

            // 计数
            var countLabel = new Label("0");
            countLabel.style.fontSize = 11;
            countLabel.style.color = new StyleColor(accentColor);
            countLabel.style.backgroundColor = new StyleColor(new Color(accentColor.r, accentColor.g, accentColor.b, 0.15f));
            countLabel.style.paddingLeft = 8;
            countLabel.style.paddingRight = 8;
            countLabel.style.paddingTop = 2;
            countLabel.style.paddingBottom = 2;
            countLabel.style.borderTopLeftRadius = countLabel.style.borderTopRightRadius = 10;
            countLabel.style.borderBottomLeftRadius = countLabel.style.borderBottomRightRadius = 10;
            header.Add(countLabel);

            // 内容容器
            var itemsContainer = new VisualElement();
            itemsContainer.style.display = DisplayStyle.None;
            itemsContainer.style.paddingLeft = 36;
            itemsContainer.style.paddingRight = 12;
            itemsContainer.style.paddingBottom = 8;
            itemsContainer.style.borderTopWidth = 1;
            itemsContainer.style.borderTopColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            root.Add(itemsContainer);

            return new CategoryPanel
            {
                Root = root,
                Header = header,
                NameLabel = nameLabel,
                CountLabel = countLabel,
                ItemsContainer = itemsContainer,
                ExpandBtn = expandBtn,
                ExpandIcon = expandIcon,
                IsExpanded = false
            };
        }

        private VisualElement CreateAssetItem(ResDebugger.ResInfo info)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 32;
            item.style.marginTop = 4;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f));
            item.style.borderTopLeftRadius = item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = item.style.borderBottomRightRadius = 4;

            // 状态指示器
            var indicator = new VisualElement();
            indicator.style.width = 6;
            indicator.style.height = 6;
            indicator.style.borderTopLeftRadius = indicator.style.borderTopRightRadius = 3;
            indicator.style.borderBottomLeftRadius = indicator.style.borderBottomRightRadius = 3;
            indicator.style.backgroundColor = new StyleColor(info.IsDone ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.9f, 0.7f, 0.2f));
            indicator.style.marginRight = 8;
            item.Add(indicator);

            // 文件名
            var fileName = GetAssetName(info.Path);
            var nameLabel = new Label(fileName);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.87f));
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(nameLabel);

            // 来源标签
            var sourceTag = info.Source == ResDebugger.ResSource.ResKit ? "ResKit" : "Loader";
            var sourceLabel = new Label(sourceTag);
            sourceLabel.style.fontSize = 9;
            sourceLabel.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.58f));
            sourceLabel.style.marginRight = 8;
            item.Add(sourceLabel);

            // 引用计数
            var refLabel = new Label($"×{info.RefCount}");
            refLabel.style.fontSize = 10;
            refLabel.style.width = 32;
            refLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            refLabel.style.color = new StyleColor(info.RefCount > 1 ? new Color(0.9f, 0.7f, 0.3f) : new Color(0.55f, 0.55f, 0.58f));
            item.Add(refLabel);

            item.RegisterCallback<ClickEvent>(_ => SelectAsset(info));

            return item;
        }

        private void ToggleCategoryExpand(string typeName)
        {
            if (!mCategoryPanels.TryGetValue(typeName, out var panel)) return;

            panel.IsExpanded = !panel.IsExpanded;
            panel.ExpandIcon.image = KitIcons.GetTexture(panel.IsExpanded ? KitIcons.CHEVRON_DOWN : KitIcons.CHEVRON_RIGHT);
            panel.ItemsContainer.style.display = panel.IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            mCategoryPanels[typeName] = panel;
        }

        private void ExpandAllCategories()
        {
            foreach (var typeName in mCategoryPanels.Keys.ToList())
            {
                var panel = mCategoryPanels[typeName];
                panel.IsExpanded = true;
                panel.ExpandIcon.image = KitIcons.GetTexture(KitIcons.CHEVRON_DOWN);
                panel.ItemsContainer.style.display = DisplayStyle.Flex;
                mCategoryPanels[typeName] = panel;
            }
        }

        private void CollapseAllCategories()
        {
            foreach (var typeName in mCategoryPanels.Keys.ToList())
            {
                var panel = mCategoryPanels[typeName];
                panel.IsExpanded = false;
                panel.ExpandIcon.image = KitIcons.GetTexture(KitIcons.CHEVRON_RIGHT);
                panel.ItemsContainer.style.display = DisplayStyle.None;
                mCategoryPanels[typeName] = panel;
            }
        }

        private void RefreshCategoryDisplay()
        {
            var groupedAssets = new Dictionary<string, List<ResDebugger.ResInfo>>();
            
            foreach (var asset in mAllAssets)
            {
                if (!string.IsNullOrEmpty(mSearchFilter))
                {
                    var matchPath = asset.Path?.ToLowerInvariant().Contains(mSearchFilter) ?? false;
                    var matchType = asset.TypeName?.ToLowerInvariant().Contains(mSearchFilter) ?? false;
                    if (!matchPath && !matchType) continue;
                }

                var typeName = asset.TypeName ?? "Unknown";
                if (!groupedAssets.TryGetValue(typeName, out var list))
                {
                    list = new List<ResDebugger.ResInfo>();
                    groupedAssets[typeName] = list;
                }
                list.Add(asset);
            }

            foreach (var kvp in mCategoryPanels)
            {
                kvp.Value.Root.style.display = groupedAssets.ContainsKey(kvp.Key) 
                    ? DisplayStyle.Flex 
                    : DisplayStyle.None;
            }

            foreach (var kvp in groupedAssets.OrderBy(x => x.Key))
            {
                CreateOrUpdateCategoryPanel(kvp.Key, kvp.Value);
            }
        }
    }
}
#endif
