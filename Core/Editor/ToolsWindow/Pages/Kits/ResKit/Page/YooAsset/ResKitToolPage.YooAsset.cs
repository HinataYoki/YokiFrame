#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - YooAsset Collector 仪表盘。
    /// </summary>
    public partial class ResKitToolPage
    {
        #region YooAsset 状态字段

        /// <summary>当前选中的 Package 索引</summary>
        private int mYooSelectedPackageIndex;

        /// <summary>当前选中的 Group 索引</summary>
        private int mYooSelectedGroupIndex;

        /// <summary>是否有未保存的更改</summary>
        private bool mYooHasUnsavedChanges;

        /// <summary>包设置面板是否展开</summary>
        private bool mYooPackageSettingsExpanded;

        /// <summary>全局设置面板是否展开</summary>
        private bool mYooGlobalSettingsExpanded;

        /// <summary>当前正在编辑的分组索引（-1 表示无）</summary>
        private int mYooEditingGroupIndex = -1;

        /// <summary>SO 文件上次修改时间戳</summary>
        private long mYooSettingLastWriteTime;

        /// <summary>SO 文件路径缓存</summary>
        private string mYooSettingAssetPath;

        /// <summary>是否已注册 Undo 回调</summary>
        private bool mYooUndoCallbackRegistered;

        /// <summary>上次检测到的包数量</summary>
        private int mYooLastPackageCount;

        /// <summary>上次检测到的分组数量</summary>
        private int mYooLastGroupCount;

        /// <summary>上次检测到的收集器数量</summary>
        private int mYooLastCollectorCount;

        #endregion

        #region YooAsset UI 元素引用

        /// <summary>工具栏容器</summary>
        private VisualElement mYooToolbar;

        /// <summary>全局设置面板</summary>
        private VisualElement mYooGlobalSettingsPanel;

        /// <summary>包设置面板</summary>
        private VisualElement mYooPackageSettingsPanel;

        /// <summary>分组导航容器</summary>
        private VisualElement mYooGroupNavContainer;

        /// <summary>收集器画布</summary>
        private VisualElement mYooCollectorCanvas;

        /// <summary>Package 下拉选择器</summary>
        private DropdownField mYooPackageDropdown;

        /// <summary>添加资源包按钮</summary>
        private Button mYooAddPackageBtn;

        /// <summary>删除资源包按钮</summary>
        private Button mYooRemovePackageBtn;

        /// <summary>未保存提示标签</summary>
        private Label mYooUnsavedLabel;

        #endregion

        /// <summary>
        /// 构建 YooAsset Collector 仪表盘界面。
        /// </summary>
        private void BuildYooAssetUI(VisualElement root)
        {
            mYooToolbar = BuildYooToolbar();
            root.Add(mYooToolbar);

            mYooGlobalSettingsPanel = BuildYooGlobalSettingsPanel();
            mYooGlobalSettingsPanel.style.display = DisplayStyle.None;
            root.Add(mYooGlobalSettingsPanel);

            mYooPackageSettingsPanel = BuildYooPackageSettingsPanel();
            mYooPackageSettingsPanel.style.display = DisplayStyle.None;
            root.Add(mYooPackageSettingsPanel);

            var mainSplitView = CreateSplitView(250f, "YokiFrame.ResKit.GroupNavWidth");
            mainSplitView.style.flexGrow = 1;
            root.Add(mainSplitView);

            mYooGroupNavContainer = BuildYooGroupNav();
            mainSplitView.Add(mYooGroupNavContainer);

            var rightSplitView = CreateSplitView(400f, "YokiFrame.ResKit.PreviewWidth");
            rightSplitView.style.flexGrow = 1;
            mainSplitView.Add(rightSplitView);

            mYooCollectorCanvas = BuildYooCollectorCanvas();
            rightSplitView.Add(mYooCollectorCanvas);

            mYooAssetPreviewPanel = BuildYooAssetPreviewPanel();
            rightSplitView.Add(mYooAssetPreviewPanel);

            InitYooData();
        }

        /// <summary>
        /// 初始化 YooAsset 数据。
        /// </summary>
        private void InitYooData()
        {
            var setting = YooSetting;
            if (setting == default)
            {
                ResetYooUnavailableState();
                UnityEngine.Debug.LogError("[ResKit] 无法加载 YooAsset 配置文件");
                return;
            }

            CacheYooSettingFileInfo();
            RegisterYooUndoCallback();

            if (setting.Packages == default || setting.Packages.Count == 0)
            {
                YooAsset.Editor.AssetBundleCollectorSettingData.CreatePackage("DefaultPackage");
                YooAsset.Editor.AssetBundleCollectorSettingData.SaveFile();
            }

            mYooSelectedPackageIndex = 0;
            mYooSelectedGroupIndex = 0;
            mYooHasUnsavedChanges = false;
            mYooGlobalSettingsExpanded = false;
            mYooPackageSettingsExpanded = false;
            mYooEditingGroupIndex = -1;

            CacheYooDataState();

            RefreshYooPackageDropdown();
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
            RefreshYooRemovePackageButton();
        }

        /// <summary>
        /// 注册 Undo 回调。
        /// </summary>
        private void RegisterYooUndoCallback()
        {
            if (mYooUndoCallbackRegistered)
            {
                return;
            }

            Undo.undoRedoPerformed += OnYooUndoRedoPerformed;
            mYooUndoCallbackRegistered = true;
        }

        /// <summary>
        /// 注销 Undo 回调。
        /// </summary>
        private void UnregisterYooUndoCallback()
        {
            if (!mYooUndoCallbackRegistered)
            {
                return;
            }

            Undo.undoRedoPerformed -= OnYooUndoRedoPerformed;
            mYooUndoCallbackRegistered = false;
        }

        /// <summary>
        /// Undo/Redo 执行后的回调。
        /// </summary>
        private void OnYooUndoRedoPerformed()
        {
            EditorApplication.delayCall += () =>
            {
                if (YooSetting == default)
                {
                    return;
                }

                ManualRefreshYooUI();
            };
        }

        /// <summary>
        /// 缓存当前数据状态。
        /// </summary>
        private void CacheYooDataState()
        {
            if (YooSetting == default || YooSetting.Packages == default)
            {
                mYooLastPackageCount = 0;
                mYooLastGroupCount = 0;
                mYooLastCollectorCount = 0;
                return;
            }

            mYooLastPackageCount = YooSetting.Packages.Count;

            var package = YooCurrentPackage;
            mYooLastGroupCount = package?.Groups?.Count ?? 0;

            var group = YooCurrentGroup;
            mYooLastCollectorCount = group?.Collectors?.Count ?? 0;
        }

        /// <summary>
        /// 检测内存数据是否变化。
        /// </summary>
        private bool CheckYooMemoryDataChange()
        {
            if (YooSetting == default || YooSetting.Packages == default)
            {
                return false;
            }

            if (YooSetting.Packages.Count != mYooLastPackageCount)
            {
                return true;
            }

            var package = YooCurrentPackage;
            int currentGroupCount = package?.Groups?.Count ?? 0;
            if (currentGroupCount != mYooLastGroupCount)
            {
                return true;
            }

            var group = YooCurrentGroup;
            int currentCollectorCount = group?.Collectors?.Count ?? 0;
            if (currentCollectorCount != mYooLastCollectorCount)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 缓存 SO 文件信息。
        /// </summary>
        private void CacheYooSettingFileInfo()
        {
            var setting = YooSetting;
            if (setting == default)
            {
                return;
            }

            mYooSettingAssetPath = AssetDatabase.GetAssetPath(setting);
            if (string.IsNullOrEmpty(mYooSettingAssetPath))
            {
                return;
            }

            var fullPath = System.IO.Path.GetFullPath(mYooSettingAssetPath);
            if (System.IO.File.Exists(fullPath))
            {
                mYooSettingLastWriteTime = System.IO.File.GetLastWriteTimeUtc(fullPath).Ticks;
            }
        }

        /// <summary>
        /// 检测 SO 文件是否被外部修改。
        /// </summary>
        private bool CheckYooSettingExternalChange()
        {
            if (string.IsNullOrEmpty(mYooSettingAssetPath))
            {
                return false;
            }

            var fullPath = System.IO.Path.GetFullPath(mYooSettingAssetPath);
            if (!System.IO.File.Exists(fullPath))
            {
                return false;
            }

            var currentWriteTime = System.IO.File.GetLastWriteTimeUtc(fullPath).Ticks;
            if (currentWriteTime != mYooSettingLastWriteTime)
            {
                mYooSettingLastWriteTime = currentWriteTime;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 刷新全部 YooAsset UI。
        /// </summary>
        private void RefreshAllYooUI()
        {
            if (!string.IsNullOrEmpty(mYooSettingAssetPath))
            {
                AssetDatabase.ImportAsset(mYooSettingAssetPath, ImportAssetOptions.ForceUpdate);
            }

            if (YooSetting == default || YooSetting.Packages == default)
            {
                ResetYooUnavailableState();
                return;
            }

            if (mYooSelectedPackageIndex >= YooSetting.Packages.Count)
            {
                mYooSelectedPackageIndex = YooSetting.Packages.Count > 0 ? YooSetting.Packages.Count - 1 : 0;
            }

            var package = YooCurrentPackage;
            if (package != default && package.Groups != default && mYooSelectedGroupIndex >= package.Groups.Count)
            {
                mYooSelectedGroupIndex = package.Groups.Count > 0 ? package.Groups.Count - 1 : 0;
            }

            mYooExpandedCardIndex = -1;
            CacheYooDataState();

            RefreshYooPackageDropdown();
            RefreshYooPackageSettingsPanel();
            RefreshYooGlobalSettingsPanel();
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
            RefreshYooRemovePackageButton();
        }

        /// <summary>
        /// YooAsset 标签页的 Update 检测。
        /// </summary>
        private void OnYooAssetUpdate()
        {
            if (CheckYooSettingExternalChange())
            {
                RefreshAllYooUI();
                return;
            }

            if (CheckYooMemoryDataChange())
            {
                CacheYooDataState();
                RefreshYooPackageDropdown();
                RefreshYooPackageSettingsPanel();
                RefreshYooGlobalSettingsPanel();
                RefreshYooGroupNav();
                RefreshYooCollectorCanvas();
                RefreshYooRemovePackageButton();
            }
        }

        /// <summary>
        /// 手动刷新 YooAsset UI。
        /// </summary>
        private void ManualRefreshYooUI()
        {
            if (!string.IsNullOrEmpty(mYooSettingAssetPath))
            {
                AssetDatabase.ImportAsset(mYooSettingAssetPath, ImportAssetOptions.ForceUpdate);
            }

            CacheYooSettingFileInfo();

            if (YooSetting == default || YooSetting.Packages == default)
            {
                ResetYooUnavailableState();
                return;
            }

            if (mYooSelectedPackageIndex >= YooSetting.Packages.Count)
            {
                mYooSelectedPackageIndex = YooSetting.Packages.Count > 0 ? YooSetting.Packages.Count - 1 : 0;
            }

            var package = YooCurrentPackage;
            if (package != default && package.Groups != default && mYooSelectedGroupIndex >= package.Groups.Count)
            {
                mYooSelectedGroupIndex = package.Groups.Count > 0 ? package.Groups.Count - 1 : 0;
            }

            mYooExpandedCardIndex = -1;
            CacheYooDataState();

            RefreshYooPackageDropdown();
            RefreshYooPackageSettingsPanel();
            RefreshYooGlobalSettingsPanel();
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
            RefreshYooRemovePackageButton();
        }

        /// <summary>
        /// 当配置不可用时清空相关 UI，避免残留旧状态。
        /// </summary>
        private void ResetYooUnavailableState()
        {
            mYooSelectedPackageIndex = 0;
            mYooSelectedGroupIndex = 0;
            mYooExpandedCardIndex = -1;
            mYooEditingGroupIndex = -1;
            mYooHasUnsavedChanges = false;

            CacheYooDataState();
            RefreshYooUnsavedLabel();
            RefreshYooPackageDropdown();
            RefreshYooPackageSettingsPanel();
            RefreshYooGlobalSettingsPanel();
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
            RefreshYooRemovePackageButton();
            ClearYooAssetPreview();
        }
    }
}
#endif
