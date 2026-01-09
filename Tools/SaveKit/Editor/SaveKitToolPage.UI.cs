#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 工具页面 - UI 构建
    /// </summary>
    public partial class SaveKitToolPage
    {
        #region UI 构建

        private VisualElement CreateLeftPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("left-panel");

            // 头部
            var header = CreateSectionHeader("存档槽位");
            panel.Add(header);

            mSlotCountLabel = new Label("0 个存档");
            mSlotCountLabel.style.fontSize = 11;
            mSlotCountLabel.style.color = new StyleColor(Colors.TextSecondary);
            mSlotCountLabel.style.marginLeft = Spacing.MD;
            mSlotCountLabel.style.marginBottom = Spacing.SM;
            panel.Add(mSlotCountLabel);

            // 槽位列表
            mSlotListView = new ListView();
            mSlotListView.makeItem = MakeSlotItem;
            mSlotListView.bindItem = BindSlotItem;
            mSlotListView.fixedItemHeight = 60;
            mSlotListView.selectionType = SelectionType.Single;
#if UNITY_2022_1_OR_NEWER
            mSlotListView.selectionChanged += OnSlotSelectionChanged;
#else
            mSlotListView.onSelectionChange += OnSlotSelectionChanged;
#endif
            mSlotListView.style.flexGrow = 1;
            panel.Add(mSlotListView);

            return panel;
        }

        private VisualElement CreateRightPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("right-panel");
            panel.style.flexGrow = 1;

            // 空状态
            mEmptyState = CreateEmptyState("选择一个存档槽位查看详情");
            mEmptyState.style.display = DisplayStyle.Flex;
            panel.Add(mEmptyState);

            // 详情面板
            mDetailPanel = new VisualElement();
            mDetailPanel.style.flexGrow = 1;
            mDetailPanel.style.display = DisplayStyle.None;
            panel.Add(mDetailPanel);

            BuildDetailPanel();

            return panel;
        }

        private void BuildDetailPanel()
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = Spacing.XL;
            scrollView.style.paddingRight = Spacing.XL;
            scrollView.style.paddingTop = Spacing.XL;
            mDetailPanel.Add(scrollView);

            // 标题区域
            var titleRow = CreateRow();
            titleRow.style.marginBottom = Spacing.XL;
            scrollView.Add(titleRow);

            var iconBg = new VisualElement();
            iconBg.style.width = 48;
            iconBg.style.height = 48;
            iconBg.style.borderTopLeftRadius = 10;
            iconBg.style.borderTopRightRadius = 10;
            iconBg.style.borderBottomLeftRadius = 10;
            iconBg.style.borderBottomRightRadius = 10;
            iconBg.style.backgroundColor = new StyleColor(new Color(0.2f, 0.4f, 0.6f, 0.3f));
            iconBg.style.alignItems = Align.Center;
            iconBg.style.justifyContent = Justify.Center;
            iconBg.style.marginRight = Spacing.LG;
            titleRow.Add(iconBg);

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.SAVEKIT) };
            icon.style.width = 24;
            icon.style.height = 24;
            iconBg.Add(icon);

            var titleBox = new VisualElement();
            titleRow.Add(titleBox);

            mDetailSlotId = new Label("槽位 #0");
            mDetailSlotId.style.fontSize = 18;
            mDetailSlotId.style.unityFontStyleAndWeight = FontStyle.Bold;
            mDetailSlotId.style.color = new StyleColor(Colors.TextPrimary);
            titleBox.Add(mDetailSlotId);

            mDetailDisplayName = new Label();
            mDetailDisplayName.style.fontSize = 12;
            mDetailDisplayName.style.color = new StyleColor(Colors.TextSecondary);
            mDetailDisplayName.style.marginTop = Spacing.XS;
            titleBox.Add(mDetailDisplayName);

            // 信息卡片
            var infoCard = CreateInfoCard("存档信息");
            scrollView.Add(infoCard);

            var infoContent = infoCard.Q<VisualElement>("card-content");
            
            mDetailVersion = CreateDetailInfoRow(infoContent, "数据版本");
            mDetailCreatedTime = CreateDetailInfoRow(infoContent, "创建时间");
            mDetailLastSavedTime = CreateDetailInfoRow(infoContent, "最后保存");
            mDetailFileSize = CreateDetailInfoRow(infoContent, "文件大小");

            // 操作按钮
            var buttonRow = CreateRow();
            buttonRow.style.marginTop = Spacing.XL;
            scrollView.Add(buttonRow);

            var deleteBtn = CreateActionButtonWithIcon(KitIcons.DELETE, "删除存档", DeleteSelectedSlot, true);
            buttonRow.Add(deleteBtn);

            var exportBtn = CreateActionButtonWithIcon(KitIcons.SEND, "导出", ExportSelectedSlot, false);
            exportBtn.style.marginLeft = Spacing.SM;
            buttonRow.Add(exportBtn);
        }

        private VisualElement CreateInfoCard(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("card");

            var header = new VisualElement();
            header.AddToClassList("card-header");
            card.Add(header);

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("card-title");
            header.Add(titleLabel);

            var content = new VisualElement();
            content.AddToClassList("card-body");
            content.name = "card-content";
            card.Add(content);

            return card;
        }

        private Label CreateDetailInfoRow(VisualElement parent, string labelText)
        {
            var row = new VisualElement();
            row.AddToClassList("info-row");
            parent.Add(row);

            var label = new Label(labelText);
            label.AddToClassList("info-label");
            row.Add(label);

            var value = new Label("-");
            value.AddToClassList("info-value");
            row.Add(value);

            return value;
        }

        private VisualElement MakeSlotItem()
        {
            var item = new VisualElement();
            item.AddToClassList("list-item");
            item.style.minHeight = 56;
            item.style.paddingTop = 10;
            item.style.paddingBottom = 10;

            // 状态指示器
            var indicator = new VisualElement();
            indicator.AddToClassList("list-item-indicator");
            indicator.name = "indicator";
            item.Add(indicator);

            // 内容区域
            var content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.justifyContent = Justify.Center;
            item.Add(content);

            var topRow = CreateRow();
            content.Add(topRow);

            var slotLabel = new Label();
            slotLabel.name = "slot-label";
            slotLabel.style.fontSize = 13;
            slotLabel.style.color = new StyleColor(Colors.TextPrimary);
            slotLabel.style.flexGrow = 1;
            topRow.Add(slotLabel);

            var versionBadge = new Label();
            versionBadge.name = "version-badge";
            versionBadge.style.fontSize = 10;
            versionBadge.style.paddingLeft = Spacing.SM;
            versionBadge.style.paddingRight = Spacing.SM;
            versionBadge.style.paddingTop = 2;
            versionBadge.style.paddingBottom = 2;
            versionBadge.style.borderTopLeftRadius = Radius.MD;
            versionBadge.style.borderTopRightRadius = Radius.MD;
            versionBadge.style.borderBottomLeftRadius = Radius.MD;
            versionBadge.style.borderBottomRightRadius = Radius.MD;
            versionBadge.style.backgroundColor = new StyleColor(Colors.BadgeSuccess);
            versionBadge.style.color = new StyleColor(new Color(0.8f, 1f, 0.8f));
            topRow.Add(versionBadge);

            var timeLabel = new Label();
            timeLabel.name = "time-label";
            timeLabel.style.fontSize = 11;
            timeLabel.style.color = new StyleColor(Colors.TextTertiary);
            timeLabel.style.marginTop = Spacing.SM;
            content.Add(timeLabel);

            // 文件大小
            var sizeLabel = new Label();
            sizeLabel.name = "size-label";
            sizeLabel.AddToClassList("list-item-count");
            item.Add(sizeLabel);

            return item;
        }

        private void BindSlotItem(VisualElement element, int index)
        {
            var slot = mSlots[index];

            var indicator = element.Q<VisualElement>("indicator");
            var slotLabel = element.Q<Label>("slot-label");
            var versionBadge = element.Q<Label>("version-badge");
            var timeLabel = element.Q<Label>("time-label");
            var sizeLabel = element.Q<Label>("size-label");

            if (slot.Exists)
            {
                indicator.AddToClassList("active");
                indicator.RemoveFromClassList("inactive");

                var displayName = string.IsNullOrEmpty(slot.Meta.DisplayName) 
                    ? $"槽位 #{slot.SlotId}" 
                    : slot.Meta.DisplayName;
                slotLabel.text = displayName;
                
                versionBadge.text = $"v{slot.Meta.Version}";
                versionBadge.style.display = DisplayStyle.Flex;
                
                timeLabel.text = slot.Meta.GetLastSavedDateTime().ToString("yyyy-MM-dd HH:mm:ss");
                sizeLabel.text = FormatFileSize(slot.FileSize);
            }
            else
            {
                indicator.RemoveFromClassList("active");
                indicator.AddToClassList("inactive");

                slotLabel.text = $"槽位 #{slot.SlotId} (空)";
                versionBadge.style.display = DisplayStyle.None;
                timeLabel.text = "未使用";
                sizeLabel.text = "-";
            }
        }

        #endregion
    }
}
#endif
