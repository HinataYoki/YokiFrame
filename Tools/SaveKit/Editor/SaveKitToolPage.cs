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
    /// SaveKit 工具页面 - 存档管理器（响应式）
    /// 使用 FileSystemWatcher 监控存档目录变化
    /// </summary>
    [YokiToolPage(
        kit: "SaveKit",
        name: "SaveKit",
        icon: KitIcons.SAVEKIT,
        priority: 50,
        category: YokiPageCategory.Tool)]
    public partial class SaveKitToolPage : YokiToolPageBase
    {

        #region 常量

        private const float FILE_WATCH_DEBOUNCE = 0.5f;

        #endregion

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

        // 文件监控
        private FileSystemWatcher mFileWatcher;
        private bool mNeedsRefresh;
        private double mLastRefreshTime;

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
            var toolbar = YokiFrameUIComponents.CreateToolbar();
            root.Add(toolbar);

            var refreshBtn = YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", RefreshSlots);
            toolbar.Add(refreshBtn);

            var openFolderBtn = YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.FOLDER_DOCS, "打开目录", OpenSaveFolder);
            toolbar.Add(openFolderBtn);

            var spacer = YokiFrameUIComponents.CreateFlexSpacer();
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

            // 启动文件监控
            SetupFileWatcher();
        }

        public override void OnActivate()
        {
            base.OnActivate();
            SetupFileWatcher();
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            DisposeFileWatcher();
        }

        [Obsolete("保留用于文件监控防抖刷新")]
        public override void OnUpdate()
        {
            // 检查是否需要刷新（从文件监控线程触发）
            if (mNeedsRefresh)
            {
                mNeedsRefresh = false;
                
                // 使用简单的时间检查实现防抖
                var now = EditorApplication.timeSinceStartup;
                if (now - mLastRefreshTime > FILE_WATCH_DEBOUNCE)
                {
                    mLastRefreshTime = now;
                    RefreshSlots();
                }
            }
        }

        #region 文件监控

        private void SetupFileWatcher()
        {
            DisposeFileWatcher();

            mSavePath = SaveKit.GetSavePath();
            
            // 确保目录存在
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
                // 获取当前文件格式配置
                var (prefix, extension) = SaveKit.GetFileFormat();
                
                mFileWatcher = new FileSystemWatcher(mSavePath)
                {
                    NotifyFilter = NotifyFilters.FileName 
                                 | NotifyFilters.LastWrite 
                                 | NotifyFilters.Size,
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
                Debug.LogWarning($"[SaveKit] 无法启动文件监控: {ex.Message}");
            }
        }

        private void DisposeFileWatcher()
        {
            if (mFileWatcher != null)
            {
                mFileWatcher.EnableRaisingEvents = false;
                mFileWatcher.Created -= OnFileChanged;
                mFileWatcher.Deleted -= OnFileChanged;
                mFileWatcher.Changed -= OnFileChanged;
                mFileWatcher.Renamed -= OnFileRenamed;
                mFileWatcher.Dispose();
                mFileWatcher = null;
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e) => mNeedsRefresh = true;

        private void OnFileRenamed(object sender, RenamedEventArgs e) => mNeedsRefresh = true;

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
            var (prefix, extension) = SaveKit.GetFileFormat();
            var filePath = Path.Combine(mSavePath, $"{prefix}{slotId}{extension}");

            if (File.Exists(filePath))
                return new FileInfo(filePath).Length;

            return 0;
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

            var (prefix, extension) = SaveKit.GetFileFormat();
            var extWithoutDot = extension.TrimStart('.');

            var exportPath = EditorUtility.SaveFilePanel(
                "导出存档", "", $"{prefix}{slot.SlotId}_export", extWithoutDot);

            if (string.IsNullOrEmpty(exportPath)) return;

            try
            {
                var sourcePath = Path.Combine(mSavePath, $"{prefix}{slot.SlotId}{extension}");

                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, exportPath, true);
                    EditorUtility.DisplayDialog("导出成功", $"存档已导出到:\n{exportPath}", "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("导出失败", "存档文件不存在", "确定");
                }
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
