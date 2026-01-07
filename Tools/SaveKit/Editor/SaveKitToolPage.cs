#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 工具页面 - 存档管理器
    /// </summary>
    public class SaveKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "SaveKit";
        public override string PageIcon => KitIcons.SAVEKIT;
        public override int Priority => 50;

        #region 私有字段

        private string mSavePath;
        private readonly List<SlotInfo> mSlots = new(16);
        private int mSelectedSlotIndex = -1;
        
        // UI 元素
        private Label mPathLabel;
        private Label mSlotCountLabel;
        private ListView mSlotListView;
        private VisualElement mDetailPanel;
        private VisualElement mEmptyState;
        
        // 详情面板元素
        private Label mDetailSlotId;
        private Label mDetailVersion;
        private Label mDetailDisplayName;
        private Label mDetailCreatedTime;
        private Label mDetailLastSavedTime;
        private Label mDetailFileSize;

        #endregion

        #region 数据结构

        private struct SlotInfo
        {
            public int SlotId;
            public SaveMeta Meta;
            public long FileSize;
            public bool Exists;
        }

        #endregion

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = CreateToolbar();
            root.Add(toolbar);

            var refreshBtn = CreateToolbarButton("🔄 刷新", RefreshSlots);
            toolbar.Add(refreshBtn);

            var openFolderBtn = CreateToolbarButton("📂 打开目录", OpenSaveFolder);
            toolbar.Add(openFolderBtn);

            var spacer = new VisualElement();
            spacer.AddToClassList("toolbar-spacer");
            toolbar.Add(spacer);

            mPathLabel = new Label();
            mPathLabel.AddToClassList("toolbar-label");
            toolbar.Add(mPathLabel);

            // 主内容区域
            var splitView = CreateSplitView(320f);
            root.Add(splitView);

            // 左侧：槽位列表
            var leftPanel = CreateLeftPanel();
            splitView.Add(leftPanel);

            // 右侧：详情面板
            var rightPanel = CreateRightPanel();
            splitView.Add(rightPanel);

            // 初始加载
            RefreshSlots();
        }

        #region UI 构建

        private VisualElement CreateLeftPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("left-panel");

            // 头部
            var header = CreatePanelHeader("存档槽位");
            panel.Add(header);

            mSlotCountLabel = new Label("0 个存档");
            mSlotCountLabel.style.fontSize = 11;
            mSlotCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            mSlotCountLabel.style.marginLeft = 12;
            mSlotCountLabel.style.marginBottom = 8;
            panel.Add(mSlotCountLabel);

            // 槽位列表
            mSlotListView = new ListView();
            mSlotListView.makeItem = MakeSlotItem;
            mSlotListView.bindItem = BindSlotItem;
            mSlotListView.fixedItemHeight = 60;
            mSlotListView.selectionType = SelectionType.Single;
            mSlotListView.selectionChanged += OnSlotSelectionChanged;
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
            scrollView.style.paddingLeft = 20;
            scrollView.style.paddingRight = 20;
            scrollView.style.paddingTop = 20;
            mDetailPanel.Add(scrollView);

            // 标题区域
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 20;
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
            iconBg.style.marginRight = 16;
            titleRow.Add(iconBg);

            var icon = new Label(KitIcons.SAVEKIT);
            icon.style.fontSize = 24;
            iconBg.Add(icon);

            var titleBox = new VisualElement();
            titleRow.Add(titleBox);

            mDetailSlotId = new Label("槽位 #0");
            mDetailSlotId.style.fontSize = 18;
            mDetailSlotId.style.unityFontStyleAndWeight = FontStyle.Bold;
            mDetailSlotId.style.color = new StyleColor(new Color(0.95f, 0.95f, 0.95f));
            titleBox.Add(mDetailSlotId);

            mDetailDisplayName = new Label();
            mDetailDisplayName.style.fontSize = 12;
            mDetailDisplayName.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            mDetailDisplayName.style.marginTop = 4;
            titleBox.Add(mDetailDisplayName);

            // 信息卡片
            var infoCard = CreateInfoCard("存档信息");
            scrollView.Add(infoCard);

            var infoContent = infoCard.Q<VisualElement>("card-content");
            
            mDetailVersion = CreateInfoRow(infoContent, "数据版本");
            mDetailCreatedTime = CreateInfoRow(infoContent, "创建时间");
            mDetailLastSavedTime = CreateInfoRow(infoContent, "最后保存");
            mDetailFileSize = CreateInfoRow(infoContent, "文件大小");

            // 操作按钮
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 20;
            scrollView.Add(buttonRow);

            var deleteBtn = new Button(DeleteSelectedSlot) { text = "🗑️ 删除存档" };
            deleteBtn.AddToClassList("action-button");
            deleteBtn.AddToClassList("danger");
            buttonRow.Add(deleteBtn);

            var exportBtn = new Button(ExportSelectedSlot) { text = "📤 导出" };
            exportBtn.AddToClassList("action-button");
            exportBtn.style.marginLeft = 8;
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

        private Label CreateInfoRow(VisualElement parent, string labelText)
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

            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.alignItems = Align.Center;
            content.Add(topRow);

            var slotLabel = new Label();
            slotLabel.name = "slot-label";
            slotLabel.style.fontSize = 13;
            slotLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            slotLabel.style.flexGrow = 1;
            topRow.Add(slotLabel);

            var versionBadge = new Label();
            versionBadge.name = "version-badge";
            versionBadge.style.fontSize = 10;
            versionBadge.style.paddingLeft = 6;
            versionBadge.style.paddingRight = 6;
            versionBadge.style.paddingTop = 2;
            versionBadge.style.paddingBottom = 2;
            versionBadge.style.borderTopLeftRadius = 4;
            versionBadge.style.borderTopRightRadius = 4;
            versionBadge.style.borderBottomLeftRadius = 4;
            versionBadge.style.borderBottomRightRadius = 4;
            versionBadge.style.backgroundColor = new StyleColor(new Color(0.3f, 0.5f, 0.3f));
            versionBadge.style.color = new StyleColor(new Color(0.8f, 1f, 0.8f));
            topRow.Add(versionBadge);

            var timeLabel = new Label();
            timeLabel.name = "time-label";
            timeLabel.style.fontSize = 11;
            timeLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            timeLabel.style.marginTop = 6;
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

        #region 数据操作

        private void RefreshSlots()
        {
            mSavePath = SaveKit.GetSavePath();
            mPathLabel.text = $"路径: {mSavePath}";

            mSlots.Clear();
            int maxSlots = SaveKit.GetMaxSlots();
            int existCount = 0;

            for (int i = 0; i < maxSlots; i++)
            {
                var slotInfo = new SlotInfo
                {
                    SlotId = i,
                    Exists = SaveKit.Exists(i)
                };

                if (slotInfo.Exists)
                {
                    slotInfo.Meta = SaveKit.GetMeta(i);
                    slotInfo.FileSize = GetSlotFileSize(i);
                    existCount++;
                }

                mSlots.Add(slotInfo);
            }

            mSlotCountLabel.text = $"{existCount} 个存档 / {maxSlots} 槽位";
            mSlotListView.itemsSource = mSlots;
            mSlotListView.RefreshItems();

            // 清除选择
            mSelectedSlotIndex = -1;
            mDetailPanel.style.display = DisplayStyle.None;
            mEmptyState.style.display = DisplayStyle.Flex;
        }

        private long GetSlotFileSize(int slotId)
        {
            var dataPath = Path.Combine(mSavePath, $"save_{slotId}.dat");
            var metaPath = Path.Combine(mSavePath, $"save_{slotId}.meta");

            long size = 0;
            if (File.Exists(dataPath))
                size += new FileInfo(dataPath).Length;
            if (File.Exists(metaPath))
                size += new FileInfo(metaPath).Length;

            return size;
        }

        private void OnSlotSelectionChanged(IEnumerable<object> selection)
        {
            mSelectedSlotIndex = mSlotListView.selectedIndex;

            if (mSelectedSlotIndex < 0 || mSelectedSlotIndex >= mSlots.Count)
            {
                mDetailPanel.style.display = DisplayStyle.None;
                mEmptyState.style.display = DisplayStyle.Flex;
                return;
            }

            var slot = mSlots[mSelectedSlotIndex];
            
            if (!slot.Exists)
            {
                mDetailPanel.style.display = DisplayStyle.None;
                mEmptyState.style.display = DisplayStyle.Flex;
                return;
            }

            // 显示详情
            mDetailPanel.style.display = DisplayStyle.Flex;
            mEmptyState.style.display = DisplayStyle.None;

            mDetailSlotId.text = $"槽位 #{slot.SlotId}";
            mDetailDisplayName.text = string.IsNullOrEmpty(slot.Meta.DisplayName) 
                ? "无显示名称" 
                : slot.Meta.DisplayName;
            mDetailVersion.text = $"v{slot.Meta.Version}";
            mDetailCreatedTime.text = slot.Meta.GetCreatedDateTime().ToString("yyyy-MM-dd HH:mm:ss");
            mDetailLastSavedTime.text = slot.Meta.GetLastSavedDateTime().ToString("yyyy-MM-dd HH:mm:ss");
            mDetailFileSize.text = FormatFileSize(slot.FileSize);
        }

        private void DeleteSelectedSlot()
        {
            if (mSelectedSlotIndex < 0 || mSelectedSlotIndex >= mSlots.Count) return;

            var slot = mSlots[mSelectedSlotIndex];
            if (!slot.Exists) return;

            if (EditorUtility.DisplayDialog("确认删除", 
                $"确定要删除槽位 #{slot.SlotId} 的存档吗？\n此操作不可撤销。", 
                "删除", "取消"))
            {
                SaveKit.Delete(slot.SlotId);
                RefreshSlots();
            }
        }

        private void ExportSelectedSlot()
        {
            if (mSelectedSlotIndex < 0 || mSelectedSlotIndex >= mSlots.Count) return;

            var slot = mSlots[mSelectedSlotIndex];
            if (!slot.Exists) return;

            var exportPath = EditorUtility.SaveFilePanel(
                "导出存档",
                "",
                $"save_{slot.SlotId}_export",
                "zip");

            if (string.IsNullOrEmpty(exportPath)) return;

            try
            {
                var dataPath = Path.Combine(mSavePath, $"save_{slot.SlotId}.dat");
                var metaPath = Path.Combine(mSavePath, $"save_{slot.SlotId}.meta");

                // 简单导出：复制文件到目标目录
                var exportDir = Path.GetDirectoryName(exportPath);
                var baseName = Path.GetFileNameWithoutExtension(exportPath);
                
                if (File.Exists(dataPath))
                    File.Copy(dataPath, Path.Combine(exportDir, $"{baseName}.dat"), true);
                if (File.Exists(metaPath))
                    File.Copy(metaPath, Path.Combine(exportDir, $"{baseName}.meta"), true);

                EditorUtility.DisplayDialog("导出成功", $"存档已导出到:\n{exportDir}", "确定");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导出失败", ex.Message, "确定");
            }
        }

        private void OpenSaveFolder()
        {
            if (Directory.Exists(mSavePath))
            {
                EditorUtility.RevealInFinder(mSavePath);
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "存档目录不存在", "确定");
            }
        }

        #endregion

        #region 工具方法

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }

        #endregion
    }
}
#endif
