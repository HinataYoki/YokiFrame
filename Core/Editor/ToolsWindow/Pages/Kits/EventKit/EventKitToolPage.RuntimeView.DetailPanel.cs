#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 运行时视图 - 右侧详情面板
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 右侧：深度详情面板

        private VisualElement CreateDetailPanel()
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f));

            // 空状态
            var emptyState = CreateEmptyState("选择左侧事件查看详情");
            emptyState.name = "empty-state";
            panel.Add(emptyState);

            // 详情内容（初始隐藏）
            var detailContent = new VisualElement();
            detailContent.name = "detail-content";
            detailContent.style.display = DisplayStyle.None;
            detailContent.style.flexGrow = 1;
            panel.Add(detailContent);

            // 头部：数据概览
            var header = CreateDetailHeader();
            detailContent.Add(header);

            // 主体：时间轴日志
            var timelineSection = CreateTimelineSection();
            detailContent.Add(timelineSection);

            return panel;
        }

        private VisualElement CreateDetailHeader()
        {
            var header = new VisualElement();
            header.style.paddingLeft = 20;
            header.style.paddingRight = 20;
            header.style.paddingTop = 16;
            header.style.paddingBottom = 16;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.22f));

            // 标题行
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 16;
            header.Add(titleRow);

            mDetailTitle = new Label("事件名称");
            mDetailTitle.style.fontSize = 18;
            mDetailTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            mDetailTitle.style.color = new StyleColor(new Color(0.98f, 0.98f, 0.98f));
            mDetailTitle.style.flexGrow = 1;
            titleRow.Add(mDetailTitle);

            mDetailParamType = new Label("<void>");
            mDetailParamType.style.fontSize = 12;
            mDetailParamType.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.55f));
            titleRow.Add(mDetailParamType);

            // 统计数字容器（仪表盘卡片）
            var statsContainer = new VisualElement();
            statsContainer.style.flexDirection = FlexDirection.Row;
            statsContainer.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.17f));
            statsContainer.style.borderTopLeftRadius = 8;
            statsContainer.style.borderTopRightRadius = 8;
            statsContainer.style.borderBottomLeftRadius = 8;
            statsContainer.style.borderBottomRightRadius = 8;
            statsContainer.style.paddingLeft = 4;
            statsContainer.style.paddingRight = 4;
            statsContainer.style.paddingTop = 4;
            statsContainer.style.paddingBottom = 4;
            header.Add(statsContainer);

            // 累计触发
            var triggerStat = CreateStatBox("累计触发", "0 次", new Color(0.3f, 0.6f, 0.3f));
            mDetailTriggerCount = triggerStat.Q<Label>("stat-value");
            statsContainer.Add(triggerStat);

            // 当前监听者
            var listenerStat = CreateStatBox("当前监听者", "0 个", new Color(0.3f, 0.5f, 0.7f));
            listenerStat.style.marginLeft = 8;
            mDetailListenerCount = listenerStat.Q<Label>("stat-value");
            statsContainer.Add(listenerStat);

            return header;
        }

        private VisualElement CreateStatBox(string label, string value, Color accentColor)
        {
            var box = new VisualElement();
            box.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
            box.style.paddingLeft = 16;
            box.style.paddingRight = 16;
            box.style.paddingTop = 12;
            box.style.paddingBottom = 12;
            box.style.borderTopLeftRadius = 6;
            box.style.borderTopRightRadius = 6;
            box.style.borderBottomLeftRadius = 6;
            box.style.borderBottomRightRadius = 6;
            box.style.minWidth = 100;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = new StyleColor(accentColor);

            var labelElement = new Label(label);
            labelElement.style.fontSize = 10;
            labelElement.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.55f));
            labelElement.style.marginBottom = 4;
            box.Add(labelElement);

            var valueElement = new Label(value);
            valueElement.name = "stat-value";
            valueElement.style.fontSize = 20;
            valueElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueElement.style.color = new StyleColor(new Color(0.95f, 0.95f, 0.95f));
            box.Add(valueElement);

            return box;
        }

        #endregion

        #region 时间轴

        private VisualElement CreateTimelineSection()
        {
            var section = new VisualElement();
            section.style.flexGrow = 1;
            section.style.paddingLeft = 20;
            section.style.paddingRight = 20;
            section.style.paddingTop = 16;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 12;
            section.Add(titleRow);

            // 时间轴图标
            var titleIcon = new Image { image = EditorTools.KitIcons.GetTexture(EditorTools.KitIcons.SCROLL) };
            titleIcon.style.width = 16;
            titleIcon.style.height = 16;
            titleIcon.style.marginRight = 6;
            titleRow.Add(titleIcon);

            var title = new Label("时间轴日志");
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            titleRow.Add(title);

            var clearBtn = new Button(ClearTimeline) { text = "清空" };
            clearBtn.style.height = 22;
            clearBtn.style.fontSize = 11;
            titleRow.Add(clearBtn);

            // 时间轴表头（始终显示）
            var headerRow = CreateTimelineHeader();
            section.Add(headerRow);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            section.Add(scrollView);

            mTimelineContainer = new VisualElement();
            scrollView.Add(mTimelineContainer);

            return section;
        }

        /// <summary>
        /// 创建时间轴表头
        /// </summary>
        private VisualElement CreateTimelineHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 6;
            header.style.paddingBottom = 6;
            header.style.marginBottom = 4;
            header.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.17f));
            header.style.borderTopLeftRadius = 4;
            header.style.borderTopRightRadius = 4;

            var iconCol = new Label("");
            iconCol.style.width = 24;
            header.Add(iconCol);

            var timeCol = new Label("时间");
            timeCol.style.width = 60;
            timeCol.style.fontSize = 10;
            timeCol.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            header.Add(timeCol);

            var actionCol = new Label("类型");
            actionCol.style.width = 80;
            actionCol.style.fontSize = 10;
            actionCol.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            header.Add(actionCol);

            var argsCol = new Label("参数");
            argsCol.style.flexGrow = 1;
            argsCol.style.fontSize = 10;
            argsCol.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            header.Add(argsCol);

            return header;
        }

        private void ClearTimeline()
        {
            mTimelineContainer?.Clear();
            
            // 清理当前选中事件的缓存
            if (mSelectedEvent != null)
            {
                var cacheKey = $"{mSelectedEvent.EventType}_{mSelectedEvent.EventKey}";
                mTimelineHistoryCache.Remove(cacheKey);
            }
            
            ShowEmptyTimelineState();
            EasyEventDebugger.ClearHistory();
        }

        /// <summary>
        /// 创建时间轴条目
        /// </summary>
        private VisualElement CreateTimelineEntry(string action, string time, string args)
        {
            var entry = new VisualElement();
            entry.style.flexDirection = FlexDirection.Row;
            entry.style.alignItems = Align.Center;
            entry.style.marginBottom = 4;
            entry.style.paddingLeft = 12;
            entry.style.paddingRight = 12;
            entry.style.paddingTop = 8;
            entry.style.paddingBottom = 8;
            entry.style.borderTopLeftRadius = 4;
            entry.style.borderTopRightRadius = 4;
            entry.style.borderBottomLeftRadius = 4;
            entry.style.borderBottomRightRadius = 4;

            var (bgColor, iconId) = action.ToLower() switch
            {
                "send" => (new Color(0.2f, 0.35f, 0.2f, 0.6f), EditorTools.KitIcons.SEND),
                "register" => (new Color(0.2f, 0.25f, 0.35f, 0.6f), EditorTools.KitIcons.RECEIVE),
                "unregister" => (new Color(0.35f, 0.3f, 0.2f, 0.6f), EditorTools.KitIcons.WARNING),
                _ => (new Color(0.25f, 0.25f, 0.25f, 0.6f), EditorTools.KitIcons.INFO)
            };
            entry.style.backgroundColor = new StyleColor(bgColor);

            var icon = new Image { image = EditorTools.KitIcons.GetTexture(iconId) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.style.marginRight = 8;
            entry.Add(icon);

            var timeLabel = new Label(time);
            timeLabel.style.fontSize = 10;
            timeLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            timeLabel.style.width = 60;
            entry.Add(timeLabel);

            var actionLabel = new Label(action);
            actionLabel.style.fontSize = 11;
            actionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionLabel.style.width = 80;
            entry.Add(actionLabel);

            if (!string.IsNullOrEmpty(args))
            {
                var argsLabel = new Label($"Args: {args}");
                argsLabel.style.fontSize = 10;
                argsLabel.style.color = new StyleColor(new Color(0.8f, 0.9f, 0.8f));
                argsLabel.style.flexGrow = 1;
                entry.Add(argsLabel);
            }

            return entry;
        }

        #endregion

        #region 详情面板更新

        /// <summary>
        /// 更新详情面板
        /// </summary>
        private void UpdateDetailPanel()
        {
            var emptyState = mDetailPanel.Q<VisualElement>("empty-state");
            var detailContent = mDetailPanel.Q<VisualElement>("detail-content");

            if (mSelectedEvent == null)
            {
                emptyState.style.display = DisplayStyle.Flex;
                detailContent.style.display = DisplayStyle.None;
                return;
            }

            var previousSelected = mDetailTitle?.text;
            var isNewSelection = previousSelected != mSelectedEvent.EventKey;

            emptyState.style.display = DisplayStyle.None;
            detailContent.style.display = DisplayStyle.Flex;

            mDetailTitle.text = mSelectedEvent.EventKey;
            mDetailParamType.text = $"<{mSelectedEvent.ParamType ?? "void"}>";
            mDetailTriggerCount.text = $"{mSelectedEvent.TriggerCount} 次";
            mDetailListenerCount.text = $"{mSelectedEvent.ListenerCount} 个";

            // 只有切换选中事件时才清空时间轴
            if (isNewSelection)
            {
                RefreshTimeline();
            }
        }

        /// <summary>
        /// 刷新时间轴日志（从缓存恢复）
        /// </summary>
        private void RefreshTimeline()
        {
            mTimelineContainer.Clear();

            if (mSelectedEvent == null)
            {
                ShowEmptyTimelineState();
                return;
            }
            
            // 从缓存恢复历史记录
            var cacheKey = $"{mSelectedEvent.EventType}_{mSelectedEvent.EventKey}";
            if (mTimelineHistoryCache.TryGetValue(cacheKey, out var history) && history.Count > 0)
            {
                foreach (var entry in history)
                {
                    var uiEntry = CreateTimelineEntry("Send", $"{entry.Time:F2}s", entry.Args);
                    mTimelineContainer.Add(uiEntry);
                }
            }
            else
            {
                ShowEmptyTimelineState();
            }
        }
        
        /// <summary>
        /// 显示空状态提示
        /// </summary>
        private void ShowEmptyTimelineState()
        {
            var emptyState = CreateEmptyState("暂无事件记录\n选中事件后触发即可查看");
            emptyState.name = "empty-timeline-state";
            mTimelineContainer.Add(emptyState);
        }

        #endregion
    }
}
#endif
