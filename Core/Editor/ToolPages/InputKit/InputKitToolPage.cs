#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 工具页面
    /// 提供输入系统配置、绑定管理、运行时调试功能
    /// </summary>
    public partial class InputKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "InputKit";
        public override string PageIcon => KitIcons.INPUTKIT;
        public override int Priority => 30;

        #region 常量

        private const float THROTTLE_INTERVAL = 0.1f;
        private const string SPLIT_WIDTH_PREF_KEY = "YokiFrame.InputKit.SplitWidth";
        private const float DEFAULT_SPLIT_WIDTH = 280f;

        #endregion

        #region 字段

        // UI 元素引用
        private TwoPaneSplitView mSplitView;
        private VisualElement mLeftPanel;
        private VisualElement mRightPanel;
        private ListView mActionMapListView;
        private ListView mActionListView;

        // 数据缓存
        private readonly List<string> mActionMaps = new(8);
        private readonly List<ActionInfo> mActions = new(16);
        private string mSelectedActionMap;
        private ActionInfo mSelectedAction;

        // 节流器
        private Throttle mRefreshThrottle;

        #endregion

        #region 数据结构

        private struct ActionInfo
        {
            public string Name;
            public string BindingDisplay;
            public bool IsComposite;
        }

        #endregion

        #region BuildUI

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = BuildToolbar();
            root.Add(toolbar);

            // 内容区域
            var content = new VisualElement();
            content.AddToClassList("content-area");
            root.Add(content);

            // 左右分割面板
            mSplitView = CreateSplitView(DEFAULT_SPLIT_WIDTH, SPLIT_WIDTH_PREF_KEY);
            content.Add(mSplitView);

            // 左侧：ActionMap 列表
            mLeftPanel = BuildLeftPanel();
            mSplitView.Add(mLeftPanel);

            // 右侧：Action 详情
            mRightPanel = BuildRightPanel();
            mSplitView.Add(mRightPanel);

            // 初始状态
            UpdateEmptyState();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = YokiFrameUIComponents.CreateToolbar();

            var helpLabel = new Label("输入系统配置与调试");
            helpLabel.AddToClassList("toolbar-label");
            toolbar.Add(helpLabel);

            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            // 刷新按钮
            var refreshBtn = CreateToolbarButton("刷新", RefreshAll);
            toolbar.Add(refreshBtn);

            // 重置绑定按钮
            var resetBtn = CreateToolbarButton("重置绑定", OnResetAllBindings);
            toolbar.Add(resetBtn);

            return toolbar;
        }

        private VisualElement BuildLeftPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("left-panel");

            // 标题
            var header = CreatePanelHeader("ActionMaps");
            panel.Add(header);

            // ActionMap 列表
            mActionMapListView = new ListView
            {
                fixedItemHeight = 32f,
                selectionType = SelectionType.Single,
                makeItem = MakeActionMapItem,
                bindItem = BindActionMapItem
            };
            mActionMapListView.AddToClassList("action-map-list");
            mActionMapListView.selectionChanged += OnActionMapSelectionChanged;
            panel.Add(mActionMapListView);

            return panel;
        }

        private VisualElement BuildRightPanel()
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.flexDirection = FlexDirection.Column;

            // 标题
            var header = CreatePanelHeader("Actions");
            panel.Add(header);

            // Action 列表
            mActionListView = new ListView
            {
                fixedItemHeight = 48f,
                selectionType = SelectionType.Single,
                makeItem = MakeActionItem,
                bindItem = BindActionItem
            };
            mActionListView.AddToClassList("action-list");
            mActionListView.selectionChanged += OnActionSelectionChanged;
            panel.Add(mActionListView);

            // 详情面板
            var detailPanel = BuildDetailPanel();
            panel.Add(detailPanel);

            return panel;
        }

        private VisualElement BuildDetailPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("detail-panel");
            panel.name = "detail-panel";

            var emptyState = CreateEmptyState(KitIcons.INFO, "选择一个 Action 查看详情");
            panel.Add(emptyState);

            return panel;
        }

        #endregion

        #region 列表项创建

        private VisualElement MakeActionMapItem() => new Label { name = "action-map-label" };

        private void BindActionMapItem(VisualElement element, int index)
        {
            if (index < 0 || index >= mActionMaps.Count) return;
            var label = element as Label;
            if (label != default) label.text = mActionMaps[index];
        }

        private VisualElement MakeActionItem()
        {
            var container = new VisualElement();
            container.AddToClassList("action-item");

            var nameLabel = new Label { name = "action-name" };
            nameLabel.AddToClassList("action-name");
            container.Add(nameLabel);

            var bindingLabel = new Label { name = "action-binding" };
            bindingLabel.AddToClassList("action-binding");
            container.Add(bindingLabel);

            return container;
        }

        private void BindActionItem(VisualElement element, int index)
        {
            if (index < 0 || index >= mActions.Count) return;

            var action = mActions[index];
            var nameLabel = element.Q<Label>("action-name");
            var bindingLabel = element.Q<Label>("action-binding");

            if (nameLabel != default) nameLabel.text = action.Name;
            if (bindingLabel != default) bindingLabel.text = action.BindingDisplay;
        }

        #endregion

        #region 生命周期

        public override void OnActivate()
        {
            base.OnActivate();
            mRefreshThrottle = CreateThrottle(THROTTLE_INTERVAL);
            RefreshAll();
        }

        public override void OnDeactivate()
        {
            mSelectedActionMap = default;
            mSelectedAction = default;
            base.OnDeactivate();
        }

        protected override void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                RefreshAll();
            }
        }

        #endregion

        #region 事件处理

        private void OnActionMapSelectionChanged(IEnumerable<object> selection)
        {
            mSelectedActionMap = default;
            foreach (var item in selection)
            {
                mSelectedActionMap = item as string;
                break;
            }
            RefreshActionList();
        }

        private void OnActionSelectionChanged(IEnumerable<object> selection)
        {
            mSelectedAction = default;
            foreach (var item in selection)
            {
                if (item is ActionInfo info)
                {
                    mSelectedAction = info;
                    break;
                }
            }
            UpdateDetailPanel();
        }

        private void OnResetAllBindings()
        {
            if (!EditorUtility.DisplayDialog("重置绑定", "确定要重置所有绑定到默认值吗？", "确定", "取消"))
                return;

#if ENABLE_INPUT_SYSTEM
            if (InputKit.IsInitialized)
            {
                InputKit.ResetAllBindings();
                RefreshAll();
            }
#endif
        }

        #endregion

        #region 刷新方法

        private void RefreshAll()
        {
            RefreshActionMapList();
            RefreshActionList();
            UpdateDetailPanel();
        }

        private void RefreshActionMapList()
        {
            mActionMaps.Clear();

#if ENABLE_INPUT_SYSTEM
            // 优先使用运行时已注册的 Asset
            var asset = InputKit.Asset;
            
            // 编辑器模式下，如果运行时未初始化，则扫描项目中的 InputActionAsset
            if (asset == default && !EditorApplication.isPlaying)
            {
                asset = FindFirstInputActionAsset();
            }
            
            if (asset != default)
            {
                foreach (var map in asset.actionMaps)
                {
                    mActionMaps.Add(map.name);
                }
            }
#endif

            mActionMapListView.itemsSource = mActionMaps;
            mActionMapListView.RefreshItems();
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// 在编辑器模式下查找项目中的第一个 InputActionAsset
        /// </summary>
        private static InputActionAsset FindFirstInputActionAsset()
        {
            // 仅搜索 Assets 目录，排除 Packages
            var guids = AssetDatabase.FindAssets("t:InputActionAsset", new[] { "Assets" });
            if (guids.Length == 0) return default;
            
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }
#endif

        private void RefreshActionList()
        {
            mActions.Clear();

#if ENABLE_INPUT_SYSTEM
            if (string.IsNullOrEmpty(mSelectedActionMap)) 
            {
                mActionListView.itemsSource = mActions;
                mActionListView.RefreshItems();
                return;
            }

            // 优先使用运行时已注册的 Asset
            var asset = InputKit.Asset;
            
            // 编辑器模式下，如果运行时未初始化，则扫描项目中的 InputActionAsset
            if (asset == default && !EditorApplication.isPlaying)
            {
                asset = FindFirstInputActionAsset();
            }
            
            if (asset == default)
            {
                mActionListView.itemsSource = mActions;
                mActionListView.RefreshItems();
                return;
            }

            var actionMap = asset.FindActionMap(mSelectedActionMap);
            if (actionMap == default)
            {
                mActionListView.itemsSource = mActions;
                mActionListView.RefreshItems();
                return;
            }

            foreach (var action in actionMap.actions)
            {
                mActions.Add(new ActionInfo
                {
                    Name = action.name,
                    BindingDisplay = action.GetBindingDisplayString(),
                    IsComposite = action.bindings.Count > 1
                });
            }
#endif

            mActionListView.itemsSource = mActions;
            mActionListView.RefreshItems();
        }

        private void UpdateDetailPanel()
        {
            var detailPanel = Root.Q("detail-panel");
            if (detailPanel == default) return;

            detailPanel.Clear();

            if (string.IsNullOrEmpty(mSelectedAction.Name))
            {
                var emptyState = CreateEmptyState(KitIcons.INFO, "选择一个 Action 查看详情");
                detailPanel.Add(emptyState);
                return;
            }

            BuildActionDetail(detailPanel, mSelectedAction);
        }

        private void BuildActionDetail(VisualElement container, ActionInfo action)
        {
            var (card, body) = CreateCard($"Action: {action.Name}");
            container.Add(card);

            var (nameRow, _) = CreateInfoRow("名称", action.Name);
            body.Add(nameRow);

            var (bindingRow, _) = CreateInfoRow("绑定", action.BindingDisplay);
            body.Add(bindingRow);

            var (compositeRow, _) = CreateInfoRow("复合绑定", action.IsComposite ? "是" : "否");
            body.Add(compositeRow);

#if ENABLE_INPUT_SYSTEM
            // 重绑定按钮
            var rebindBtn = CreatePrimaryButton("重新绑定", () => StartRebind(action.Name));
            body.Add(rebindBtn);

            // 重置按钮
            var resetBtn = CreateSecondaryButton("重置此 Action", () => ResetActionBinding(action.Name));
            body.Add(resetBtn);
#endif
        }

        private void UpdateEmptyState()
        {
            if (mActionMaps.Count == 0)
            {
                var emptyState = CreateEmptyState(
                    KitIcons.WARNING,
                    "未检测到 InputActionAsset",
                    "请先调用 InputKit.Register<T>() 注册输入类");
                mLeftPanel.Add(emptyState);
            }
        }

#if ENABLE_INPUT_SYSTEM
        private async void StartRebind(string actionName)
        {
            var action = InputKit.FindAction(actionName);
            if (action == default)
            {
                UnityEngine.Debug.LogWarning($"[InputKit] 找不到 Action: {actionName}");
                return;
            }

            var success = await InputKit.RebindAsync(action);
            if (success)
            {
                RefreshActionList();
                UpdateDetailPanel();
            }
        }

        private void ResetActionBinding(string actionName)
        {
            var action = InputKit.FindAction(actionName);
            if (action == default) return;

            InputKit.ResetActionBindings(action);
            RefreshActionList();
            UpdateDetailPanel();
        }
#endif

        #endregion
    }
}
#endif
