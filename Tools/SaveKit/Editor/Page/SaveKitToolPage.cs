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
    /// SaveKit 工具页。
    /// 用于查看存档槽位、元数据、文件大小以及目录监听状态。
    /// </summary>
    [YokiToolPage(
        kit: "SaveKit",
        name: "SaveKit",
        icon: KitIcons.SAVEKIT,
        priority: 50,
        category: YokiPageCategory.Tool)]
    public partial class SaveKitToolPage : YokiToolPageBase
    {
        private const float FileWatchDebounce = 0.5f;

        private string mSavePath;
        private readonly List<SlotInfo> mSlots = new(16);
        private int mSelectedSlotIndex = -1;

        private Label mPathLabel;
        private Label mSlotCountLabel;
        private Label mExistingSlotMetricLabel;
        private Label mMaxSlotMetricLabel;
        private Label mSelectedSlotMetricLabel;
        private ListView mSlotListView;
        private VisualElement mDetailPanel;
        private VisualElement mEmptyState;

        private Label mDetailSlotId;
        private Label mDetailVersion;
        private Label mDetailDisplayName;
        private Label mDetailCreatedTime;
        private Label mDetailLastSavedTime;
        private Label mDetailFileSize;

        private FileSystemWatcher mFileWatcher;
        private bool mNeedsRefresh;
        private double mLastRefreshTime;

        private struct SlotInfo
        {
            public int SlotId;
            public SaveMeta Meta;
            public long FileSize;
            public bool Exists;
        }

        protected override void BuildUI(VisualElement root)
        {
            var scaffold = CreateKitPageScaffold(
                "SaveKit",
                "集中查看存档槽位、元数据、文件体积与目录监听状态，保留原有存档管理操作。",
                KitIcons.SAVEKIT,
                "存档工作台");
            root.Add(scaffold.Root);

            scaffold.Toolbar.Add(CreateToolbarSection());
            SetStatusContent(scaffold.StatusBar, CreateKitStatusBanner(
                "目录监听",
                "页面会持续监听存档目录变化，外部写入、删除或覆盖存档后会自动刷新。"));

            var metricStrip = CreateKitMetricStrip();
            scaffold.Content.Add(metricStrip);

            var (existingCard, existingValue) = CreateKitMetricCard("已占用槽位", "0", "当前存在的存档数量", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mExistingSlotMetricLabel = existingValue;
            metricStrip.Add(existingCard);

            var (maxCard, maxValue) = CreateKitMetricCard("槽位上限", "0", "SaveKit 当前配置上限", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mMaxSlotMetricLabel = maxValue;
            metricStrip.Add(maxCard);

            var (selectedCard, selectedValue) = CreateKitMetricCard("当前选中", "-", "未选择有效存档时显示为空", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mSelectedSlotMetricLabel = selectedValue;
            metricStrip.Add(selectedCard);

            var splitView = CreateSplitView(320f);
            scaffold.Content.Add(splitView);
            splitView.Add(CreateLeftPanel());
            splitView.Add(CreateRightPanel());

            RefreshSlots();
            SetupFileWatcher();
        }

        private VisualElement CreateToolbarSection()
        {
            var toolbar = YokiFrameUIComponents.CreateToolbar();
            toolbar.Add(YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", RefreshSlots));
            toolbar.Add(YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.FOLDER_DOCS, "打开目录", OpenSaveFolder));
            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            mPathLabel = new Label();
            mPathLabel.AddToClassList("toolbar-label");
            toolbar.Add(mPathLabel);
            return toolbar;
        }

        public override void OnActivate()
        {
            base.OnActivate();
            SetupFileWatcher();
        }

        public override void OnDeactivate()
        {
            DisposeFileWatcher();
            base.OnDeactivate();
        }

        [Obsolete("保留用于文件监听防抖刷新。")]
        public override void OnUpdate()
        {
            if (!mNeedsRefresh)
            {
                return;
            }

            mNeedsRefresh = false;
            double now = EditorApplication.timeSinceStartup;
            if (now - mLastRefreshTime <= FileWatchDebounce)
            {
                return;
            }

            mLastRefreshTime = now;
            RefreshSlots();
        }

        private void SetupFileWatcher()
        {
            DisposeFileWatcher();

            mSavePath = SaveKit.GetSavePath();
            if (!Directory.Exists(mSavePath))
            {
                try
                {
                    Directory.CreateDirectory(mSavePath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SaveKit] 无法创建存档目录: {ex.Message}");
                    return;
                }
            }

            try
            {
                var (prefix, extension) = SaveKit.GetFileFormat();
                mFileWatcher = new FileSystemWatcher(mSavePath)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    Filter = $"{prefix}*{extension}",
                    EnableRaisingEvents = true
                };

                mFileWatcher.Created += OnFileChanged;
                mFileWatcher.Deleted += OnFileChanged;
                mFileWatcher.Changed += OnFileChanged;
                mFileWatcher.Renamed += OnFileRenamed;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveKit] 无法启动文件监听: {ex.Message}");
            }
        }

        private void DisposeFileWatcher()
        {
            if (mFileWatcher == null)
            {
                return;
            }

            mFileWatcher.EnableRaisingEvents = false;
            mFileWatcher.Created -= OnFileChanged;
            mFileWatcher.Deleted -= OnFileChanged;
            mFileWatcher.Changed -= OnFileChanged;
            mFileWatcher.Renamed -= OnFileRenamed;
            mFileWatcher.Dispose();
            mFileWatcher = null;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e) => mNeedsRefresh = true;

        private void OnFileRenamed(object sender, RenamedEventArgs e) => mNeedsRefresh = true;

        private void RefreshSlots()
        {
            mSavePath = SaveKit.GetSavePath();
            mPathLabel.text = $"路径: {mSavePath}";

            int previousSelectedSlotId = mSelectedSlotIndex >= 0 && mSelectedSlotIndex < mSlots.Count
                ? mSlots[mSelectedSlotIndex].SlotId
                : -1;

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

            mSlotCountLabel.text = $"{existCount} / {maxSlots} 个存档槽位";
            mExistingSlotMetricLabel.text = existCount.ToString();
            mMaxSlotMetricLabel.text = maxSlots.ToString();

            mSlotListView.itemsSource = mSlots;
            mSlotListView.RefreshItems();
            RestoreSlotSelection(previousSelectedSlotId);
        }

        private long GetSlotFileSize(int slotId)
        {
            var (prefix, extension) = SaveKit.GetFileFormat();
            string filePath = Path.Combine(mSavePath, $"{prefix}{slotId}{extension}");
            return File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
        }

        private void OnSlotSelectionChanged(IEnumerable<object> selection)
        {
            mSelectedSlotIndex = mSlotListView.selectedIndex;
            ApplySlotSelectionState(mSelectedSlotIndex);
        }

        private void RestoreSlotSelection(int previousSelectedSlotId)
        {
            if (previousSelectedSlotId >= 0)
            {
                for (int i = 0; i < mSlots.Count; i++)
                {
                    if (mSlots[i].SlotId != previousSelectedSlotId)
                    {
                        continue;
                    }

                    mSelectedSlotIndex = i;
                    mSlotListView.SetSelectionWithoutNotify(new[] { i });
                    ApplySlotSelectionState(i);
                    return;
                }
            }

            mSelectedSlotIndex = -1;
            mSlotListView.ClearSelection();
            ApplySlotSelectionState(-1);
        }

        private void ApplySlotSelectionState(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= mSlots.Count)
            {
                mDetailPanel.style.display = DisplayStyle.None;
                mEmptyState.style.display = DisplayStyle.Flex;
                mSelectedSlotMetricLabel.text = "-";
                return;
            }

            SlotInfo slot = mSlots[slotIndex];
            mSelectedSlotMetricLabel.text = $"槽位 #{slot.SlotId}";

            if (!slot.Exists)
            {
                mDetailPanel.style.display = DisplayStyle.None;
                mEmptyState.style.display = DisplayStyle.Flex;
                return;
            }

            mDetailPanel.style.display = DisplayStyle.Flex;
            mEmptyState.style.display = DisplayStyle.None;

            mDetailSlotId.text = $"槽位 #{slot.SlotId}";
            mDetailDisplayName.text = string.IsNullOrEmpty(slot.Meta.DisplayName) ? "未设置显示名称" : slot.Meta.DisplayName;
            mDetailVersion.text = $"v{slot.Meta.Version}";
            mDetailCreatedTime.text = slot.Meta.GetCreatedDateTime().ToString("yyyy-MM-dd HH:mm:ss");
            mDetailLastSavedTime.text = slot.Meta.GetLastSavedDateTime().ToString("yyyy-MM-dd HH:mm:ss");
            mDetailFileSize.text = FormatFileSize(slot.FileSize);
        }

        private void DeleteSelectedSlot()
        {
            if (mSelectedSlotIndex < 0 || mSelectedSlotIndex >= mSlots.Count)
            {
                return;
            }

            SlotInfo slot = mSlots[mSelectedSlotIndex];
            if (!slot.Exists)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("确认删除", $"确定要删除槽位 #{slot.SlotId} 的存档吗？\n此操作不可撤销。", "删除", "取消"))
            {
                return;
            }

            SaveKit.Delete(slot.SlotId);
            RefreshSlots();
        }

        private void ExportSelectedSlot()
        {
            if (mSelectedSlotIndex < 0 || mSelectedSlotIndex >= mSlots.Count)
            {
                return;
            }

            SlotInfo slot = mSlots[mSelectedSlotIndex];
            if (!slot.Exists)
            {
                return;
            }

            var (prefix, extension) = SaveKit.GetFileFormat();
            string exportPath = EditorUtility.SaveFilePanel("导出存档", "", $"{prefix}{slot.SlotId}_export", extension.TrimStart('.'));
            if (string.IsNullOrEmpty(exportPath))
            {
                return;
            }

            try
            {
                string sourcePath = Path.Combine(mSavePath, $"{prefix}{slot.SlotId}{extension}");
                if (!File.Exists(sourcePath))
                {
                    EditorUtility.DisplayDialog("导出失败", "存档文件不存在。", "确定");
                    return;
                }

                File.Copy(sourcePath, exportPath, true);
                EditorUtility.DisplayDialog("导出成功", $"存档已导出到：\n{exportPath}", "确定");
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
                return;
            }

            EditorUtility.DisplayDialog("提示", "存档目录不存在。", "确定");
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }

            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
    }
}
#endif
