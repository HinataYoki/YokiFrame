#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 页面 UI 结构。
    /// </summary>
    public partial class SaveKitToolPage
    {
        private VisualElement CreateLeftPanel()
        {
            var (panel, body) = CreateKitSectionPanel("存档槽位", "显示所有槽位的占用情况、版本信息与最近保存时间。", KitIcons.SAVEKIT);
            panel.style.flexGrow = 1;

            mSlotCountLabel = new Label("0 / 0 个存档槽位");
            mSlotCountLabel.style.fontSize = 11;
            mSlotCountLabel.style.color = new StyleColor(Colors.TextSecondary);
            mSlotCountLabel.style.marginBottom = Spacing.SM;
            body.Add(mSlotCountLabel);

            mSlotListView = new ListView
            {
                makeItem = MakeSlotItem,
                bindItem = BindSlotItem,
                fixedItemHeight = 60,
                selectionType = SelectionType.Single
            };
            mSlotListView.AddToClassList("yoki-save-list");
            mSlotListView.style.flexGrow = 1;

#if UNITY_2022_1_OR_NEWER
            mSlotListView.selectionChanged += OnSlotSelectionChanged;
#else
            mSlotListView.onSelectionChange += OnSlotSelectionChanged;
#endif

            body.Add(mSlotListView);
            return panel;
        }

        private VisualElement CreateRightPanel()
        {
            var (panel, body) = CreateKitSectionPanel("存档详情", "查看选中存档的元数据与导出、删除操作。", KitIcons.DOCUMENTATION);
            panel.style.flexGrow = 1;

            mEmptyState = CreateEmptyState(KitIcons.SAVEKIT, "选择一个有效存档查看详情", "空槽位不会显示详细元数据。");
            mEmptyState.style.display = DisplayStyle.Flex;
            body.Add(mEmptyState);

            mDetailPanel = new VisualElement();
            mDetailPanel.style.flexGrow = 1;
            mDetailPanel.style.display = DisplayStyle.None;
            body.Add(mDetailPanel);

            BuildDetailPanel();
            return panel;
        }

        private void BuildDetailPanel()
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            mDetailPanel.Add(scrollView);

            var titleRow = CreateRow();
            titleRow.style.marginBottom = Spacing.XL;
            scrollView.Add(titleRow);

            var iconWrap = new VisualElement();
            iconWrap.style.width = 48;
            iconWrap.style.height = 48;
            iconWrap.style.marginRight = Spacing.LG;
            iconWrap.style.alignItems = Align.Center;
            iconWrap.style.justifyContent = Justify.Center;
            iconWrap.style.backgroundColor = new StyleColor(Colors.WorkbenchPrimarySoft);
            iconWrap.style.borderTopLeftRadius = 12;
            iconWrap.style.borderTopRightRadius = 12;
            iconWrap.style.borderBottomLeftRadius = 12;
            iconWrap.style.borderBottomRightRadius = 12;
            titleRow.Add(iconWrap);

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.SAVEKIT) };
            icon.style.width = 24;
            icon.style.height = 24;
            iconWrap.Add(icon);

            var titleBox = new VisualElement();
            titleBox.style.flexGrow = 1;
            titleRow.Add(titleBox);

            mDetailSlotId = new Label("槽位 #0");
            mDetailSlotId.style.fontSize = 18;
            mDetailSlotId.style.unityFontStyleAndWeight = FontStyle.Bold;
            mDetailSlotId.style.color = new StyleColor(Colors.TextPrimary);
            titleBox.Add(mDetailSlotId);

            mDetailDisplayName = new Label("未设置显示名称");
            mDetailDisplayName.style.fontSize = 12;
            mDetailDisplayName.style.marginTop = Spacing.XS;
            mDetailDisplayName.style.color = new StyleColor(Colors.TextSecondary);
            titleBox.Add(mDetailDisplayName);

            var (infoCard, infoBody) = CreateCard("存档信息", KitIcons.CHART);
            scrollView.Add(infoCard);

            infoBody.Add(CreateDetailInfoRow("数据版本", out mDetailVersion));
            infoBody.Add(CreateDetailInfoRow("创建时间", out mDetailCreatedTime));
            infoBody.Add(CreateDetailInfoRow("最后保存", out mDetailLastSavedTime));
            infoBody.Add(CreateDetailInfoRow("文件大小", out mDetailFileSize));

            var buttonRow = CreateRow();
            buttonRow.style.marginTop = Spacing.XL;
            scrollView.Add(buttonRow);

            var deleteButton = CreateActionButtonWithIcon(KitIcons.DELETE, "删除存档", DeleteSelectedSlot, true);
            buttonRow.Add(deleteButton);

            var exportButton = CreateActionButtonWithIcon(KitIcons.SEND, "导出", ExportSelectedSlot, false);
            exportButton.style.marginLeft = Spacing.SM;
            buttonRow.Add(exportButton);
        }

        private VisualElement CreateDetailInfoRow(string labelText, out Label valueLabel)
        {
            var (row, value) = CreateInfoRow(labelText);
            valueLabel = value;
            return row;
        }

        private VisualElement MakeSlotItem()
        {
            var item = new VisualElement();
            item.AddToClassList("yoki-save-item");

            var indicator = new VisualElement();
            indicator.AddToClassList("list-item-indicator");
            indicator.name = "indicator";
            item.Add(indicator);

            var content = new VisualElement();
            content.AddToClassList("yoki-save-item__info");
            item.Add(content);

            var topRow = CreateRow();
            content.Add(topRow);

            var slotLabel = new Label();
            slotLabel.name = "slot-label";
            slotLabel.AddToClassList("yoki-save-item__name");
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
            versionBadge.style.backgroundColor = new StyleColor(Colors.WorkbenchPrimarySoft);
            versionBadge.style.color = new StyleColor(Colors.WorkbenchPrimaryText);
            topRow.Add(versionBadge);

            var timeLabel = new Label();
            timeLabel.name = "time-label";
            timeLabel.AddToClassList("yoki-save-item__meta");
            content.Add(timeLabel);

            var sizeLabel = new Label();
            sizeLabel.name = "size-label";
            sizeLabel.AddToClassList("list-item-count");
            item.Add(sizeLabel);

            return item;
        }

        private void BindSlotItem(VisualElement element, int index)
        {
            if (index < 0 || index >= mSlots.Count)
            {
                return;
            }

            SlotInfo slot = mSlots[index];

            var indicator = element.Q<VisualElement>("indicator");
            var slotLabel = element.Q<Label>("slot-label");
            var versionBadge = element.Q<Label>("version-badge");
            var timeLabel = element.Q<Label>("time-label");
            var sizeLabel = element.Q<Label>("size-label");

            if (slot.Exists)
            {
                indicator.RemoveFromClassList("inactive");
                indicator.AddToClassList("active");

                slotLabel.text = string.IsNullOrEmpty(slot.Meta.DisplayName) ? $"槽位 #{slot.SlotId}" : slot.Meta.DisplayName;
                versionBadge.text = $"v{slot.Meta.Version}";
                versionBadge.style.display = DisplayStyle.Flex;
                timeLabel.text = slot.Meta.GetLastSavedDateTime().ToString("yyyy-MM-dd HH:mm:ss");
                sizeLabel.text = FormatFileSize(slot.FileSize);
                return;
            }

            indicator.RemoveFromClassList("active");
            indicator.AddToClassList("inactive");

            slotLabel.text = $"槽位 #{slot.SlotId}（空）";
            versionBadge.style.display = DisplayStyle.None;
            timeLabel.text = "未使用";
            sizeLabel.text = "-";
        }
    }
}
#endif
