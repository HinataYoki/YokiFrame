#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 事件日志视图
    /// 提供时间戳、类型徽章、内容显示的标准事件日志布局
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region EventLogView

        /// <summary>
        /// 事件日志条目数据接口
        /// </summary>
        public interface IEventLogEntry
        {
            /// <summary>
            /// 时间戳（秒）
            /// </summary>
            float Timestamp { get; }

            /// <summary>
            /// 事件类型名称（用于徽章显示）
            /// </summary>
            string TypeName { get; }

            /// <summary>
            /// 事件内容/描述
            /// </summary>
            string Content { get; }

            /// <summary>
            /// 附加信息（可选，如来源、堆栈等）
            /// </summary>
            string Extra { get; }
        }

        /// <summary>
        /// 事件日志视图配置
        /// </summary>
        public class EventLogViewConfig
        {
            /// <summary>
            /// 列表项高度（默认 24）
            /// </summary>
            public float ItemHeight { get; set; } = 24f;

            /// <summary>
            /// 标题文本
            /// </summary>
            public string Title { get; set; } = "事件日志";

            /// <summary>
            /// 是否显示工具栏
            /// </summary>
            public bool ShowToolbar { get; set; } = true;

            /// <summary>
            /// 是否显示清空按钮
            /// </summary>
            public bool ShowClearButton { get; set; } = true;

            /// <summary>
            /// 时间戳列宽度
            /// </summary>
            public float TimestampWidth { get; set; } = 65f;

            /// <summary>
            /// 徽章列宽度
            /// </summary>
            public float BadgeWidth { get; set; } = 65f;

            /// <summary>
            /// 徽章颜色映射（类型名 -> 背景色, 文字色）
            /// </summary>
            public Dictionary<string, (Color bg, Color text)> BadgeColors { get; set; }

            /// <summary>
            /// 默认徽章颜色
            /// </summary>
            public (Color bg, Color text) DefaultBadgeColor { get; set; } = (
                new Color(0.25f, 0.25f, 0.28f),
                new Color(0.71f, 0.73f, 0.76f)
            );
        }

        /// <summary>
        /// 事件日志视图结果
        /// </summary>
        /// <typeparam name="T">事件条目类型</typeparam>
        public class EventLogViewResult<T> where T : IEventLogEntry
        {
            /// <summary>
            /// 根容器元素
            /// </summary>
            public VisualElement Root { get; set; }

            /// <summary>
            /// 工具栏容器（可添加自定义按钮）
            /// </summary>
            public VisualElement Toolbar { get; set; }

            /// <summary>
            /// 列表视图
            /// </summary>
            public ListView ListView { get; set; }

            /// <summary>
            /// 清空按钮（如果启用）
            /// </summary>
            public Button ClearButton { get; set; }

            /// <summary>
            /// 刷新列表数据
            /// </summary>
            /// <param name="items">数据源</param>
            public void RefreshList(IList<T> items)
            {
                ListView.itemsSource = items as System.Collections.IList;
                ListView.RefreshItems();
            }

            /// <summary>
            /// 滚动到底部（显示最新条目）
            /// </summary>
            public void ScrollToBottom()
            {
                if (ListView.itemsSource is IList<T> list && list.Count > 0)
                {
                    ListView.ScrollToItem(list.Count - 1);
                }
            }
        }

        /// <summary>
        /// 创建事件日志视图
        /// 提供时间戳、类型徽章、内容显示的标准布局
        /// </summary>
        /// <typeparam name="T">事件条目类型（需实现 IEventLogEntry）</typeparam>
        /// <param name="onClear">清空按钮回调（可选）</param>
        /// <param name="onItemClicked">条目点击回调（可选）</param>
        /// <param name="config">视图配置（可选）</param>
        /// <returns>事件日志视图结果</returns>
        /// <example>
        /// <code>
        /// var result = YokiFrameUIComponents.CreateEventLogView&lt;PoolEvent&gt;(
        ///     onClear: () => PoolDebugger.ClearEventHistory(),
        ///     onItemClicked: evt => Debug.Log(evt.StackTrace),
        ///     config: new EventLogViewConfig
        ///     {
        ///         Title = "池事件日志",
        ///         BadgeColors = new Dictionary&lt;string, (Color, Color)&gt;
        ///         {
        ///             ["Spawn"] = (new Color(0.2f, 0.45f, 0.22f), new Color(0.6f, 0.9f, 0.6f)),
        ///             ["Return"] = (new Color(0.25f, 0.25f, 0.28f), Colors.TextSecondary)
        ///         }
        ///     }
        /// );
        /// root.Add(result.Root);
        /// result.RefreshList(eventList);
        /// </code>
        /// </example>
        public static EventLogViewResult<T> CreateEventLogView<T>(
            Action onClear = null,
            Action<T> onItemClicked = null,
            EventLogViewConfig config = null) where T : IEventLogEntry
        {
            config ??= new EventLogViewConfig();

            var result = new EventLogViewResult<T>();

            // 根容器
            result.Root = new VisualElement();
            result.Root.AddToClassList("yoki-event-log");
            result.Root.style.flexGrow = 1;
            result.Root.style.flexDirection = FlexDirection.Column;
            result.Root.style.minHeight = 120;

            // 工具栏
            if (config.ShowToolbar)
            {
                result.Toolbar = BuildEventLogToolbar(config, onClear, out var clearBtn);
                result.ClearButton = clearBtn;
                result.Root.Add(result.Toolbar);
            }

            // 列表视图
            result.ListView = new ListView
            {
                fixedItemHeight = config.ItemHeight,
                makeItem = () => MakeEventLogItem(config),
                bindItem = (element, index) =>
                {
                    if (result.ListView.itemsSource is IList<T> list && index < list.Count)
                    {
                        BindEventLogItem(element, list[index], index, config, onItemClicked);
                    }
                }
            };
            result.ListView.AddToClassList("yoki-event-log__list");
            result.ListView.style.flexGrow = 1;
            result.Root.Add(result.ListView);

            return result;
        }

        /// <summary>
        /// 构建事件日志工具栏
        /// </summary>
        private static VisualElement BuildEventLogToolbar(
            EventLogViewConfig config,
            Action onClear,
            out Button clearButton)
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("yoki-event-log__toolbar");
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.height = 34;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.13f, 0.13f, 0.15f));
            toolbar.style.borderTopWidth = 1;
            toolbar.style.borderTopColor = new StyleColor(Colors.BorderLight);

            // 标题
            var title = new Label(config.Title);
            title.AddToClassList("yoki-event-log__title");
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Colors.TextPrimary);
            title.style.flexGrow = 1;
            toolbar.Add(title);

            // 清空按钮
            clearButton = null;
            if (config.ShowClearButton && onClear != null)
            {
                clearButton = new Button(onClear) { text = "清空" };
                clearButton.AddToClassList("yoki-event-log__clear-btn");
                clearButton.style.fontSize = 12;
                clearButton.style.height = 22;
                clearButton.style.paddingLeft = 10;
                clearButton.style.paddingRight = 10;
                clearButton.style.paddingTop = 2;
                clearButton.style.paddingBottom = 2;
                clearButton.style.backgroundColor = new StyleColor(Colors.LayerCard);
                clearButton.style.borderTopLeftRadius = 3;
                clearButton.style.borderTopRightRadius = 3;
                clearButton.style.borderBottomLeftRadius = 3;
                clearButton.style.borderBottomRightRadius = 3;
                toolbar.Add(clearButton);
            }

            return toolbar;
        }

        /// <summary>
        /// 创建事件日志项模板
        /// </summary>
        private static VisualElement MakeEventLogItem(EventLogViewConfig config)
        {
            var item = new VisualElement { name = "event-row" };
            item.AddToClassList("yoki-event-log__item");
            item.style.height = config.ItemHeight;
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.17f));

            // 时间戳列
            var timeLabel = new Label { name = "time" };
            timeLabel.AddToClassList("yoki-event-log__time");
            timeLabel.style.fontSize = 9;
            timeLabel.style.width = config.TimestampWidth;
            timeLabel.style.color = new StyleColor(Colors.TextTertiary);
            item.Add(timeLabel);

            // 事件类型徽章
            var badge = new Label { name = "badge" };
            badge.AddToClassList("yoki-event-log__badge");
            badge.style.fontSize = 9;
            badge.style.width = config.BadgeWidth;
            badge.style.height = 16;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.borderTopLeftRadius = 3;
            badge.style.borderTopRightRadius = 3;
            badge.style.borderBottomLeftRadius = 3;
            badge.style.borderBottomRightRadius = 3;
            badge.style.marginRight = 8;
            item.Add(badge);

            // 内容列
            var contentLabel = new Label { name = "content" };
            contentLabel.AddToClassList("yoki-event-log__content");
            contentLabel.style.fontSize = 10;
            contentLabel.style.color = new StyleColor(Colors.TextPrimary);
            contentLabel.style.flexGrow = 0.4f;
            contentLabel.style.overflow = Overflow.Hidden;
            contentLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(contentLabel);

            // 附加信息列
            var extraLabel = new Label { name = "extra" };
            extraLabel.AddToClassList("yoki-event-log__extra");
            extraLabel.style.fontSize = 10;
            extraLabel.style.color = new StyleColor(new Color(1f, 0.76f, 0.03f)); // 琥珀色
            extraLabel.style.flexGrow = 0.6f;
            extraLabel.style.overflow = Overflow.Hidden;
            extraLabel.style.textOverflow = TextOverflow.Ellipsis;
            extraLabel.pickingMode = PickingMode.Position;
            item.Add(extraLabel);

            return item;
        }

        /// <summary>
        /// 绑定事件日志项数据
        /// </summary>
        private static void BindEventLogItem<T>(
            VisualElement element,
            T entry,
            int index,
            EventLogViewConfig config,
            Action<T> onItemClicked) where T : IEventLogEntry
        {
            // 斑马纹背景
            element.style.backgroundColor = new StyleColor(GetZebraRowColor(index));

            // 时间戳
            var timeLabel = element.Q<Label>("time");
            var timeSpan = TimeSpan.FromSeconds(entry.Timestamp);
            timeLabel.text = $"[{timeSpan:mm\\:ss\\.f}]";

            // 事件类型徽章
            var badge = element.Q<Label>("badge");
            badge.text = entry.TypeName;

            var (bgColor, textColor) = config.DefaultBadgeColor;
            if (config.BadgeColors != null && config.BadgeColors.TryGetValue(entry.TypeName, out var colors))
            {
                bgColor = colors.bg;
                textColor = colors.text;
            }
            badge.style.backgroundColor = new StyleColor(bgColor);
            badge.style.color = new StyleColor(textColor);

            // 内容
            var contentLabel = element.Q<Label>("content");
            contentLabel.text = entry.Content;

            // 附加信息
            var extraLabel = element.Q<Label>("extra");
            extraLabel.text = entry.Extra ?? string.Empty;
            extraLabel.style.display = string.IsNullOrEmpty(entry.Extra) 
                ? DisplayStyle.None 
                : DisplayStyle.Flex;

            // 点击事件
            if (onItemClicked != null)
            {
                extraLabel.userData = entry;
                extraLabel.UnregisterCallback<ClickEvent>(OnEventLogItemClicked<T>);
                extraLabel.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target is Label label && label.userData is T data)
                    {
                        onItemClicked(data);
                    }
                });
            }
        }

        /// <summary>
        /// 事件日志项点击回调（占位，实际逻辑在 BindEventLogItem 中处理）
        /// </summary>
        private static void OnEventLogItemClicked<T>(ClickEvent evt) where T : IEventLogEntry
        {
            // 实际逻辑在 BindEventLogItem 中通过闭包处理
        }

        #endregion
    }
}
#endif
