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
            var card = new VisualElement { name = $"card-{action.ActionID}" };
            card.AddToClassList("yoki-action-node");
            card.style.borderLeftColor = new StyleColor(typeColor);

            ApplyStatusStyle(card, action.ActionState);
            BuildCardHeader(card, action, typeName, typeColor, executorName, isRoot);
            RegisterCardClick(card, action.ActionID);

            return card;
        }

        private void BuildCardHeader(VisualElement card, IAction action, string typeName, Color typeColor, string executorName, bool isRoot)
        {
            var header = new VisualElement();
            header.AddToClassList("yoki-action-node__header");
            card.Add(header);

            // 状态点
            var statusColor = action.ActionState switch { ActionStatus.Started => COLOR_RUNNING, ActionStatus.Finished => COLOR_FINISHED, _ => new Color(0.4f, 0.4f, 0.4f) };
            var dot = new VisualElement();
            dot.AddToClassList("yoki-action-node__status-dot");
            dot.style.backgroundColor = new StyleColor(statusColor);
            header.Add(dot);

            // 图标和类型
            var icon = new Label(GetTypeIcon(typeName));
            icon.AddToClassList("yoki-action-node__icon");
            icon.style.color = new StyleColor(typeColor);
            header.Add(icon);
            
            var typeLabel = new Label(typeName);
            typeLabel.AddToClassList("yoki-action-node__type");
            typeLabel.style.color = new StyleColor(typeColor);
            header.Add(typeLabel);

            // 调试信息
            var debugInfo = action.GetDebugInfo();
            if (debugInfo != typeName && !debugInfo.StartsWith(typeName))
            {
                var info = debugInfo.Length > 30 ? debugInfo.Substring(0, 27) + "..." : debugInfo;
                var infoLabel = new Label(info) { tooltip = debugInfo };
                infoLabel.AddToClassList("yoki-action-node__info");
                header.Add(infoLabel);
            }

            // Sequence 进度
            var childCount = ActionMonitorService.GetChildCount(action);
            if (typeName == "Sequence" && childCount > 0)
            {
                var idx = ActionMonitorService.GetCurrentChildIndex(action);
                var progress = new Label($"[{idx + 1}/{childCount}]");
                progress.AddToClassList("yoki-action-node__progress");
                progress.style.color = new StyleColor(COLOR_SEQUENCE);
                header.Add(progress);
            }

            // 执行器标签
            if (isRoot)
            {
                var exec = new Label(executorName);
                exec.AddToClassList("yoki-action-node__executor");
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
            var container = new VisualElement();
            container.AddToClassList("yoki-action-child-container");

            if (isSequence)
            {
                container.AddToClassList("yoki-action-child-container--sequence");
                container.style.borderLeftColor = new StyleColor(COLOR_SEQUENCE);
            }
            else if (isParallel)
            {
                container.AddToClassList("yoki-action-child-container--parallel");
                container.style.borderTopColor = new StyleColor(COLOR_PARALLEL);
                container.style.borderBottomColor = new StyleColor(COLOR_PARALLEL);
                container.style.borderLeftColor = new StyleColor(COLOR_PARALLEL);
                container.style.borderRightColor = new StyleColor(COLOR_PARALLEL);
            }
            else if (isRepeat)
            {
                container.AddToClassList("yoki-action-child-container--repeat");
                container.style.borderLeftColor = new StyleColor(COLOR_LEAF_REPEAT);
            }
            
            return container;
        }

        private void ApplyStatusStyle(VisualElement card, ActionStatus status)
        {
            switch (status)
            {
                case ActionStatus.Started:
                    card.AddToClassList("yoki-action-node--running");
                    card.style.borderTopColor = new StyleColor(new Color(COLOR_RUNNING.r, COLOR_RUNNING.g, COLOR_RUNNING.b, 0.4f));
                    card.style.borderBottomColor = new StyleColor(new Color(COLOR_RUNNING.r, COLOR_RUNNING.g, COLOR_RUNNING.b, 0.4f));
                    card.style.borderRightColor = new StyleColor(new Color(COLOR_RUNNING.r, COLOR_RUNNING.g, COLOR_RUNNING.b, 0.4f));
                    break;
                case ActionStatus.Finished:
                    card.AddToClassList("yoki-action-node--finished");
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
                var emptyLabel = new Label("暂无活跃的 Action");
                emptyLabel.AddToClassList("yoki-action-empty");
                mTreeContainer.Add(emptyLabel);
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
                if (isSelected)
                {
                    kvp.Value.AddToClassList("yoki-action-node--selected");
                    kvp.Value.style.borderRightColor = new StyleColor(new Color(0.4f, 0.7f, 1f));
                }
                else
                {
                    kvp.Value.RemoveFromClassList("yoki-action-node--selected");
                    kvp.Value.style.borderRightColor = new StyleColor(Color.clear);
                }
            }
        }

        private void ShowStackTrace(ulong actionId)
        {
            // 检查是否启用了堆栈追踪
            if (!ActionStackTraceService.Enabled)
            {
                mStackTraceLabel.text = "请先启用「堆栈追踪」功能，然后重新运行游戏";
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
