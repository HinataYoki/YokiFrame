using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 监控页的节点构建与数据刷新部分。
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

            if (!isContainer)
            {
                return card;
            }

            var childCount = ActionMonitorService.GetChildCount(action);
            if (childCount <= 0)
            {
                return card;
            }

            var childContainer = CreateChildContainer(isSequence, isParallel, isRepeat);
            card.Add(childContainer);

            var currentIndex = ActionMonitorService.GetCurrentChildIndex(action);
            for (var i = 0; i < childCount; i++)
            {
                var child = ActionMonitorService.GetChild(action, i);
                if (child == null)
                {
                    continue;
                }

                var childNode = CreateVisualNode(child, executorName);
                if (isSequence && i == currentIndex)
                {
                    childNode.AddToClassList("yoki-action-node--sequence-current");
                }

                childContainer.Add(childNode);
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

            var statusColor = action.ActionState switch
            {
                ActionStatus.Started => COLOR_RUNNING,
                ActionStatus.Finished => COLOR_FINISHED,
                _ => Colors.TextTertiary
            };

            var dot = new VisualElement();
            dot.AddToClassList("yoki-action-node__status-dot");
            dot.style.backgroundColor = new StyleColor(statusColor);
            header.Add(dot);

            var icon = new Label(GetTypeIcon(typeName));
            icon.AddToClassList("yoki-action-node__icon");
            icon.style.color = new StyleColor(typeColor);
            header.Add(icon);

            var typeLabel = new Label(typeName);
            typeLabel.AddToClassList("yoki-action-node__type");
            typeLabel.style.color = new StyleColor(typeColor);
            header.Add(typeLabel);

            var debugInfo = action.GetDebugInfo();
            if (debugInfo != typeName && !debugInfo.StartsWith(typeName))
            {
                var info = debugInfo.Length > 30 ? debugInfo.Substring(0, 27) + "..." : debugInfo;
                var infoLabel = new Label(info) { tooltip = debugInfo };
                infoLabel.AddToClassList("yoki-action-node__info");
                header.Add(infoLabel);
            }

            var childCount = ActionMonitorService.GetChildCount(action);
            if (typeName == "Sequence" && childCount > 0)
            {
                var idx = ActionMonitorService.GetCurrentChildIndex(action);
                var progress = new Label($"[{idx + 1}/{childCount}]");
                progress.AddToClassList("yoki-action-node__progress");
                progress.style.color = new StyleColor(COLOR_SEQUENCE);
                header.Add(progress);
            }

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
                    card.style.borderTopColor = new StyleColor(new Color(COLOR_RUNNING.r, COLOR_RUNNING.g, COLOR_RUNNING.b, 0.36f));
                    card.style.borderBottomColor = new StyleColor(new Color(COLOR_RUNNING.r, COLOR_RUNNING.g, COLOR_RUNNING.b, 0.36f));
                    card.style.borderRightColor = new StyleColor(new Color(COLOR_RUNNING.r, COLOR_RUNNING.g, COLOR_RUNNING.b, 0.36f));
                    break;
                case ActionStatus.Finished:
                    card.AddToClassList("yoki-action-node--finished");
                    break;
                default:
                    card.style.opacity = 0.82f;
                    break;
            }
        }

        #endregion

        #region 数据刷新

        private void RefreshData()
        {
            var previousCount = mActiveActions.Count;
            ActionMonitorService.CollectActiveActions(mActiveActions, mExecutorNames);

            if (previousCount > mActiveActions.Count)
            {
                mTotalFinished += previousCount - mActiveActions.Count;
            }

            if (mActiveCountLabel != null)
            {
                mActiveCountLabel.text = mActiveActions.Count.ToString();
            }

            if (mTotalFinishedLabel != null)
            {
                mTotalFinishedLabel.text = mTotalFinished.ToString();
            }

            if (mStackCountLabel != null)
            {
                mStackCountLabel.text = $"已记录: {ActionStackTraceService.Count}";
            }

            RefreshTreeView();
        }

        private void RefreshTreeView()
        {
            if (mTreeContainer == null)
            {
                return;
            }

            mTreeContainer.Clear();
            mNodeCache.Clear();

            if (mActiveActions.Count == 0)
            {
                mSelectedActionId = 0;

                if (mStackTraceLabel != null)
                {
                    mStackTraceLabel.text = "当前没有活跃 Action。";
                    mStackTraceLabel.style.color = new StyleColor(Colors.TextTertiary);
                }

                var empty = CreateEmptyState(
                    KitIcons.INFO,
                    "暂无活跃 Action",
                    Application.isPlaying
                        ? "当有 Action 启动后，这里会实时显示组合结构和执行状态。"
                        : "请进入 PlayMode 后查看运行时流程。");
                empty.AddToClassList("yoki-action-empty");
                mTreeContainer.Add(empty);
                return;
            }

            for (var i = 0; i < mActiveActions.Count; i++)
            {
                var action = mActiveActions[i];
                var executorName = i < mExecutorNames.Count ? mExecutorNames[i] : "Unknown";
                mTreeContainer.Add(CreateVisualNode(action, executorName, true));
            }

            if (mSelectedActionId != 0 && !mNodeCache.ContainsKey(mSelectedActionId))
            {
                mSelectedActionId = 0;
                mStackTraceLabel.text = "所选 Action 已结束。";
                mStackTraceLabel.style.color = new StyleColor(Colors.TextTertiary);
            }

            UpdateSelection();
        }

        private void UpdateSelection()
        {
            foreach (var pair in mNodeCache)
            {
                var isSelected = pair.Key == mSelectedActionId;
                if (isSelected)
                {
                    pair.Value.AddToClassList("yoki-action-node--selected");
                    pair.Value.style.borderRightColor = new StyleColor(Colors.BrandPrimary);
                }
                else
                {
                    pair.Value.RemoveFromClassList("yoki-action-node--selected");
                    pair.Value.style.borderRightColor = new StyleColor(Color.clear);
                }
            }
        }

        private void ShowStackTrace(ulong actionId)
        {
            if (!ActionStackTraceService.Enabled)
            {
                mStackTraceLabel.text = "请先启用“堆栈追踪”，然后重新进入 PlayMode，再点击节点查看调用来源。";
                mStackTraceLabel.style.color = new StyleColor(Colors.StatusWarning);
                return;
            }

            if (ActionStackTraceService.TryGet(actionId, out var stackTrace))
            {
                var builder = new StringBuilder();
                var frames = stackTrace.GetFrames();
                if (frames != null)
                {
                    foreach (var frame in frames)
                    {
                        var method = frame.GetMethod();
                        if (method?.DeclaringType == null)
                        {
                            continue;
                        }

                        var ns = method.DeclaringType.Namespace ?? string.Empty;
                        if (ns.StartsWith("UnityEngine") ||
                            ns.StartsWith("UnityEditor") ||
                            ns.StartsWith("System") ||
                            ns.StartsWith("YokiFrame.Sequence") ||
                            method.DeclaringType.Name.Contains("ActionExtensions"))
                        {
                            continue;
                        }

                        builder.Append($"→ {method.DeclaringType.Name}.{method.Name}()");
                        var fileName = frame.GetFileName();
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            var idx = fileName.IndexOf("Assets");
                            if (idx >= 0)
                            {
                                fileName = fileName.Substring(idx);
                            }

                            builder.Append($"\n   {fileName}:{frame.GetFileLineNumber()}");
                        }

                        builder.AppendLine();
                    }
                }

                mStackTraceLabel.text = builder.Length > 0 ? builder.ToString() : "没有可显示的堆栈帧，可能已被过滤。";
                mStackTraceLabel.style.color = new StyleColor(Colors.TextSecondary);
                return;
            }

            mStackTraceLabel.text = ActionStackTraceService.Count > 0
                ? "该 Action 没有可用堆栈记录，可能是子节点或在启用追踪前创建。"
                : "当前没有堆栈记录，请启用后重新进入 PlayMode。";
            mStackTraceLabel.style.color = new StyleColor(Colors.TextTertiary);
        }

        #endregion
    }
}
