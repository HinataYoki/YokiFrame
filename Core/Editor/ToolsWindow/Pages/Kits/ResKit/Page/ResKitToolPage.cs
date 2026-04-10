#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 资源监控页。
    /// 提供资源监控主工作区，并在启用 YooAsset 支持时切换到采集器页。
    /// </summary>
    [YokiToolPage(
        kit: "ResKit",
        name: "ResKit",
        icon: KitIcons.RESKIT,
        priority: 35,
        category: YokiPageCategory.Tool)]
    public partial class ResKitToolPage : YokiToolPageBase
    {
        private const float THROTTLE_INTERVAL = 0.5f;

        private enum WorkspaceTab
        {
            ResourceMonitor,
#if YOKIFRAME_YOOASSET_SUPPORT
            YooAssetCollector
#endif
        }

#if YOKIFRAME_YOOASSET_SUPPORT
        private YokiFrameUIComponents.TabView mTabView;
#endif

        private WorkspaceTab mCurrentTab;
        private VisualElement mStatusContainer;
        private VisualElement mMonitorContent;

#if YOKIFRAME_YOOASSET_SUPPORT
        private VisualElement mYooAssetContent;
#endif

        private TextField mSearchField;
        private VisualElement mCategoryContainer;
        private Label mLoadedCountLabel;
        private Label mTotalRefCountLabel;
        private Label mDetailPath;
        private Label mDetailType;
        private Label mDetailRefCount;
        private Label mDetailStatus;
        private Label mDetailSource;
        private VisualElement mDetailPanel;

        private VisualElement mHistoryContainer;
        private Label mHistoryCountLabel;

        private bool mAutoRefresh = true;
        private string mSearchFilter = string.Empty;

        private ResKitViewModel mViewModel;
        private Throttle mRefreshThrottle;

        private readonly List<ResDebugger.ResInfo> mAllAssets = new();
        private readonly Dictionary<string, CategoryPanel> mCategoryPanels = new();
        private ResDebugger.ResInfo? mSelectedAsset;

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

        /// <summary>
        /// 构建 ResKit 统一工作台入口。
        /// </summary>
        protected override void BuildUI(VisualElement root)
        {
            var scaffold = CreateKitPageScaffold(
                "ResKit",
                "集中查看运行时资源占用、引用计数、卸载历史，并在启用 YooAsset 支持时进入采集器工作区。",
                KitIcons.RESKIT,
                "资源工作台");
            root.Add(scaffold.Root);
            scaffold.Toolbar.style.display = DisplayStyle.None;

            mStatusContainer = scaffold.StatusBar;

#if YOKIFRAME_YOOASSET_SUPPORT
            mMonitorContent = new VisualElement { style = { flexGrow = 1 } };
            BuildResourceMonitorUI(mMonitorContent);

            mYooAssetContent = new VisualElement { style = { flexGrow = 1 } };
            BuildYooAssetUI(mYooAssetContent);

            mTabView = YokiFrameUIComponents.CreateTabView(
                ("资源监控", mMonitorContent),
                ("YooAsset 采集器", mYooAssetContent));
            mTabView.OnTabChanged += OnWorkspaceTabChanged;
            mTabView.Root.Insert(1, scaffold.StatusBar);
            scaffold.Content.Add(mTabView.Root);
#else
            mMonitorContent = new VisualElement { style = { flexGrow = 1 } };
            BuildResourceMonitorUI(mMonitorContent);
            scaffold.Content.Add(mMonitorContent);
#endif

            SwitchWorkspace(WorkspaceTab.ResourceMonitor);
        }

        /// <summary>
        /// 切换 ResKit 工作区。
        /// </summary>
        private void SwitchWorkspace(WorkspaceTab tab)
        {
            if (mCurrentTab == tab)
            {
                return;
            }

            mCurrentTab = tab;
            RefreshStatusBanner();

#if YOKIFRAME_YOOASSET_SUPPORT
            int targetIndex = tab == WorkspaceTab.ResourceMonitor ? 0 : 1;
            if (mTabView != null && mTabView.SelectedIndex != targetIndex)
            {
                mTabView.SwitchTo(targetIndex);
            }
#endif
        }

#if YOKIFRAME_YOOASSET_SUPPORT
        private void OnWorkspaceTabChanged(int index)
        {
            var nextTab = index == 0 ? WorkspaceTab.ResourceMonitor : WorkspaceTab.YooAssetCollector;
            if (mCurrentTab == nextTab)
            {
                return;
            }

            mCurrentTab = nextTab;
            RefreshStatusBanner();
        }
#endif

        /// <summary>
        /// 根据当前工作区刷新顶部状态说明。
        /// </summary>
        private void RefreshStatusBanner()
        {
            if (mStatusContainer == null)
            {
                return;
            }

            if (mCurrentTab == WorkspaceTab.ResourceMonitor)
            {
                string message = EditorApplication.isPlaying
                    ? "资源监控已连接运行时资源快照与卸载历史，支持自动刷新与分类查看。"
                    : "资源监控建议在 PlayMode 下查看实时资源状态，当前仍可预览布局与工具操作。";

                SetStatusBanner(mStatusContainer, "资源监控", message);
                return;
            }

#if YOKIFRAME_YOOASSET_SUPPORT
            SetStatusBanner(
                mStatusContainer,
                "YooAsset 采集器",
                "采集器页用于管理资源包、分组与收集规则，不依赖 PlayMode。");
#endif
        }

        /// <summary>
        /// 清空详情区的当前显示。
        /// </summary>
        private void ShowEmptyState()
        {
            mDetailPath.text = "-";
            mDetailType.text = "-";
            mDetailRefCount.text = "-";
            mDetailStatus.text = "-";
            mDetailSource.text = "-";
        }

        /// <summary>
        /// 获取资源类型颜色。
        /// </summary>
        private Color GetTypeColor(string typeName) =>
            sTypeColors.TryGetValue(typeName, out var color) ? color : new Color(0.50f, 0.50f, 0.55f);

        /// <summary>
        /// 获取资源类型徽标样式类名。
        /// </summary>
        private static string GetTypeClass(string typeName) => typeName?.ToLowerInvariant() switch
        {
            "audioclip" => "yoki-res-type--audioclip",
            "texture2d" => "yoki-res-type--texture2d",
            "sprite" => "yoki-res-type--sprite",
            "material" => "yoki-res-type--material",
            "gameobject" => "yoki-res-type--gameobject",
            "textasset" => "yoki-res-type--textasset",
            "scriptableobject" => "yoki-res-type--scriptableobject",
            "shader" => "yoki-res-type--shader",
            "font" => "yoki-res-type--font",
            "animationclip" => "yoki-res-type--animationclip",
            _ => "yoki-res-type--unknown"
        };

        /// <summary>
        /// 选中一条资源记录并刷新详情区。
        /// </summary>
        private void SelectAsset(ResDebugger.ResInfo info)
        {
            mSelectedAsset = info;
            mDetailPath.text = info.Path;
            mDetailType.text = info.TypeName;
            mDetailRefCount.text = info.RefCount.ToString();
            mDetailStatus.text = info.IsDone ? "Loaded" : "Loading";
            mDetailSource.text = info.Source == ResDebugger.ResSource.ResKit ? "ResKit Cache" : "Loader";

            mDetailRefCount.RemoveFromClassList("yoki-res-detail__value--highlight");
            if (info.RefCount > 1)
            {
                mDetailRefCount.AddToClassList("yoki-res-detail__value--highlight");
            }
        }

        /// <summary>
        /// 从路径中提取资源名。
        /// </summary>
        private string GetAssetName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            int lastSlash = path.LastIndexOf('/');
            return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        }

        /// <summary>
        /// 刷新资源快照与摘要计数。
        /// </summary>
        private void RefreshData()
        {
            mAllAssets.Clear();
            mAllAssets.AddRange(ResDebugger.GetLoadedAssets());

            mLoadedCountLabel.text = $"已加载 {ResDebugger.GetLoadedCount()}";
            mTotalRefCountLabel.text = $"总引用 {ResDebugger.GetTotalRefCount()}";

            RefreshCategoryDisplay();
            SyncSelectedAssetDetail();
        }

        /// <summary>
        /// 刷新后重新定位当前选中的资源。
        /// </summary>
        private void SyncSelectedAssetDetail()
        {
            if (mSelectedAsset is not ResDebugger.ResInfo selected)
            {
                ShowEmptyState();
                return;
            }

            for (int i = 0; i < mAllAssets.Count; i++)
            {
                var asset = mAllAssets[i];
                if (asset.Path == selected.Path &&
                    asset.TypeName == selected.TypeName &&
                    asset.Source == selected.Source)
                {
                    SelectAsset(asset);
                    return;
                }
            }

            mSelectedAsset = null;
            ShowEmptyState();
        }

        /// <summary>
        /// 激活页面时建立资源监控订阅。
        /// </summary>
        public override void OnActivate()
        {
            base.OnActivate();
            mViewModel = new ResKitViewModel();
            mRefreshThrottle = CreateThrottle(THROTTLE_INTERVAL);

            SubscribeChannelThrottled<int>(DataChannels.RES_LIST_CHANGED, OnResListChanged, THROTTLE_INTERVAL);
            SubscribeChannelThrottled<ResDebugger.UnloadRecord>(DataChannels.RES_UNLOADED, OnResUnloaded, THROTTLE_INTERVAL);

            RefreshStatusBanner();

            if (IsPlaying)
            {
                RefreshData();
            }
        }

        /// <summary>
        /// 页面停用时释放视图模型。
        /// </summary>
        public override void OnDeactivate()
        {
            mViewModel?.Dispose();
            mViewModel = null;
            base.OnDeactivate();
        }

        /// <summary>
        /// 与 PlayMode 状态保持同步。
        /// </summary>
        protected override void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);
            RefreshStatusBanner();

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                RefreshData();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                mAllAssets.Clear();
                mSelectedAsset = null;
                RefreshCategoryDisplay();
                ShowEmptyState();
            }
        }

        /// <summary>
        /// 处理资源列表变化。
        /// </summary>
        private void OnResListChanged(int count) => mRefreshThrottle.Execute(RefreshData);

        /// <summary>
        /// 处理新的卸载历史记录。
        /// </summary>
        private void OnResUnloaded(ResDebugger.UnloadRecord record) => mRefreshThrottle.Execute(RefreshHistoryDisplay);

        /// <summary>
        /// 保留轮询以兜底检测卸载与 YooAsset 刷新。
        /// </summary>
        [System.Obsolete("Polling is retained only for unload detection and YooAsset tab refresh fallback.")]
        public override void OnUpdate()
        {
#if YOKIFRAME_YOOASSET_SUPPORT
            OnYooAssetUpdate();
#endif

            if (!mAutoRefresh || !EditorApplication.isPlaying)
            {
                return;
            }

            ResDebugger.DetectUnloadedAssets();
        }
    }
}
#endif
