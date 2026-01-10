#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 资源监控（按类型分类 + 搜索）
    /// </summary>
    public partial class ResKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "ResKit";
        public override string PageIcon => KitIcons.RESKIT;
        public override int Priority => 35;

        #region 标签页状态

#if YOKIFRAME_YOOASSET_SUPPORT
        /// <summary>标签页视图组件</summary>
        private YokiFrameUIComponents.TabView mTabView;
#endif

        /// <summary>资源监控内容容器</summary>
        private VisualElement mMonitorContent;

#if YOKIFRAME_YOOASSET_SUPPORT
        /// <summary>YooAsset 收集器内容容器</summary>
        private VisualElement mYooAssetContent;
#endif

        #endregion

        // UI 元素
        private TextField mSearchField;
        private VisualElement mCategoryContainer;
        private Label mLoadedCountLabel;
        private Label mTotalRefCountLabel;
        private VisualElement mDetailPanel;
        private Label mDetailPath;
        private Label mDetailType;
        private Label mDetailRefCount;
        private Label mDetailStatus;
        private Label mDetailSource;
        
        // 卸载历史记录 UI
        private VisualElement mHistoryContainer;
        private Label mHistoryCountLabel;
        
        // 状态
        private bool mAutoRefresh = true;
        private float mLastRefreshTime;
        private const float REFRESH_INTERVAL = 0.5f;
        private string mSearchFilter = "";
        
        // 数据
        private readonly List<ResDebugger.ResInfo> mAllAssets = new();
        private readonly Dictionary<string, CategoryPanel> mCategoryPanels = new();
        private ResDebugger.ResInfo? mSelectedAsset;

        // 类型颜色映射
        private static readonly Dictionary<string, Color> sTypeColors = new()
        {
            { "AudioClip", new Color(0.25f, 0.55f, 0.90f) },
            { "Texture2D", new Color(0.90f, 0.55f, 0.25f) },
            { "Sprite", new Color(0.55f, 0.85f, 0.35f) },
            { "Material", new Color(0.85f, 0.35f, 0.55f) },
            { "GameObject", new Color(0.60f, 0.35f, 0.85f) },
            { "TextAsset", new Color(0.35f, 0.75f, 0.75f) },
            { "ScriptableObject", new Color(0.75f, 0.75f, 0.35f) },
            { "Shader", new Color(0.55f, 0.55f, 0.75f) },
            { "Font", new Color(0.75f, 0.55f, 0.55f) },
            { "AnimationClip", new Color(0.45f, 0.65f, 0.85f) },
        };

        private struct CategoryPanel
        {
            public VisualElement Root;
            public VisualElement Header;
            public Label NameLabel;
            public Label CountLabel;
            public VisualElement ItemsContainer;
            public Button ExpandBtn;
            public Image ExpandIcon;
            public bool IsExpanded;
        }

        protected override void BuildUI(VisualElement root)
        {
#if YOKIFRAME_YOOASSET_SUPPORT
            // 有 YooAsset 支持时，使用统一的标签页视图组件
            mMonitorContent = new VisualElement();
            mMonitorContent.style.flexGrow = 1;
            BuildResourceMonitorUI(mMonitorContent);

            mYooAssetContent = new VisualElement();
            mYooAssetContent.style.flexGrow = 1;
            BuildYooAssetUI(mYooAssetContent);

            // 创建标签页视图
            mTabView = YokiFrameUIComponents.CreateTabView(
                ("资源监控", mMonitorContent),
                ("资源收集", mYooAssetContent)
            );
            root.Add(mTabView.Root);
#else
            BuildResourceMonitorUI(root);
#endif
        }

        private void ShowEmptyState()
        {
            mDetailPath.text = "-";
            mDetailType.text = "-";
            mDetailRefCount.text = "-";
            mDetailStatus.text = "-";
            mDetailSource.text = "-";
        }

        private Color GetTypeColor(string typeName) =>
            sTypeColors.TryGetValue(typeName, out var color) ? color : new Color(0.50f, 0.50f, 0.55f);

        private string GetTypeIcon(string typeName) => typeName switch
        {
            "AudioClip" => "A",
            "Texture2D" => "T",
            "Sprite" => "S",
            "Material" => "M",
            "GameObject" => "G",
            "TextAsset" => "X",
            "ScriptableObject" => "O",
            "Shader" => "H",
            "Font" => "F",
            "AnimationClip" => "C",
            _ => "?"
        };

        private void SelectAsset(ResDebugger.ResInfo info)
        {
            mSelectedAsset = info;
            mDetailPath.text = info.Path;
            mDetailType.text = info.TypeName;
            mDetailRefCount.text = info.RefCount.ToString();
            mDetailStatus.text = info.IsDone ? "已加载" : "加载中";
            mDetailSource.text = info.Source == ResDebugger.ResSource.ResKit 
                ? "ResKit (缓存管理)" 
                : "Loader (直接加载)";

            mDetailRefCount.RemoveFromClassList("highlight");
            if (info.RefCount > 1)
            {
                mDetailRefCount.AddToClassList("highlight");
            }
        }

        private string GetAssetName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var lastSlash = path.LastIndexOf('/');
            return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        }

        private void RefreshData()
        {
            mAllAssets.Clear();
            mAllAssets.AddRange(ResDebugger.GetLoadedAssets());
            
            mLoadedCountLabel.text = $"已加载: {ResDebugger.GetLoadedCount()}";
            mTotalRefCountLabel.text = $"总引用: {ResDebugger.GetTotalRefCount()}";

            RefreshCategoryDisplay();
        }

        public override void OnUpdate()
        {
#if YOKIFRAME_YOOASSET_SUPPORT
            // YooAsset 标签页更新检测
            OnYooAssetUpdate();
#endif

            if (!mAutoRefresh) return;
            if (!EditorApplication.isPlaying) return;

            var now = Time.realtimeSinceStartup;
            if (now - mLastRefreshTime < REFRESH_INTERVAL) return;
            
            mLastRefreshTime = now;
            
            ResDebugger.DetectUnloadedAssets();
            
            RefreshData();
            RefreshHistoryDisplay();
        }
    }
}
#endif
