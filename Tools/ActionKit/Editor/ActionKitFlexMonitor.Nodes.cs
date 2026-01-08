using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKitFlexMonitor - 节点构建与数据刷新部分
    /// </summary>
    public partial class ActionKitFlexMonitor
    {
        #region 节点构建

        private VisualElement CreateVisualNode(IAction action, string executorName, bool isRoot = false)
        {
            var typeName = ActionMonitorService.GetTypeName(action);
            var isSequence = typeName == "Sequence";
            var isParallel = typeName == "Parallel";
            var isRepeat = typeName == "Repeat";
            var isContainer = isSequence || isParallel || isRepeat;

            var card = CreateActionCard(action, typeName, executorName, isRoot);
            mNodeCache[action.ActionID] = card;

            if (isContainer)
            {
                var childCount = ActionMonitorService.GetChildCount(action);
                if (childCount > 0)
                {
                    var childContainer = CreateChildContainer(isSequence, isParallel, isRepeat);
                    card.Add(childContainer);

                    var currentIndex = ActionMonitorService.GetCurrentChildIndex(action);
                    for (var i = 0; i < childCount; i++)
                    {
                        var child = ActionMonitorService.GetChild(action, i);
                        if (child == null) continue;

                        var childNode = CreateVisualNode(child, executorName);
                        if (isSequence && i == currentIndex)
                            childNode.style.backgroundColor = new StyleColor(new Color(0.2f, 0.3f, 0.2f));
                        childContainer.Add(childNode);
                    }
                }
            }
            return card;
        }

        private VisualElement CreateActionCard(IAction action, string typeName, string executorName, bool isRoot)
        {
            var typeColor = GetTypeColor(typeName);
            var card = new VisualElement
            {
                name = $"card-{action.ActionID}",
                style =
                {
                    backgroundColor = new StyleColor(CARD_BG),
                    borderTopLeftRadius = CARD_BORDER_RADIUS,
                    borderTopRightRadius = CARD_BORDER_RADIUS,
                    borderBottomLeftRadius = CARD_BORDER_RADIUS,
                    borderBottomRightRadius = CARD_BORDER_RADIUS,
                    marginLeft = CARD_MARGIN,
                    marginRight = CARD_MARGIN,
                    marginTop = CARD_MARGIN,
                    marginBottom = CARD_MARGIN,
                    paddingLeft = CARD_PADDING,
                    paddingRight = CARD_PADDING,
                    paddingTop = CARD_PADDING,
                    paddingBottom = CARD_PADDING,
                    borderLeftWidth = 3,
                    borderLeftColor = new StyleColor(typeColor),
                    position = Position.Relative,
                    overflow = Overflow.Hidden
                }
            };

            ApplyStatusStyle(card, action.ActionState);
            BuildCardHeader(card, action, typeName, typeColor, executorName, isRoot);
            RegisterCardClick(card, action.ActionID);

            return card;
        }

        private void BuildCardHeader(VisualElement card, IAction action, string typeName, Color typeColor, string executorName, bool isRoot)
        {
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            card.Add(header);

            // 状态点
            var statusColor = action.ActionState switch { ActionStatus.Started => COLOR_RUNNING, ActionStatus.Finished => COLOR_FINISHED, _ => new Color(0.4f, 0.4f, 0.4f) };
            var dot = new VisualElement { style = { width = 8, height = 8, borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4, backgroundColor = new StyleColor(statusColor), marginRight = 6 } };
            header.Add(dot);

            // 图标和类型
            header.Add(new Label(GetTypeIcon(typeName)) { style = { fontSize = 12, marginRight = 4, color = new StyleColor(typeColor) } });
            header.Add(new Label(typeName) { style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, color = new StyleColor(typeColor), marginRight = 8 } });

            // 调试信息
            var debugInfo = action.GetDebugInfo();
            if (debugInfo != typeName && !debugInfo.StartsWith(typeName))
            {
                var info = debugInfo.Length > 30 ? debugInfo.Substring(0, 27) + "..." : debugInfo;
                header.Add(new Label(info) { style = { fontSize = 10, color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)), flexGrow = 1 }, tooltip = debugInfo });
            }

            // Sequence 进度
            var childCount = ActionMonitorService.GetChildCount(action);
            if (typeName == "Sequence" && childCount > 0)
            {
                var idx = ActionMonitorService.GetCurrentChildIndex(action);
                header.Add(new Label($"[{idx + 1}/{childCount}]") { style = { fontSize = 10, color = new StyleColor(COLOR_SEQUENCE), marginLeft = 8 } });
            }

            // 执行器标签
            if (isRoot)
            {
                var exec = new Label(executorName) { style = { fontSize = 9, color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)), marginLeft = 8, paddingLeft = 6, paddingRight = 6, paddingTop = 2, paddingBottom = 2, backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f)), borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3 } };
                header.Add(exec);
            }
        }

        private void RegisterCardClick(VisualElement card, ulong actionId)
        {
            card.RegisterCallback<ClickEvent>(evt =>
            {
                mLastInteractionTime = EditorApplication.timeSinceStartup;
                mSelectedActionId = actionId;
                ShowStackTrace(actionId);
                UpdateSelection();
                evt.StopPropagation();
            });
        }

        private VisualElement CreateChildContainer(bool isSequence, bool isParallel, bool isRepeat)
        {
            var container = new VisualElement { style = { marginTop = 8, paddingTop = 8 } };

            if (isSequence || isRepeat)
            {
                // 纵向排列，左侧连接线
                container.style.flexDirection = FlexDirection.Column;
                container.style.borderLeftWidth = 2;
                container.style.borderLeftColor = new StyleColor(isRepeat ? COLOR_LEAF_REPEAT : COLOR_SEQUENCE);
                container.style.paddingLeft = 8;
                container.style.marginLeft = 4;
            }
            else if (isParallel)
            {
                // 横向排列，边框包围
                container.style.flexDirection = FlexDirection.Row;
                container.style.flexWrap = Wrap.Wrap;
                container.style.alignItems = Align.FlexStart;
                container.style.borderTopWidth = 1;
                container.style.borderBottomWidth = 1;
                container.style.borderLeftWidth = 1;
                container.style.borderRightWidth = 1;
                container.style.borderTopColor = new StyleColor(COLOR_PARALLEL);
                container.style.borderBottomColor = new StyleColor(COLOR_PARALLEL);
                container.style.borderLeftColor = new StyleColor(COLOR_PARALLEL);
                container.style.borderRightColor = new StyleColor(COLOR_PARALLEL);
                container.style.borderTopLeftRadius = 4;
                container.style.borderTopRightRadius = 4;
                container.style.borderBottomLeftRadius = 4;
                container.style.borderBottomRightRadius = 4;
                container.style.paddingLeft = 4;
                container.style.paddingRight = 4;
                container.style.paddingTop = 4;
                container.style.paddingBottom = 4;
                container.style.backgroundColor = new StyleColor(new Color(0.25f, 0.2f, 0.15f, 0.3f));
            }
            return container;
        }

        private void ApplyStatusStyle(VisualElement card, ActionStatus status)
        {
            switch (status)
            {
                case ActionStatus.Started:
                    card.style.borderTopWidth = 1;
                    card.style.borderBottomWidth = 1;
                    card.style.borderRightWidth = 1;
                    card.style.borderTopColor = new StyleColor(new Color(COLOR_RUNNING.r, COLOR_RUNNING.g, COLOR_RUNNING.b, 0.4f));
                    card.style.borderBottomColor = new StyleColor(new Color(COLOR_RUNNING.r, COLOR_RUNNING.g, COLOR_RUNNING.b, 0.4f));
                    card.style.borderRightColor = new StyleColor(new Color(COLOR_RUNNING.r, COLOR_RUNNING.g, COLOR_RUNNING.b, 0.4f));
                    card.style.opacity = 1f;
                    break;
                case ActionStatus.Finished:
                    card.style.opacity = 0.5f;
                    break;
                default:
                    card.style.opacity = 0.8f;
                    break;
            }
        }

        #endregion

        #region 数据刷新

        private void RefreshData()
        {
            var prevCount = mActiveActions.Count;
            ActionMonitorService.CollectActiveActions(mActiveActions, mExecutorNames);

            if (prevCount > mActiveActions.Count)
                mTotalFinished += prevCount - mActiveActions.Count;

            mActiveCountLabel.text = mActiveActions.Count.ToString();
            mTotalFinishedLabel.text = mTotalFinished.ToString();
            mStackCountLabel.text = $"已记录: {ActionStackTraceService.Count}";

            RefreshTreeView();
        }

        private void RefreshTreeView()
        {
            mTreeContainer.Clear();
            mNodeCache.Clear();

            if (mActiveActions.Count == 0)
            {
                mTreeContainer.Add(new Label("暂无活跃的 Action") { style = { color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)), unityTextAlign = TextAnchor.MiddleCenter, paddingTop = 40, paddingBottom = 40 } });
                return;
            }

            for (var i = 0; i < mActiveActions.Count; i++)
            {
                var action = mActiveActions[i];
                var executorName = i < mExecutorNames.Count ? mExecutorNames[i] : "Unknown";
                mTreeContainer.Add(CreateVisualNode(action, executorName, true));
            }
        }

        private void UpdateSelection()
        {
            foreach (var kvp in mNodeCache)
            {
                var isSelected = kvp.Key == mSelectedActionId;
                kvp.Value.style.borderRightWidth = isSelected ? 2 : 0;
                kvp.Value.style.borderRightColor = isSelected ? new StyleColor(new Color(0.4f, 0.7f, 1f)) : new StyleColor(Color.clear);
            }
        }

        private void ShowStackTrace(ulong actionId)
        {
            // 检查是否启用了堆栈追踪
            if (!ActionStackTraceService.Enabled)
            {
                mStackTraceLabel.text = "⚠ 请先启用「堆栈追踪」功能，然后重新运行游戏";
                mStackTraceLabel.style.color = new StyleColor(new Color(1f, 0.7f, 0.3f));
                return;
            }
            
            if (ActionStackTraceService.TryGet(actionId, out var stackTrace))
            {
                var sb = new StringBuilder();
                var frames = stackTrace.GetFrames();
                if (frames != null)
                {
                    foreach (var frame in frames)
                    {
                        var method = frame.GetMethod();
                        if (method?.DeclaringType == null) continue;
                        var ns = method.DeclaringType.Namespace ?? "";
                        if (ns.StartsWith("UnityEngine") || ns.StartsWith("UnityEditor") || ns.StartsWith("System") || ns.StartsWith("YokiFrame.Sequence") || method.DeclaringType.Name.Contains("ActionExtensions")) continue;

                        sb.Append($"→ {method.DeclaringType.Name}.{method.Name}()");
                        var fileName = frame.GetFileName();
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            var idx = fileName.IndexOf("Assets");
                            if (idx >= 0) fileName = fileName.Substring(idx);
                            sb.Append($"\n   {fileName}:{frame.GetFileLineNumber()}");
                        }
                        sb.AppendLine();
                    }
                }
                mStackTraceLabel.text = sb.Length > 0 ? sb.ToString() : "无可用堆栈（可能被过滤）";
                mStackTraceLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            }
            else
            {
                // 区分是子 Action 还是启用后创建的 Action
                mStackTraceLabel.text = ActionStackTraceService.Count > 0 
                    ? "此 Action 无堆栈记录（可能是子节点或启用前创建）" 
                    : "暂无堆栈记录，请启用后重新运行游戏";
                mStackTraceLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            }
        }

        #endregion
    }
}
