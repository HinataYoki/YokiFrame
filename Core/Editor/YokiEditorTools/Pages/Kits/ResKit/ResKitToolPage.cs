#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 资源监控（按类型分类 + 搜索）
    /// 使用响应式订阅模式，通过 EditorDataBridge 接收数据变化
    /// </summary>
    [YokiToolPage(
        kit: "ResKit",
        name: "ResKit",
        icon: KitIcons.RESKIT,
        priority: 35,
        category: YokiPageCategory.Tool)]
    public partial class ResKitToolPage : YokiToolPageBase
    {

        #region 常量

        private const float THROTTLE_INTERVAL = 0.5f;

        #endregion

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
        private string mSearchFilter = "";

        // 响应式
        private ResKitViewModel mViewModel;
        private Throttle mRefreshThrottle;

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

        /// <summary>
        /// 获取类型对应的 USS 类名
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

            mDetailRefCount.RemoveFromClassList("yoki-res-detail__value--highlight");
            if (info.RefCount > 1)
            {
                mDetailRefCount.AddToClassList("yoki-res-detail__value--highlight");
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

        #region 生命周期

        public override void OnActivate()
        {
            base.OnActivate();
            mViewModel = new ResKitViewModel();
            mRefreshThrottle = CreateThrottle(THROTTLE_INTERVAL);

            // 订阅事件通道
            SubscribeChannelThrottled<int>(ResDebugger.CHANNEL_RES_LIST_CHANGED, OnResListChanged, THROTTLE_INTERVAL);
            SubscribeChannelThrottled<ResDebugger.UnloadRecord>(ResDebugger.CHANNEL_RES_UNLOADED, OnResUnloaded, THROTTLE_INTERVAL);

            if (IsPlaying) RefreshData();
        }

        public override void OnDeactivate()
        {
            mViewModel?.Dispose();
            mViewModel = null;
            base.OnDeactivate();
        }

        protected override void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);
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

        #endregion

        #region 响应式事件处理

        private void OnResListChanged(int count) => mRefreshThrottle.Execute(RefreshData);

        private void OnResUnloaded(ResDebugger.UnloadRecord record) => mRefreshThrottle.Execute(RefreshHistoryDisplay);

        #endregion

        #region 轮询更新（自动刷新模式）

        /// <summary>
        /// 轮询更新 - 用于检测资源卸载和 YooAsset 标签页更新
        /// 注：资源卸载检测需要主动轮询，无法通过事件通知
        /// </summary>
        [System.Obsolete("保留用于资源卸载检测")]
        public override void OnUpdate()
        {
#if YOKIFRAME_YOOASSET_SUPPORT
            // YooAsset 标签页更新检测
            OnYooAssetUpdate();
#endif

            // 自动刷新模式下仍需要定期检测资源变化
            if (!mAutoRefresh) return;
            if (!EditorApplication.isPlaying) return;

            // 检测卸载的资源（这会触发事件通知）
            ResDebugger.DetectUnloadedAssets();
        }

        #endregion
    }
}
#endif
