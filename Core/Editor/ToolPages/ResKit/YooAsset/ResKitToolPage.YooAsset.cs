#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - YooAsset Collector 仪表盘
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

        /// <summary>SO 文件上次修改时间戳（用于检测外部变更）</summary>
        private long mYooSettingLastWriteTime;

        /// <summary>SO 文件路径缓存</summary>
        private string mYooSettingAssetPath;

        /// <summary>是否已注册 Undo 回调</summary>
        private bool mYooUndoCallbackRegistered;

        /// <summary>上次检测到的包数量（用于检测内存数据变化）</summary>
        private int mYooLastPackageCount;

        /// <summary>上次检测到的分组数量（用于检测内存数据变化）</summary>
        private int mYooLastGroupCount;

        /// <summary>上次检测到的收集器数量（用于检测内存数据变化）</summary>
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
        /// 构建 YooAsset Collector 仪表盘界面
        /// </summary>
        private void BuildYooAssetUI(VisualElement root)
        {
            // 工具栏
            mYooToolbar = BuildYooToolbar();
            root.Add(mYooToolbar);

            // 全局设置面板（默认折叠）
            mYooGlobalSettingsPanel = BuildYooGlobalSettingsPanel();
            mYooGlobalSettingsPanel.style.display = DisplayStyle.None;
            root.Add(mYooGlobalSettingsPanel);

            // 包设置面板（默认折叠）
            mYooPackageSettingsPanel = BuildYooPackageSettingsPanel();
            mYooPackageSettingsPanel.style.display = DisplayStyle.None;
            root.Add(mYooPackageSettingsPanel);

            // 主分割视图（左侧分组导航 | 右侧内容区）
            var mainSplitView = CreateSplitView(200f);
            mainSplitView.style.flexGrow = 1;
            root.Add(mainSplitView);

            // 左侧分组导航
            mYooGroupNavContainer = BuildYooGroupNav();
            mainSplitView.Add(mYooGroupNavContainer);

            // 右侧内容区（收集器画布 | 资源预览）
            var rightSplitView = CreateSplitView(400f);
            rightSplitView.style.flexGrow = 1;
            mainSplitView.Add(rightSplitView);

            // 收集器画布
            mYooCollectorCanvas = BuildYooCollectorCanvas();
            rightSplitView.Add(mYooCollectorCanvas);

            // 资源预览面板
            mYooAssetPreviewPanel = BuildYooAssetPreviewPanel();
            rightSplitView.Add(mYooAssetPreviewPanel);

            // 初始化数据
            InitYooData();
        }

        /// <summary>
        /// 初始化 YooAsset 数据
        /// </summary>
        private void InitYooData()
        {
            // 确保配置文件存在
            var setting = YooSetting;
            if (setting == default)
            {
                UnityEngine.Debug.LogError("[ResKit] 无法加载 YooAsset 配置文件");
                return;
            }

            // 缓存 SO 文件路径和修改时间
            CacheYooSettingFileInfo();

            // 注册 Undo 回调（用于检测其他编辑器的修改）
            RegisterYooUndoCallback();

            // 如果没有任何资源包，创建默认包
            if (setting.Packages == default || setting.Packages.Count == 0)
            {
                YooAsset.Editor.AssetBundleCollectorSettingData.CreatePackage("DefaultPackage");
                YooAsset.Editor.AssetBundleCollectorSettingData.SaveFile();
                UnityEngine.Debug.Log("[ResKit] 已创建默认资源包 'DefaultPackage'");
            }

            // 初始化选中状态
            mYooSelectedPackageIndex = 0;
            mYooSelectedGroupIndex = 0;
            mYooHasUnsavedChanges = false;
            mYooGlobalSettingsExpanded = false;
            mYooPackageSettingsExpanded = false;
            mYooEditingGroupIndex = -1;

            // 缓存当前数据状态
            CacheYooDataState();

            // 刷新 UI
            RefreshYooPackageDropdown();
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
            RefreshYooRemovePackageButton();
        }

        /// <summary>
        /// 注册 Undo 回调
        /// </summary>
        private void RegisterYooUndoCallback()
        {
            if (mYooUndoCallbackRegistered)
                return;

            Undo.undoRedoPerformed += OnYooUndoRedoPerformed;
            mYooUndoCallbackRegistered = true;
        }

        /// <summary>
        /// 注销 Undo 回调
        /// </summary>
        private void UnregisterYooUndoCallback()
        {
            if (!mYooUndoCallbackRegistered)
                return;

            Undo.undoRedoPerformed -= OnYooUndoRedoPerformed;
            mYooUndoCallbackRegistered = false;
        }

        /// <summary>
        /// Undo/Redo 执行后的回调
        /// </summary>
        private void OnYooUndoRedoPerformed()
        {
            // 延迟刷新，确保 Undo 操作完成
            EditorApplication.delayCall += () =>
            {
                if (YooSetting == default)
                    return;

                ManualRefreshYooUI();
            };
        }

        /// <summary>
        /// 缓存当前数据状态（用于检测内存数据变化）
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
        /// 检测内存数据是否变化
        /// </summary>
        private bool CheckYooMemoryDataChange()
        {
            if (YooSetting == default || YooSetting.Packages == default)
                return false;

            // 检测包数量变化
            if (YooSetting.Packages.Count != mYooLastPackageCount)
                return true;

            // 检测分组数量变化
            var package = YooCurrentPackage;
            int currentGroupCount = package?.Groups?.Count ?? 0;
            if (currentGroupCount != mYooLastGroupCount)
                return true;

            // 检测收集器数量变化
            var group = YooCurrentGroup;
            int currentCollectorCount = group?.Collectors?.Count ?? 0;
            if (currentCollectorCount != mYooLastCollectorCount)
                return true;

            return false;
        }

        /// <summary>
        /// 缓存 SO 文件信息（路径和修改时间）
        /// </summary>
        private void CacheYooSettingFileInfo()
        {
            var setting = YooSetting;
            if (setting == default)
                return;

            mYooSettingAssetPath = AssetDatabase.GetAssetPath(setting);
            if (string.IsNullOrEmpty(mYooSettingAssetPath))
                return;

            var fullPath = System.IO.Path.GetFullPath(mYooSettingAssetPath);
            if (System.IO.File.Exists(fullPath))
            {
                mYooSettingLastWriteTime = System.IO.File.GetLastWriteTimeUtc(fullPath).Ticks;
            }
        }

        /// <summary>
        /// 检测 SO 文件是否被外部修改
        /// </summary>
        private bool CheckYooSettingExternalChange()
        {
            if (string.IsNullOrEmpty(mYooSettingAssetPath))
                return false;

            var fullPath = System.IO.Path.GetFullPath(mYooSettingAssetPath);
            if (!System.IO.File.Exists(fullPath))
                return false;

            var currentWriteTime = System.IO.File.GetLastWriteTimeUtc(fullPath).Ticks;
            if (currentWriteTime != mYooSettingLastWriteTime)
            {
                mYooSettingLastWriteTime = currentWriteTime;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 刷新所有 YooAsset UI（外部变更时调用）
        /// </summary>
        private void RefreshAllYooUI()
        {
            // 强制重新导入 SO 文件，使 Unity 重新加载数据
            if (!string.IsNullOrEmpty(mYooSettingAssetPath))
            {
                AssetDatabase.ImportAsset(mYooSettingAssetPath, ImportAssetOptions.ForceUpdate);
            }

            // 验证选中索引有效性
            if (YooSetting == default || YooSetting.Packages == default)
                return;

            if (mYooSelectedPackageIndex >= YooSetting.Packages.Count)
                mYooSelectedPackageIndex = YooSetting.Packages.Count > 0 ? YooSetting.Packages.Count - 1 : 0;

            var package = YooCurrentPackage;
            if (package != default && package.Groups != default && mYooSelectedGroupIndex >= package.Groups.Count)
                mYooSelectedGroupIndex = package.Groups.Count > 0 ? package.Groups.Count - 1 : 0;

            // 重置展开状态
            mYooExpandedCardIndex = -1;

            // 更新数据状态缓存
            CacheYooDataState();

            // 刷新所有 UI
            RefreshYooPackageDropdown();
            RefreshYooPackageSettingsPanel();
            RefreshYooGlobalSettingsPanel();
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
            RefreshYooRemovePackageButton();
        }

        /// <summary>
        /// YooAsset 标签页的 Update 检测（在 OnUpdate 中调用）
        /// </summary>
        private void OnYooAssetUpdate()
        {
            // 检测 SO 文件外部变更（磁盘文件修改）
            if (CheckYooSettingExternalChange())
            {
                RefreshAllYooUI();
                return;
            }

            // 检测内存数据变化（其他编辑器修改）
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
        /// 手动刷新 YooAsset UI（供工具栏按钮调用）
        /// </summary>
        private void ManualRefreshYooUI()
        {
            // 强制重新导入 SO 文件，使 Unity 重新加载数据
            if (!string.IsNullOrEmpty(mYooSettingAssetPath))
            {
                AssetDatabase.ImportAsset(mYooSettingAssetPath, ImportAssetOptions.ForceUpdate);
            }

            // 更新文件时间戳
            CacheYooSettingFileInfo();

            // 验证选中索引有效性
            if (YooSetting == default || YooSetting.Packages == default)
                return;

            if (mYooSelectedPackageIndex >= YooSetting.Packages.Count)
                mYooSelectedPackageIndex = YooSetting.Packages.Count > 0 ? YooSetting.Packages.Count - 1 : 0;

            var package = YooCurrentPackage;
            if (package != default && package.Groups != default && mYooSelectedGroupIndex >= package.Groups.Count)
                mYooSelectedGroupIndex = package.Groups.Count > 0 ? package.Groups.Count - 1 : 0;

            // 重置展开状态
            mYooExpandedCardIndex = -1;

            // 更新数据状态缓存
            CacheYooDataState();

            // 刷新所有 UI
            RefreshYooPackageDropdown();
            RefreshYooPackageSettingsPanel();
            RefreshYooGlobalSettingsPanel();
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
            RefreshYooRemovePackageButton();
        }
    }
}
#endif
