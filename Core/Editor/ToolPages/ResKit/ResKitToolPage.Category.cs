#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 分类面板
    /// 使用 USS 类消除内联样式
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
            var typeClass = GetTypeClass(typeName);

            var root = new VisualElement();
            root.AddToClassList("yoki-res-category");
            root.AddToClassList(typeClass);

            // 头部
            var header = new VisualElement();
            header.AddToClassList("yoki-res-category__header");
            root.Add(header);

            // 展开按钮
            var expandBtn = new Button(() => ToggleCategoryExpand(typeName));
            expandBtn.AddToClassList("yoki-res-category__expand-btn");
            var expandIcon = new Image { image = KitIcons.GetTexture(KitIcons.CHEVRON_RIGHT) };
            expandIcon.AddToClassList("yoki-res-category__expand-icon");
            expandBtn.Add(expandIcon);
            header.Add(expandBtn);

            // 图标和名称
            var nameLabel = new Label($"{icon} {typeName}");
            nameLabel.AddToClassList("yoki-res-category__name");
            header.Add(nameLabel);

            // 计数
            var countLabel = new Label("0");
            countLabel.AddToClassList("yoki-res-category__count");
            countLabel.AddToClassList(typeClass);
            header.Add(countLabel);

            // 内容容器
            var itemsContainer = new VisualElement();
            itemsContainer.AddToClassList("yoki-res-category__items");
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
            item.AddToClassList("yoki-res-item");

            // 状态指示器
            var indicator = new VisualElement();
            indicator.AddToClassList("yoki-res-item__indicator");
            indicator.AddToClassList(info.IsDone ? "yoki-res-item__indicator--loaded" : "yoki-res-item__indicator--loading");
            item.Add(indicator);

            // 文件名
            var fileName = GetAssetName(info.Path);
            var nameLabel = new Label(fileName);
            nameLabel.AddToClassList("yoki-res-item__name");
            item.Add(nameLabel);

            // 来源标签
            var sourceTag = info.Source == ResDebugger.ResSource.ResKit ? "ResKit" : "Loader";
            var sourceLabel = new Label(sourceTag);
            sourceLabel.AddToClassList("yoki-res-item__source");
            item.Add(sourceLabel);

            // 引用计数
            var refLabel = new Label($"×{info.RefCount}");
            refLabel.AddToClassList("yoki-res-item__ref-count");
            if (info.RefCount > 1)
            {
                refLabel.AddToClassList("yoki-res-item__ref-count--highlight");
            }
            item.Add(refLabel);

            item.RegisterCallback<ClickEvent>(_ => SelectAsset(info));

            return item;
        }

        private void ToggleCategoryExpand(string typeName)
        {
            if (!mCategoryPanels.TryGetValue(typeName, out var panel)) return;

            panel.IsExpanded = !panel.IsExpanded;
            panel.ExpandIcon.image = KitIcons.GetTexture(panel.IsExpanded ? KitIcons.CHEVRON_DOWN : KitIcons.CHEVRON_RIGHT);
            
            panel.ItemsContainer.RemoveFromClassList("yoki-res-category__items--expanded");
            if (panel.IsExpanded)
            {
                panel.ItemsContainer.AddToClassList("yoki-res-category__items--expanded");
            }
            
            mCategoryPanels[typeName] = panel;
        }

        private void ExpandAllCategories()
        {
            foreach (var typeName in mCategoryPanels.Keys.ToList())
            {
                var panel = mCategoryPanels[typeName];
                panel.IsExpanded = true;
                panel.ExpandIcon.image = KitIcons.GetTexture(KitIcons.CHEVRON_DOWN);
                panel.ItemsContainer.AddToClassList("yoki-res-category__items--expanded");
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
                panel.ItemsContainer.RemoveFromClassList("yoki-res-category__items--expanded");
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

            foreach (var kvp in groupedAssets.OrderBy(static x => x.Key))
            {
                CreateOrUpdateCategoryPanel(kvp.Key, kvp.Value);
            }
        }
    }
}
#endif
