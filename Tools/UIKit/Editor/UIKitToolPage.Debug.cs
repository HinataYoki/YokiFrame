using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// UIKitToolPage - 调试功能
    /// 采用响应式数据绑定，通过订阅面板事件实现自动更新
    /// </summary>
    public partial class UIKitToolPage
    {
        #region 字段 - 调试

        private bool mShowActivePanels = true;
        private bool mShowStackInfo = true;
        private bool mShowFocusInfo = true;
        private bool mShowCacheInfo;
        private bool mDebugAutoRefresh = true;
        private VisualElement mDebugContent;

        #endregion

        #region 调试 UI

        private void BuildDebugUI(VisualElement container)
        {
            // 工具栏
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.paddingTop = 4;
            toolbar.style.paddingBottom = 4;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            container.Add(toolbar);

            // 过滤按钮
            toolbar.Add(CreateDebugToggleButton("活动面板", mShowActivePanels, v => { mShowActivePanels = v; RefreshDebugContent(); }));
            toolbar.Add(CreateDebugToggleButton("堆栈", mShowStackInfo, v => { mShowStackInfo = v; RefreshDebugContent(); }));
            toolbar.Add(CreateDebugToggleButton("焦点", mShowFocusInfo, v => { mShowFocusInfo = v; RefreshDebugContent(); }));
            toolbar.Add(CreateDebugToggleButton("缓存", mShowCacheInfo, v => { mShowCacheInfo = v; RefreshDebugContent(); }));

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            // 响应式提示
            var reactiveIcon = new Image { image = KitIcons.GetTexture(KitIcons.REFRESH) };
            reactiveIcon.style.width = 14;
            reactiveIcon.style.height = 14;
            reactiveIcon.style.marginRight = 8;
            reactiveIcon.tintColor = new Color(0.3f, 0.9f, 0.4f);
            reactiveIcon.tooltip = "响应式更新";
            toolbar.Add(reactiveIcon);

            // 自动刷新
            var autoRefreshToggle = YokiFrameUIComponents.CreateModernToggle("自动刷新", mDebugAutoRefresh, v => mDebugAutoRefresh = v);
            autoRefreshToggle.style.marginRight = 8;
            toolbar.Add(autoRefreshToggle);

            var refreshBtn = new Button(RefreshDebugContent) { text = "刷新" };
            refreshBtn.style.height = 24;
            toolbar.Add(refreshBtn);

            // 内容区域
            mDebugContent = new ScrollView();
            mDebugContent.style.flexGrow = 1;
            mDebugContent.style.paddingLeft = 12;
            mDebugContent.style.paddingRight = 12;
            mDebugContent.style.paddingTop = 12;
            container.Add(mDebugContent);

            RefreshDebugContent();
        }

        private Button CreateDebugToggleButton(string text, bool initialValue, Action<bool> onChanged)
        {
            var btn = new Button();
            btn.text = text;
            btn.style.height = 24;
            btn.style.marginRight = 4;

            void UpdateStyle(bool isActive)
            {
                btn.style.backgroundColor = new StyleColor(isActive ? new Color(0.25f, 0.55f, 0.90f) : new Color(0.2f, 0.2f, 0.2f));
                btn.style.color = new StyleColor(isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f));
            }

            UpdateStyle(initialValue);
            bool currentValue = initialValue;

            btn.clicked += () =>
            {
                currentValue = !currentValue;
                UpdateStyle(currentValue);
                onChanged?.Invoke(currentValue);
            };

            return btn;
        }

        private void RefreshDebugContent()
        {
            if (mDebugContent == null) return;
            mDebugContent.Clear();

            if (!EditorApplication.isPlaying)
            {
                var helpBox = YokiFrameUIComponents.CreateHelpBox("请在播放模式下使用调试功能");
                mDebugContent.Add(helpBox);
                return;
            }

            if (mShowActivePanels) DrawDebugActivePanels();
            if (mShowStackInfo) DrawDebugStackInfo();
            if (mShowFocusInfo) DrawDebugFocusInfo();
            if (mShowCacheInfo) DrawDebugCacheInfo();
        }

        #endregion

        #region 调试内容绘制

        private void DrawDebugActivePanels()
        {
            var (card, body) = YokiFrameUIComponents.CreateCard("活动面板", KitIcons.CLIPBOARD);
            mDebugContent.Add(card);

            var panels = GetActivePanels();
            if (panels.Count == 0)
            {
                body.Add(new Label("无活动面板") { style = { color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)) } });
                return;
            }

            foreach (var panel in panels)
            {
                var row = CreateDebugPanelRow(panel);
                body.Add(row);
            }
        }

        private VisualElement CreateDebugPanelRow(IPanel panel)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));

            var panelName = panel.GetType().Name;
            var state = panel.State.ToString();
            var level = panel.Handler?.Level.ToString() ?? "Unknown";

            row.Add(new Label(panelName) { style = { width = 150 } });
            row.Add(new Label($"[{state}]") { style = { width = 60, color = new StyleColor(GetStateColor(panel.State)) } });
            row.Add(new Label($"Level: {level}") { style = { width = 100 } });

            row.Add(new VisualElement { style = { flexGrow = 1 } });

            // 操作按钮
            if (panel.State == PanelState.Open)
            {
                row.Add(CreateSmallButton("隐藏", () => panel.Hide()));
                row.Add(CreateSmallButton("关闭", () => panel.Close()));
            }
            else if (panel.State == PanelState.Hide)
            {
                row.Add(CreateSmallButton("显示", () => panel.Show()));
                row.Add(CreateSmallButton("关闭", () => panel.Close()));
            }

            if (panel is MonoBehaviour mb && mb != null)
            {
                row.Add(CreateSmallButton("选择", () =>
                {
                    Selection.activeGameObject = mb.gameObject;
                    EditorGUIUtility.PingObject(mb.gameObject);
                }));
            }

            return row;
        }

        private void DrawDebugStackInfo()
        {
            var (card, body) = YokiFrameUIComponents.CreateCard("面板堆栈", KitIcons.STACK);
            mDebugContent.Add(card);

            var stackNames = UIKit.GetAllStackNames();
            if (stackNames.Count == 0)
            {
                body.Add(new Label("无堆栈") { style = { color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)) } });
                return;
            }

            foreach (var stackName in stackNames)
            {
                var depth = UIKit.GetStackDepth(stackName);
                var topPanel = UIKit.PeekPanel(stackName);
                var topPanelName = topPanel?.GetType().Name ?? "空";

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 4;
                row.style.paddingBottom = 4;

                row.Add(new Label($"栈 [{stackName}]") { style = { width = 100 } });
                row.Add(new Label($"深度: {depth}") { style = { width = 60 } });
                row.Add(new Label($"栈顶: {topPanelName}") { style = { flexGrow = 1 } });

                if (depth > 0)
                {
                    row.Add(CreateSmallButton("Pop", () => UIKit.PopPanel(stackName)));
                    row.Add(CreateSmallButton("清空", () => UIKit.ClearStack(stackName)));
                }

                body.Add(row);
            }
        }

        private void DrawDebugFocusInfo()
        {
            var (card, body) = YokiFrameUIComponents.CreateCard("焦点信息", KitIcons.TARGET);
            mDebugContent.Add(card);

            var focusSystem = UIFocusSystem.Instance;
            if (focusSystem == null)
            {
                body.Add(new Label("焦点系统未初始化") { style = { color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)) } });
                return;
            }

            var currentFocus = focusSystem.CurrentFocus;
            var focusName = currentFocus != null ? currentFocus.name : "无";
            var inputMode = focusSystem.CurrentInputMode.ToString();

            var (focusRow, _) = YokiFrameUIComponents.CreateInfoRow("当前焦点:", focusName);
            body.Add(focusRow);

            var (modeRow, _) = YokiFrameUIComponents.CreateInfoRow("输入模式:", inputMode);
            body.Add(modeRow);

            if (currentFocus != null)
            {
                var btnRow = new VisualElement();
                btnRow.style.flexDirection = FlexDirection.Row;
                btnRow.style.justifyContent = Justify.FlexEnd;
                btnRow.style.marginTop = 8;

                btnRow.Add(CreateSmallButton("选择焦点对象", () =>
                {
                    Selection.activeGameObject = currentFocus.gameObject;
                    EditorGUIUtility.PingObject(currentFocus.gameObject);
                }));

                body.Add(btnRow);
            }
        }

        private void DrawDebugCacheInfo()
        {
            var (card, body) = YokiFrameUIComponents.CreateCard("缓存信息", KitIcons.CACHE);
            mDebugContent.Add(card);

            var cachedPanels = UIKit.GetCachedPanels();
            var cacheCount = cachedPanels.Count;
            var maxCapacity = UIKit.GetCacheCapacity();

            var (countRow, _) = YokiFrameUIComponents.CreateInfoRow("缓存数量:", $"{cacheCount} / {maxCapacity}");
            body.Add(countRow);

            if (cacheCount == 0)
            {
                body.Add(new Label("无缓存面板") { style = { color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)), marginTop = 8 } });
                return;
            }

            foreach (var panel in cachedPanels)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;

                row.Add(new Label($"  - {panel.GetType().Name}") { style = { flexGrow = 1 } });

                if (panel is MonoBehaviour mb && mb != null)
                {
                    row.Add(CreateSmallButton("选择", () => Selection.activeGameObject = mb.gameObject));
                }

                body.Add(row);
            }

            var clearBtn = YokiFrameUIComponents.CreateDangerButton("清空缓存", () => UIKit.ClearAllPreloadedCache());
            clearBtn.style.marginTop = 8;
            body.Add(clearBtn);
        }

        #endregion

        #region 调试辅助方法

        private List<IPanel> GetActivePanels()
        {
            var result = new List<IPanel>();

            if (UIRoot.Instance == null) return result;

            var panels = UIRoot.Instance.GetComponentsInChildren<UIPanel>(true);
            foreach (var panel in panels)
            {
                if (panel.State != PanelState.Close)
                {
                    result.Add(panel);
                }
            }

            return result;
        }

        private static Color GetStateColor(PanelState state) => state switch
        {
            PanelState.Open => new Color(0.3f, 0.8f, 0.4f),
            PanelState.Hide => new Color(0.8f, 0.6f, 0.2f),
            PanelState.Close => new Color(0.5f, 0.5f, 0.5f),
            _ => Color.white
        };

        #endregion
    }
}
