using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 工具页面 - 运行时监控（树形结构）
    /// </summary>
    public class ActionKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "ActionKit";
        public override int Priority => 30;

        // UI 元素
        private Label mActiveCountLabel;
        private Label mTotalFinishedLabel;
        private VisualElement mTreeContainer;
        
        // 数据缓存（复用容器）
        private readonly List<IAction> mActiveActions = new(32);
        private readonly List<string> mExecutorNames = new(32);
        
        // 刷新控制
        private bool mAutoRefresh = true;
        private const float REFRESH_INTERVAL = 0.2f;
        private double mLastRefreshTime;
        private double mLastInteractionTime; // 交互保护
        private const float INTERACTION_COOLDOWN = 0.5f; // 点击后暂停刷新的时间

        // 树形展示的展开状态
        private readonly HashSet<ulong> mExpandedNodes = new();
        
        // 选中的 Action（用于显示堆栈）
        private ulong mSelectedActionId;
        private Label mStackTraceLabel;
        
        // 统计
        private int mTotalFinished;
        
        // 堆栈追踪设置
        private bool mClearStackOnExit = true;
        private Label mStackCountLabel;

        protected override void BuildUI(VisualElement root)
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = 16;
            scrollView.style.paddingRight = 16;
            scrollView.style.paddingTop = 16;
            root.Add(scrollView);

            BuildHeader(scrollView);
            BuildStatsCard(scrollView);
            BuildStackTraceSettingsCard(scrollView);
            BuildActiveActionsCard(scrollView);
            BuildStackTraceCard(scrollView);

            RefreshData();
        }

        private void BuildHeader(VisualElement parent)
        {
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 16;
            parent.Add(headerRow);

            var title = new Label("运行时 Action 监控");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            headerRow.Add(title);

            var autoRefreshToggle = new Toggle("自动刷新");
            autoRefreshToggle.value = mAutoRefresh;
            autoRefreshToggle.RegisterValueChangedCallback(evt => mAutoRefresh = evt.newValue);
            headerRow.Add(autoRefreshToggle);

            var refreshBtn = new Button(RefreshData) { text = "刷新" };
            refreshBtn.style.marginLeft = 8;
            headerRow.Add(refreshBtn);

            var expandAllBtn = new Button(ExpandAll) { text = "全部展开" };
            expandAllBtn.style.marginLeft = 8;
            headerRow.Add(expandAllBtn);

            var collapseAllBtn = new Button(CollapseAll) { text = "全部折叠" };
            collapseAllBtn.style.marginLeft = 8;
            headerRow.Add(collapseAllBtn);
        }

        private void BuildStatsCard(VisualElement parent)
        {
            var card = CreateCard("统计信息");
            parent.Add(card);

            var content = new VisualElement();
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingBottom = 12;
            content.style.flexDirection = FlexDirection.Row;
            card.Add(content);

            var activeBox = CreateStatBox("活跃 Action", "0", new Color(0.3f, 0.7f, 0.4f));
            mActiveCountLabel = activeBox.Q<Label>("value");
            content.Add(activeBox);

            var totalBox = CreateStatBox("已完成", "0", new Color(0.4f, 0.6f, 0.8f));
            mTotalFinishedLabel = totalBox.Q<Label>("value");
            totalBox.style.marginLeft = 16;
            content.Add(totalBox);
        }

        private void BuildStackTraceSettingsCard(VisualElement parent)
        {
            var card = CreateCard("堆栈追踪设置");
            card.style.marginTop = 12;
            parent.Add(card);

            var content = new VisualElement();
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingBottom = 12;
            card.Add(content);

            // 第一行：启用开关和堆栈数量
            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.alignItems = Align.Center;
            row1.style.marginBottom = 8;
            content.Add(row1);

            var enableToggle = new Toggle("启用堆栈追踪");
            enableToggle.value = ActionStackTraceService.Enabled;
            enableToggle.RegisterValueChangedCallback(evt =>
            {
                ActionStackTraceService.Enabled = evt.newValue;
            });
            enableToggle.tooltip = "启用后会记录每个 Action 的创建位置，有一定性能开销";
            row1.Add(enableToggle);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            row1.Add(spacer);

            mStackCountLabel = new Label($"已记录: {ActionStackTraceService.Count}");
            mStackCountLabel.style.fontSize = 11;
            mStackCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            row1.Add(mStackCountLabel);

            // 第二行：退出时清空开关和清空按钮
            var row2 = new VisualElement();
            row2.style.flexDirection = FlexDirection.Row;
            row2.style.alignItems = Align.Center;
            content.Add(row2);

            var clearOnExitToggle = new Toggle("退出播放时清空");
            clearOnExitToggle.value = mClearStackOnExit;
            clearOnExitToggle.RegisterValueChangedCallback(evt => mClearStackOnExit = evt.newValue);
            clearOnExitToggle.tooltip = "退出 Play Mode 时自动清空堆栈记录";
            row2.Add(clearOnExitToggle);

            var spacer2 = new VisualElement();
            spacer2.style.flexGrow = 1;
            row2.Add(spacer2);

            var clearBtn = new Button(() =>
            {
                ActionStackTraceService.Clear();
                mStackCountLabel.text = $"已记录: 0";
                mStackTraceLabel.text = "点击上方 Action 查看调用位置";
                mStackTraceLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            }) { text = "清空堆栈" };
            row2.Add(clearBtn);
        }

        private VisualElement CreateStatBox(string label, string value, Color color)
        {
            var box = new VisualElement();
            box.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            box.style.borderTopLeftRadius = 4;
            box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4;
            box.style.borderBottomRightRadius = 4;
            box.style.paddingLeft = 16;
            box.style.paddingRight = 16;
            box.style.paddingTop = 8;
            box.style.paddingBottom = 8;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = new StyleColor(color);

            var labelElement = new Label(label);
            labelElement.style.fontSize = 11;
            labelElement.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            box.Add(labelElement);

            var valueElement = new Label(value);
            valueElement.name = "value";
            valueElement.style.fontSize = 20;
            valueElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueElement.style.color = new StyleColor(color);
            box.Add(valueElement);

            return box;
        }

        private void BuildActiveActionsCard(VisualElement parent)
        {
            var card = CreateCard("活跃 Action 树 (点击查看调用堆栈)");
            card.style.marginTop = 12;
            parent.Add(card);

            mTreeContainer = new VisualElement();
            mTreeContainer.style.paddingLeft = 8;
            mTreeContainer.style.paddingRight = 8;
            mTreeContainer.style.paddingBottom = 12;
            card.Add(mTreeContainer);
        }

        private void BuildStackTraceCard(VisualElement parent)
        {
            var card = CreateCard("调用堆栈");
            card.style.marginTop = 12;
            parent.Add(card);

            mStackTraceLabel = new Label("点击上方 Action 查看调用位置");
            mStackTraceLabel.style.paddingLeft = 12;
            mStackTraceLabel.style.paddingRight = 12;
            mStackTraceLabel.style.paddingTop = 8;
            mStackTraceLabel.style.paddingBottom = 12;
            mStackTraceLabel.style.fontSize = 11;
            mStackTraceLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            mStackTraceLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(mStackTraceLabel);
        }

        private VisualElement CreateCard(string title)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;

            var header = new Label(title);
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.paddingLeft = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            card.Add(header);

            return card;
        }

        private void RefreshTreeView()
        {
            mTreeContainer.Clear();

            if (mActiveActions.Count == 0)
            {
                var emptyLabel = new Label("暂无活跃的 Action");
                emptyLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.paddingTop = 20;
                emptyLabel.style.paddingBottom = 20;
                mTreeContainer.Add(emptyLabel);
                return;
            }

            var currentTime = Time.realtimeSinceStartup;

            for (int i = 0; i < mActiveActions.Count; i++)
            {
                var action = mActiveActions[i];
                var executorName = i < mExecutorNames.Count ? mExecutorNames[i] : "Unknown";
                var treeItem = CreateTreeNode(action, executorName, 0, currentTime, -1);
                mTreeContainer.Add(treeItem);
            }
        }

        private VisualElement CreateTreeNode(IAction action, string executorName, int depth, float currentTime, int indexInParent)
        {
            var container = new VisualElement();

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;
            row.style.paddingLeft = depth * 16;
            
            // 选中高亮
            if (action.ActionID == mSelectedActionId)
            {
                row.style.backgroundColor = new StyleColor(new Color(0.2f, 0.4f, 0.6f, 0.3f));
            }
            
            // 整行可点击
            var actionId = action.ActionID;
            row.RegisterCallback<ClickEvent>(evt =>
            {
                mLastInteractionTime = EditorApplication.timeSinceStartup;
                mSelectedActionId = actionId;
                ShowStackTrace(actionId);
                RefreshTreeView();
                evt.StopPropagation();
            });
            
            container.Add(row);

            // 展开/折叠按钮
            var childCount = ActionMonitorService.GetChildCount(action);
            var hasChildren = childCount > 0;
            var isExpanded = mExpandedNodes.Contains(action.ActionID);

            var expandBtn = new Button(() =>
            {
                // 设置交互保护时间
                mLastInteractionTime = EditorApplication.timeSinceStartup;
                
                // 点击时重新检查状态
                if (mExpandedNodes.Contains(actionId))
                    mExpandedNodes.Remove(actionId);
                else
                    mExpandedNodes.Add(actionId);
                RefreshTreeView();
            });
            expandBtn.text = hasChildren ? (isExpanded ? "▼" : "▶") : "   ";
            expandBtn.style.width = 18;
            expandBtn.style.height = 18;
            expandBtn.style.fontSize = 9;
            expandBtn.style.paddingLeft = 0;
            expandBtn.style.paddingRight = 0;
            expandBtn.style.paddingTop = 0;
            expandBtn.style.paddingBottom = 0;
            expandBtn.style.marginRight = 2;
            expandBtn.style.backgroundColor = StyleKeyword.Null;
            expandBtn.style.borderLeftWidth = 0;
            expandBtn.style.borderRightWidth = 0;
            expandBtn.style.borderTopWidth = 0;
            expandBtn.style.borderBottomWidth = 0;
            if (!hasChildren) expandBtn.SetEnabled(false);
            row.Add(expandBtn);

            // 状态指示器
            var indicator = new VisualElement();
            indicator.style.width = 8;
            indicator.style.height = 8;
            indicator.style.borderTopLeftRadius = 4;
            indicator.style.borderTopRightRadius = 4;
            indicator.style.borderBottomLeftRadius = 4;
            indicator.style.borderBottomRightRadius = 4;
            indicator.style.marginRight = 4;

            var statusColor = action.ActionState switch
            {
                ActionStatus.Started => new Color(0.3f, 0.8f, 0.4f),
                ActionStatus.Finished => new Color(0.5f, 0.5f, 0.5f),
                _ => new Color(0.8f, 0.8f, 0.3f)
            };
            indicator.style.backgroundColor = new StyleColor(statusColor);
            row.Add(indicator);

            // Action 类型图标
            var typeName = ActionMonitorService.GetTypeName(action);
            var typeIcon = GetTypeIcon(typeName);
            var iconLabel = new Label(typeIcon);
            iconLabel.style.width = 16;
            iconLabel.style.fontSize = 11;
            iconLabel.style.marginRight = 2;
            row.Add(iconLabel);

            // Action 类型名
            var typeLabel = new Label(typeName);
            typeLabel.style.width = 70;
            typeLabel.style.fontSize = 11;
            typeLabel.style.color = new StyleColor(GetTypeColor(typeName));
            typeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(typeLabel);

            // 调试信息
            var debugInfo = action.GetDebugInfo();
            if (debugInfo != typeName)
            {
                var infoLabel = new Label(debugInfo);
                infoLabel.style.flexGrow = 1;
                infoLabel.style.fontSize = 10;
                infoLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                infoLabel.style.overflow = Overflow.Hidden;
                row.Add(infoLabel);
            }
            else
            {
                var spacer = new VisualElement();
                spacer.style.flexGrow = 1;
                row.Add(spacer);
            }

            // 当前子 Action 进度（仅 Sequence）
            if (typeName == "Sequence" && childCount > 0)
            {
                var currentIndex = ActionMonitorService.GetCurrentChildIndex(action);
                var progressLabel = new Label($"[{currentIndex + 1}/{childCount}]");
                progressLabel.style.width = 45;
                progressLabel.style.fontSize = 10;
                progressLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.9f));
                progressLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                row.Add(progressLabel);
            }

            // 执行器名称（仅根节点）
            if (depth == 0)
            {
                var executorLabel = new Label(executorName);
                executorLabel.style.width = 80;
                executorLabel.style.fontSize = 10;
                executorLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                executorLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                row.Add(executorLabel);
            }

            // 子节点
            if (hasChildren && isExpanded)
            {
                var currentChildIndex = ActionMonitorService.GetCurrentChildIndex(action);
                for (int i = 0; i < childCount; i++)
                {
                    var child = ActionMonitorService.GetChild(action, i);
                    if (child == null) continue;

                    var childNode = CreateTreeNode(child, executorName, depth + 1, currentTime, i);
                    
                    // 高亮当前执行的子节点（Sequence）
                    if (i == currentChildIndex && typeName == "Sequence")
                    {
                        childNode.style.backgroundColor = new StyleColor(new Color(0.2f, 0.28f, 0.2f));
                    }
                    
                    container.Add(childNode);
                }
            }

            return container;
        }

        private string GetTypeIcon(string typeName)
        {
            return typeName switch
            {
                "Sequence" => "▶",
                "Parallel" => "⊞",
                "Repeat" => "↻",
                "Delay" => "⏱",
                "DelayFrame" => "⏱",
                "Callback" => "λ",
                "Lerp" => "~",
                _ => "•"
            };
        }

        private Color GetTypeColor(string typeName)
        {
            return typeName switch
            {
                "Sequence" => new Color(0.5f, 0.8f, 1f),
                "Parallel" => new Color(1f, 0.7f, 0.5f),
                "Repeat" => new Color(0.8f, 0.5f, 1f),
                "Delay" or "DelayFrame" => new Color(0.9f, 0.9f, 0.5f),
                "Callback" => new Color(0.5f, 1f, 0.7f),
                "Lerp" => new Color(1f, 0.6f, 0.8f),
                _ => new Color(0.8f, 0.8f, 0.8f)
            };
        }

        private void ExpandAll()
        {
            foreach (var action in mActiveActions)
            {
                ExpandRecursive(action);
            }
            RefreshTreeView();
        }

        private void ExpandRecursive(IAction action)
        {
            mExpandedNodes.Add(action.ActionID);
            var childCount = ActionMonitorService.GetChildCount(action);
            for (int i = 0; i < childCount; i++)
            {
                var child = ActionMonitorService.GetChild(action, i);
                if (child != null)
                    ExpandRecursive(child);
            }
        }

        private void CollapseAll()
        {
            mExpandedNodes.Clear();
            RefreshTreeView();
        }

        private void ShowStackTrace(ulong actionId)
        {
            if (ActionStackTraceService.TryGet(actionId, out var stackTrace))
            {
                var sb = new System.Text.StringBuilder();
                var frames = stackTrace.GetFrames();
                
                if (frames != null)
                {
                    foreach (var frame in frames)
                    {
                        var method = frame.GetMethod();
                        if (method == null) continue;
                        
                        var declaringType = method.DeclaringType;
                        if (declaringType == null) continue;
                        
                        // 跳过 Unity 和系统内部调用
                        var ns = declaringType.Namespace ?? "";
                        if (ns.StartsWith("UnityEngine") || ns.StartsWith("UnityEditor") || 
                            ns.StartsWith("System") || ns.StartsWith("YokiFrame.Sequence") ||
                            declaringType.Name.Contains("ActionExtensions"))
                            continue;
                        
                        var fileName = frame.GetFileName();
                        var lineNumber = frame.GetFileLineNumber();
                        
                        sb.Append($"→ {declaringType.Name}.{method.Name}()");
                        
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            // 只显示 Assets 之后的路径
                            var assetsIndex = fileName.IndexOf("Assets");
                            if (assetsIndex >= 0)
                                fileName = fileName.Substring(assetsIndex);
                            sb.Append($"\n   {fileName}:{lineNumber}");
                        }
                        sb.AppendLine();
                    }
                }
                
                mStackTraceLabel.text = sb.Length > 0 ? sb.ToString() : "无可用堆栈信息";
                mStackTraceLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            }
            else
            {
                mStackTraceLabel.text = "堆栈信息不可用（可能是子 Action）";
                mStackTraceLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            }
        }

        private void RefreshData()
        {
            var prevCount = mActiveActions.Count;
            ActionMonitorService.CollectActiveActions(mActiveActions, mExecutorNames);
            
            // 统计完成数
            if (prevCount > mActiveActions.Count)
            {
                mTotalFinished += prevCount - mActiveActions.Count;
            }
            
            mActiveCountLabel.text = mActiveActions.Count.ToString();
            mTotalFinishedLabel.text = mTotalFinished.ToString();
            
            // 更新堆栈数量
            if (mStackCountLabel != null)
            {
                mStackCountLabel.text = $"已记录: {ActionStackTraceService.Count}";
            }

            RefreshTreeView();
        }

        public override void OnUpdate()
        {
            if (!mAutoRefresh) return;
            if (!Application.isPlaying) return;

            var currentTime = EditorApplication.timeSinceStartup;
            
            // 交互保护期内不刷新
            if (currentTime - mLastInteractionTime < INTERACTION_COOLDOWN) return;
            
            if (currentTime - mLastRefreshTime < REFRESH_INTERVAL) return;

            mLastRefreshTime = currentTime;
            RefreshData();
        }

        public override void OnActivate()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public override void OnDeactivate()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                mExpandedNodes.Clear();
                mTotalFinished = 0;
                mSelectedActionId = 0;
                
                // 根据设置决定是否清空堆栈
                if (mClearStackOnExit)
                {
                    ActionStackTraceService.Clear();
                }
                
                if (mStackTraceLabel != null)
                {
                    mStackTraceLabel.text = "点击上方 Action 查看调用位置";
                    mStackTraceLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                }
                
                if (mStackCountLabel != null)
                {
                    mStackCountLabel.text = $"已记录: {ActionStackTraceService.Count}";
                }
            }
            RefreshData();
        }
    }
}
