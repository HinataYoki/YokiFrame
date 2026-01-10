#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - YooAsset 收集器卡片
    /// </summary>
    public partial class ResKitToolPage
    {
        /// <summary>收集器卡片列表容器</summary>
        private VisualElement mYooCollectorListContainer;

        /// <summary>当前 Group 描述标签</summary>
        private Label mYooGroupDescLabel;

        /// <summary>
        /// 构建收集器画布
        /// </summary>
        private VisualElement BuildYooCollectorCanvas()
        {
            var canvas = new VisualElement();
            canvas.AddToClassList("yoo-collector-canvas");
            canvas.style.flexGrow = 1;
            canvas.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.16f));

            // 头部（显示 Group 描述）
            var header = new VisualElement();
            header.style.paddingLeft = 16;
            header.style.paddingRight = 16;
            header.style.paddingTop = 12;
            header.style.paddingBottom = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            canvas.Add(header);

            mYooGroupDescLabel = new Label("选择一个分组查看收集器");
            mYooGroupDescLabel.style.fontSize = 13;
            mYooGroupDescLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            header.Add(mYooGroupDescLabel);

            // 收集器列表滚动容器
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            canvas.Add(scrollView);

            mYooCollectorListContainer = new VisualElement();
            mYooCollectorListContainer.style.paddingLeft = 16;
            mYooCollectorListContainer.style.paddingRight = 16;
            mYooCollectorListContainer.style.paddingTop = 12;
            mYooCollectorListContainer.style.paddingBottom = 12;
            scrollView.Add(mYooCollectorListContainer);

            // 底部添加按钮
            var footer = new VisualElement();
            footer.style.paddingLeft = 16;
            footer.style.paddingRight = 16;
            footer.style.paddingTop = 12;
            footer.style.paddingBottom = 16;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            canvas.Add(footer);

            var addBtn = CreateSecondaryButton("+ 添加收集器", ShowYooAddCollectorDialog);
            footer.Add(addBtn);

            return canvas;
        }

        /// <summary>
        /// 刷新收集器画布
        /// </summary>
        private void RefreshYooCollectorCanvas()
        {
            if (mYooCollectorListContainer == default)
                return;

            mYooCollectorListContainer.Clear();

            var group = YooCurrentGroup;
            if (group == default)
            {
                mYooGroupDescLabel.text = "选择一个分组查看收集器";
                var emptyState = CreateEmptyState(KitIcons.DOCUMENT, "暂无收集器", "点击下方按钮添加收集器");
                mYooCollectorListContainer.Add(emptyState);
                return;
            }

            // 更新 Group 描述
            mYooGroupDescLabel.text = string.IsNullOrEmpty(group.GroupDesc)
                ? $"分组: {group.GroupName}"
                : $"{group.GroupName} - {group.GroupDesc}";

            if (group.Collectors == default || group.Collectors.Count == 0)
            {
                var emptyState = CreateEmptyState(KitIcons.DOCUMENT, "暂无收集器", "点击下方按钮添加收集器");
                mYooCollectorListContainer.Add(emptyState);
                return;
            }

            // 创建收集器卡片
            for (int i = 0; i < group.Collectors.Count; i++)
            {
                var collector = group.Collectors[i];
                var card = CreateYooCollectorCard(collector, i);
                mYooCollectorListContainer.Add(card);
            }
        }

        /// <summary>
        /// 创建收集器卡片
        /// </summary>
        private VisualElement CreateYooCollectorCard(AssetBundleCollector collector, int index)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.marginBottom = 8;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;

            // 卡片头部
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;
            card.Add(header);

            // 文件夹图标
            var icon = new Image { image = KitIcons.GetTexture(KitIcons.FOLDER) };
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 8;
            header.Add(icon);

            // 收集路径
            var pathLabel = new Label(collector.CollectPath);
            pathLabel.style.flexGrow = 1;
            pathLabel.style.fontSize = 13;
            pathLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            pathLabel.style.overflow = Overflow.Hidden;
            pathLabel.style.textOverflow = TextOverflow.Ellipsis;
            header.Add(pathLabel);

            // 删除按钮
            var capturedCollector = collector;
            var deleteBtn = new Button(() => OnYooDeleteCollector(capturedCollector));
            deleteBtn.style.width = 24;
            deleteBtn.style.height = 24;
            deleteBtn.style.paddingLeft = 4;
            deleteBtn.style.paddingRight = 4;
            deleteBtn.style.backgroundColor = StyleKeyword.None;
            deleteBtn.style.borderTopWidth = 0;
            deleteBtn.style.borderBottomWidth = 0;
            deleteBtn.style.borderLeftWidth = 0;
            deleteBtn.style.borderRightWidth = 0;
            var deleteIcon = new Image { image = KitIcons.GetTexture(KitIcons.DELETE) };
            deleteIcon.style.width = 14;
            deleteIcon.style.height = 14;
            deleteBtn.Add(deleteIcon);
            header.Add(deleteBtn);

            // 规则徽章容器
            var badgeContainer = new VisualElement();
            badgeContainer.style.flexDirection = FlexDirection.Row;
            badgeContainer.style.flexWrap = Wrap.Wrap;
            card.Add(badgeContainer);

            // 添加规则徽章
            badgeContainer.Add(CreateYooRuleBadge(GetYooCollectorTypeShortName(collector.CollectorType), RuleBadgeType.CollectorType));
            badgeContainer.Add(CreateYooRuleBadge(GetYooShortRuleName(collector.AddressRuleName), RuleBadgeType.AddressRule));
            badgeContainer.Add(CreateYooRuleBadge(GetYooShortRuleName(collector.PackRuleName), RuleBadgeType.PackRule));
            badgeContainer.Add(CreateYooRuleBadge(GetYooShortRuleName(collector.FilterRuleName), RuleBadgeType.FilterRule));

            // AssetTags 标签
            if (!string.IsNullOrEmpty(collector.AssetTags))
            {
                foreach (var tag in collector.AssetTags.Split(';'))
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                        badgeContainer.Add(CreateYooRuleBadge(tag.Trim(), RuleBadgeType.AssetTag));
                }
            }

            // 交互事件
            int capturedIndex = index;
            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (capturedIndex != mYooExpandedCardIndex)
                    card.style.backgroundColor = new StyleColor(new Color(0.26f, 0.26f, 0.28f));
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (capturedIndex != mYooExpandedCardIndex)
                    card.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            });
            card.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;
                ToggleYooCardExpand(capturedIndex);
                evt.StopPropagation();
            });

            // 展开状态时添加编辑面板
            if (index == mYooExpandedCardIndex)
            {
                card.style.backgroundColor = new StyleColor(new Color(0.28f, 0.28f, 0.30f));
                card.Add(CreateYooCollectorEditPanel(capturedCollector));
            }

            return card;
        }

        /// <summary>规则徽章类型</summary>
        private enum RuleBadgeType { CollectorType, AddressRule, PackRule, FilterRule, AssetTag }

        /// <summary>
        /// 创建规则徽章
        /// </summary>
        private VisualElement CreateYooRuleBadge(string text, RuleBadgeType type)
        {
            var badge = new VisualElement();
            badge.style.paddingLeft = 8;
            badge.style.paddingRight = 8;
            badge.style.paddingTop = 3;
            badge.style.paddingBottom = 3;
            badge.style.marginRight = 6;
            badge.style.marginBottom = 4;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;

            var bgColor = type switch
            {
                RuleBadgeType.CollectorType => new Color(0.25f, 0.45f, 0.70f, 0.8f),
                RuleBadgeType.AddressRule => new Color(0.55f, 0.35f, 0.70f, 0.8f),
                RuleBadgeType.PackRule => new Color(0.30f, 0.60f, 0.40f, 0.8f),
                RuleBadgeType.FilterRule => new Color(0.75f, 0.50f, 0.25f, 0.8f),
                _ => new Color(0.40f, 0.40f, 0.45f, 0.8f)
            };
            badge.style.backgroundColor = new StyleColor(bgColor);

            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = new StyleColor(Color.white);
            badge.Add(label);

            return badge;
        }

        /// <summary>
        /// 显示添加收集器对话框
        /// </summary>
        private void ShowYooAddCollectorDialog()
        {
            var group = YooCurrentGroup;
            if (group == default)
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个分组", "确定");
                return;
            }

            var path = EditorUtility.OpenFolderPanel("选择收集路径", "Assets", "");
            if (string.IsNullOrEmpty(path)) return;

            if (path.StartsWith(Application.dataPath))
                path = "Assets" + path[Application.dataPath.Length..];

            CreateYooNewCollector(path);
        }

        /// <summary>
        /// 删除收集器
        /// </summary>
        private void OnYooDeleteCollector(AssetBundleCollector collector)
        {
            if (EditorUtility.DisplayDialog("确认删除", $"确定要删除收集器 \"{collector.CollectPath}\" 吗？", "删除", "取消"))
            {
                DeleteYooCollector(collector);
            }
        }
    }
}
#endif
