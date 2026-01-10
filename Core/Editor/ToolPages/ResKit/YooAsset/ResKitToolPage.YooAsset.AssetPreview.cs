#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - YooAsset 资源预览面板
    /// </summary>
    public partial class ResKitToolPage
    {
        #region 资源预览状态字段

        /// <summary>资源预览面板容器</summary>
        private VisualElement mYooAssetPreviewPanel;

        /// <summary>资源预览列表容器</summary>
        private VisualElement mYooAssetPreviewList;

        /// <summary>资源预览搜索框</summary>
        private TextField mYooAssetPreviewSearchField;

        /// <summary>资源预览统计标签</summary>
        private Label mYooAssetPreviewCountLabel;

        /// <summary>当前预览的收集器索引</summary>
        private int mYooPreviewCollectorIndex = -1;

        /// <summary>资源预览搜索关键词</summary>
        private string mYooAssetPreviewSearchText = "";

        /// <summary>缓存的收集资源列表</summary>
        private List<CollectAssetInfo> mYooCachedCollectAssets;

        #endregion

        /// <summary>
        /// 构建资源预览面板
        /// </summary>
        private VisualElement BuildYooAssetPreviewPanel()
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f));
            panel.style.borderLeftWidth = 1;
            panel.style.borderLeftColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));

            // 头部
            var header = new VisualElement();
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            panel.Add(header);

            // 标题行
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 8;
            header.Add(titleRow);

            var titleLabel = new Label("资源预览");
            titleLabel.style.fontSize = 13;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            titleLabel.style.flexGrow = 1;
            titleRow.Add(titleLabel);

            mYooAssetPreviewCountLabel = new Label("0 个资源");
            mYooAssetPreviewCountLabel.style.fontSize = 11;
            mYooAssetPreviewCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            titleRow.Add(mYooAssetPreviewCountLabel);

            // 搜索框
            mYooAssetPreviewSearchField = new TextField();
            mYooAssetPreviewSearchField.style.marginTop = 4;
            var placeholder = "搜索资源...";
            mYooAssetPreviewSearchField.value = placeholder;
            mYooAssetPreviewSearchField.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));

            mYooAssetPreviewSearchField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (mYooAssetPreviewSearchField.value == placeholder)
                {
                    mYooAssetPreviewSearchField.value = "";
                    mYooAssetPreviewSearchField.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
                }
            });

            mYooAssetPreviewSearchField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (string.IsNullOrEmpty(mYooAssetPreviewSearchField.value))
                {
                    mYooAssetPreviewSearchField.value = placeholder;
                    mYooAssetPreviewSearchField.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                }
            });

            mYooAssetPreviewSearchField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != placeholder)
                {
                    mYooAssetPreviewSearchText = evt.newValue;
                    RefreshYooAssetPreviewList();
                }
            });
            header.Add(mYooAssetPreviewSearchField);

            // 资源列表滚动容器
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            panel.Add(scrollView);

            mYooAssetPreviewList = new VisualElement();
            mYooAssetPreviewList.style.paddingLeft = 8;
            mYooAssetPreviewList.style.paddingRight = 8;
            mYooAssetPreviewList.style.paddingTop = 8;
            mYooAssetPreviewList.style.paddingBottom = 8;
            scrollView.Add(mYooAssetPreviewList);

            // 初始空状态
            ShowYooAssetPreviewEmptyState("选择一个收集器查看资源");

            return panel;
        }

        /// <summary>
        /// 显示资源预览空状态
        /// </summary>
        private void ShowYooAssetPreviewEmptyState(string message)
        {
            if (mYooAssetPreviewList == default) return;

            mYooAssetPreviewList.Clear();
            mYooAssetPreviewCountLabel.text = "0 个资源";

            var emptyState = CreateEmptyState(KitIcons.FOLDER, message);
            emptyState.style.marginTop = 40;
            mYooAssetPreviewList.Add(emptyState);
        }

        /// <summary>
        /// 刷新资源预览（当选中收集器变化时调用）
        /// </summary>
        private void RefreshYooAssetPreview(int collectorIndex)
        {
            mYooPreviewCollectorIndex = collectorIndex;
            mYooCachedCollectAssets = null;

            if (collectorIndex < 0)
            {
                ShowYooAssetPreviewEmptyState("选择一个收集器查看资源");
                return;
            }

            var group = YooCurrentGroup;
            if (group == default || group.Collectors == default || collectorIndex >= group.Collectors.Count)
            {
                ShowYooAssetPreviewEmptyState("收集器无效");
                return;
            }

            var collector = group.Collectors[collectorIndex];
            if (!collector.IsValid())
            {
                ShowYooAssetPreviewEmptyState("收集器配置无效");
                return;
            }

            // 获取收集到的资源
            try
            {
                var package = YooCurrentPackage;
                if (package == default) return;

                IIgnoreRule ignoreRule = AssetBundleCollectorSettingData.GetIgnoreRuleInstance(package.IgnoreRuleName);
                var command = new CollectCommand(package.PackageName, ignoreRule);
                command.SetFlag(ECollectFlags.IgnoreGetDependencies, true);
                command.UniqueBundleName = YooSetting.UniqueBundleName;
                command.EnableAddressable = package.EnableAddressable;
                command.SupportExtensionless = package.SupportExtensionless;
                command.LocationToLower = package.LocationToLower;
                command.IncludeAssetGUID = package.IncludeAssetGUID;
                command.AutoCollectShaders = package.AutoCollectShaders;

                mYooCachedCollectAssets = collector.GetAllCollectAssets(command, group);
                RefreshYooAssetPreviewList();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ResKit] 获取收集资源失败: {e.Message}");
                ShowYooAssetPreviewEmptyState("获取资源失败");
            }
        }

        /// <summary>
        /// 刷新资源预览列表（应用搜索过滤）
        /// </summary>
        private void RefreshYooAssetPreviewList()
        {
            if (mYooAssetPreviewList == default) return;

            mYooAssetPreviewList.Clear();

            if (mYooCachedCollectAssets == default || mYooCachedCollectAssets.Count == 0)
            {
                ShowYooAssetPreviewEmptyState("该收集器未收集到任何资源");
                return;
            }

            // 过滤资源
            var filteredAssets = new List<CollectAssetInfo>();
            var searchLower = mYooAssetPreviewSearchText?.ToLower() ?? "";

            foreach (var asset in mYooCachedCollectAssets)
            {
                if (string.IsNullOrEmpty(searchLower) ||
                    asset.AssetInfo.AssetPath.ToLower().Contains(searchLower) ||
                    (!string.IsNullOrEmpty(asset.Address) && asset.Address.ToLower().Contains(searchLower)))
                {
                    filteredAssets.Add(asset);
                }
            }

            // 更新统计
            if (string.IsNullOrEmpty(searchLower))
            {
                mYooAssetPreviewCountLabel.text = $"{mYooCachedCollectAssets.Count} 个资源";
            }
            else
            {
                mYooAssetPreviewCountLabel.text = $"{filteredAssets.Count} / {mYooCachedCollectAssets.Count} 个资源";
            }

            if (filteredAssets.Count == 0)
            {
                ShowYooAssetPreviewEmptyState("未找到匹配的资源");
                return;
            }

            // 创建资源项
            foreach (var asset in filteredAssets)
            {
                var item = CreateYooAssetPreviewItem(asset);
                mYooAssetPreviewList.Add(item);
            }
        }

        /// <summary>
        /// 创建资源预览项
        /// </summary>
        private VisualElement CreateYooAssetPreviewItem(CollectAssetInfo asset)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.paddingTop = 4;
            item.style.paddingBottom = 4;
            item.style.marginBottom = 2;
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;

            // 悬停效果
            item.RegisterCallback<MouseEnterEvent>(_ =>
            {
                item.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            });
            item.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                item.style.backgroundColor = StyleKeyword.None;
            });

            // 点击定位到资源
            var assetPath = asset.AssetInfo.AssetPath;
            item.RegisterCallback<ClickEvent>(_ =>
            {
                var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (obj != default)
                {
                    EditorGUIUtility.PingObject(obj);
                    Selection.activeObject = obj;
                }
            });

            // 文件图标
            var icon = new Image();
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 6;

            var assetIcon = AssetDatabase.GetCachedIcon(assetPath);
            if (assetIcon != default)
            {
                icon.image = assetIcon;
            }
            else
            {
                icon.image = KitIcons.GetTexture(KitIcons.DOCUMENT);
            }
            item.Add(icon);

            // 文件名和路径
            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1;
            textContainer.style.overflow = Overflow.Hidden;
            item.Add(textContainer);

            var fileName = System.IO.Path.GetFileName(assetPath);
            var nameLabel = new Label(fileName);
            nameLabel.style.fontSize = 12;
            nameLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            textContainer.Add(nameLabel);

            // 显示地址（如果启用了寻址）
            if (!string.IsNullOrEmpty(asset.Address))
            {
                var addressLabel = new Label(asset.Address);
                addressLabel.style.fontSize = 10;
                addressLabel.style.color = new StyleColor(new Color(0.5f, 0.7f, 0.9f));
                addressLabel.style.overflow = Overflow.Hidden;
                addressLabel.style.textOverflow = TextOverflow.Ellipsis;
                textContainer.Add(addressLabel);
            }
            else
            {
                // 显示相对路径
                var pathLabel = new Label(assetPath);
                pathLabel.style.fontSize = 10;
                pathLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                pathLabel.style.overflow = Overflow.Hidden;
                pathLabel.style.textOverflow = TextOverflow.Ellipsis;
                textContainer.Add(pathLabel);
            }

            return item;
        }

        /// <summary>
        /// 清空资源预览
        /// </summary>
        private void ClearYooAssetPreview()
        {
            mYooPreviewCollectorIndex = -1;
            mYooCachedCollectAssets = null;
            ShowYooAssetPreviewEmptyState("选择一个收集器查看资源");
        }
    }
}
#endif
