#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 旧版查看窗口的运行时 UI 与监听器详情逻辑。
    /// </summary>
    public partial class EventKitViewerWindow
    {
        private struct EventNodeData
        {
            public string Key;
            public string DisplayName;
            public int ListenerCount;
            public EasyEvents EventsRef;
            public IEasyEvent EasyEventRef;
        }

        private struct ListenerDisplayData
        {
            public string TargetType;
            public string MethodName;
            public string FilePath;
            public int LineNumber;
            public string StackTrace;
        }

        #region 事件列表项

        /// <summary>
        /// 创建事件列表项。
        /// </summary>
        private VisualElement MakeEventItem()
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 28;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;

            var indicator = new VisualElement();
            indicator.name = "indicator";
            indicator.style.width = 8;
            indicator.style.height = 8;
            indicator.style.borderTopLeftRadius = 4;
            indicator.style.borderTopRightRadius = 4;
            indicator.style.borderBottomLeftRadius = 4;
            indicator.style.borderBottomRightRadius = 4;
            indicator.style.marginRight = 8;
            item.Add(indicator);

            var nameLabel = new Label();
            nameLabel.name = "name";
            nameLabel.style.flexGrow = 1;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(nameLabel);

            var countLabel = new Label();
            countLabel.name = "count";
            countLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            item.Add(countLabel);

            return item;
        }

        /// <summary>
        /// 绑定事件列表项数据。
        /// </summary>
        private void BindEventItem(VisualElement element, int index)
        {
            if (index < 0 || index >= mCachedNodes.Count)
            {
                return;
            }

            var node = mCachedNodes[index];

            var indicator = element.Q<VisualElement>("indicator");
            indicator.style.backgroundColor = new StyleColor(
                node.ListenerCount > 0 ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.5f, 0.5f, 0.5f));

            var nameLabel = element.Q<Label>("name");
            nameLabel.text = node.DisplayName;

            var countLabel = element.Q<Label>("count");
            countLabel.text = $"[{node.ListenerCount}]";
        }

        /// <summary>
        /// 事件列表选中回调。
        /// </summary>
        private void OnEventSelected(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is EventNodeData node)
                {
                    mSelectedEventKey = node.Key;
                    RefreshListenerData(node);
                    return;
                }
            }
        }

        #endregion

        #region 运行时视图刷新

        /// <summary>
        /// 刷新运行时视图。
        /// </summary>
        private void RefreshRuntimeView()
        {
            if (mEventListView == null)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                mEventCountLabel.text = "(0)";
                mEventListView.itemsSource = mCachedNodes;
                mEventListView.RefreshItems();

                mListenerDetailContainer.Clear();
                var hint = new Label("请进入 Play Mode 查看运行时事件注册情况");
                hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.marginTop = 50;
                mListenerDetailContainer.Add(hint);
                return;
            }

            RefreshEventData();
        }

        /// <summary>
        /// 刷新当前类别下的事件数据。
        /// </summary>
        private void RefreshEventData()
        {
            if (mEventListView == null)
            {
                return;
            }

            mCachedNodes.Clear();

            if (!EditorApplication.isPlaying)
            {
                mEventListView.itemsSource = mCachedNodes;
                mEventListView.RefreshItems();
                mEventCountLabel.text = "(0)";
                return;
            }

            switch (mSelectedCategory)
            {
                case EventCategory.Enum:
                    CollectEnumEvents();
                    break;
                case EventCategory.Type:
                    CollectTypeEvents();
                    break;
                case EventCategory.String:
                    CollectStringEvents();
                    break;
            }

            mEventCountLabel.text = $"({mCachedNodes.Count})";
            mEventListView.itemsSource = mCachedNodes;
            mEventListView.RefreshItems();

            RefreshSelectedListener();
        }

        /// <summary>
        /// 收集 Enum 事件。
        /// </summary>
        private void CollectEnumEvents()
        {
            foreach (var kvp in EventKit.Enum.GetAllEvents())
            {
                var enumName = Enum.GetName(kvp.Key.EnumType, kvp.Key.EnumValue) ?? kvp.Key.EnumValue.ToString();
                mCachedNodes.Add(new EventNodeData
                {
                    Key = $"Enum_{kvp.Key.EnumType.FullName}_{kvp.Key.EnumValue}",
                    DisplayName = $"{kvp.Key.EnumType.Name}.{enumName}",
                    ListenerCount = GetTotalListenerCount(kvp.Value),
                    EventsRef = kvp.Value
                });
            }
        }

        /// <summary>
        /// 收集 Type 事件。
        /// </summary>
        private void CollectTypeEvents()
        {
            foreach (var kvp in EventKit.Type.GetAllEvents())
            {
                mCachedNodes.Add(new EventNodeData
                {
                    Key = $"Type_{kvp.Key.FullName}",
                    DisplayName = kvp.Key.Name,
                    ListenerCount = kvp.Value.ListenerCount,
                    EasyEventRef = kvp.Value
                });
            }
        }

        /// <summary>
        /// 收集 String 事件。
        /// </summary>
        private void CollectStringEvents()
        {
#pragma warning disable CS0612, CS0618
            foreach (var kvp in EventKit.String.GetAllEvents())
#pragma warning restore CS0612, CS0618
            {
                mCachedNodes.Add(new EventNodeData
                {
                    Key = $"String_{kvp.Key}",
                    DisplayName = kvp.Key,
                    ListenerCount = GetTotalListenerCount(kvp.Value),
                    EventsRef = kvp.Value
                });
            }
        }

        /// <summary>
        /// 刷新当前选中事件的监听器列表。
        /// </summary>
        private void RefreshSelectedListener()
        {
            if (!string.IsNullOrEmpty(mSelectedEventKey))
            {
                var node = mCachedNodes.Find(n => n.Key == mSelectedEventKey);
                if (node.Key != null)
                {
                    RefreshListenerData(node);
                }
                else
                {
                    mSelectedEventKey = null;
                    mCachedListeners.Clear();
                    RefreshListenerDetailUI();
                }
            }
        }

        /// <summary>
        /// 获取复合事件容器的监听器总数。
        /// </summary>
        private static int GetTotalListenerCount(EasyEvents events)
        {
            int count = 0;
            foreach (var kvp in events.GetAllEvents())
            {
                count += kvp.Value.ListenerCount;
            }

            return count;
        }

        #endregion

        #region 监听器详情

        /// <summary>
        /// 刷新监听器详情数据。
        /// </summary>
        private void RefreshListenerData(EventNodeData node)
        {
            mCachedListeners.Clear();

            IEnumerable<Delegate> listeners = null;

            if (node.EasyEventRef != null)
            {
                listeners = node.EasyEventRef.GetListeners();
            }
            else if (node.EventsRef != null)
            {
                var list = new List<Delegate>(16);
                foreach (var kvp in node.EventsRef.GetAllEvents())
                {
                    foreach (var del in kvp.Value.GetListeners())
                    {
                        list.Add(del);
                    }
                }

                listeners = list;
            }

            if (listeners != null)
            {
                foreach (var del in listeners)
                {
                    var data = new ListenerDisplayData
                    {
                        TargetType = del.Target?.GetType().Name ?? del.Method?.DeclaringType?.Name ?? "Unknown",
                        MethodName = del.Method?.Name ?? "Unknown"
                    };

                    if (EasyEventDebugger.TryGetDebugInfo(del, out var debugInfo))
                    {
                        data.FilePath = debugInfo.FilePath;
                        data.LineNumber = debugInfo.LineNumber;
                        data.StackTrace = debugInfo.StackTrace;
                    }

                    mCachedListeners.Add(data);
                }
            }

            RefreshListenerDetailUI();
        }

        /// <summary>
        /// 刷新监听器详情 UI。
        /// </summary>
        private void RefreshListenerDetailUI()
        {
            mListenerDetailContainer.Clear();

            if (mCachedListeners.Count == 0)
            {
                var hint = new Label(string.IsNullOrEmpty(mSelectedEventKey)
                    ? "选择左侧事件查看监听器详情"
                    : "暂无监听器");
                hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.marginTop = 50;
                mListenerDetailContainer.Add(hint);
                return;
            }

            for (int i = 0; i < mCachedListeners.Count; i++)
            {
                var listenerItem = CreateListenerItem(i, mCachedListeners[i]);
                mListenerDetailContainer.Add(listenerItem);
            }
        }

        /// <summary>
        /// 创建单个监听器详情项。
        /// </summary>
        private VisualElement CreateListenerItem(int index, ListenerDisplayData data)
        {
            var item = new VisualElement();
            item.style.marginBottom = 8;
            item.style.paddingTop = 8;
            item.style.paddingBottom = 8;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 4;
            item.Add(titleRow);

            var indexLabel = new Label($"#{index + 1}");
            indexLabel.style.width = 30;
            indexLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            titleRow.Add(indexLabel);

            var nameLabel = new Label($"{data.TargetType}.{data.MethodName}");
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            titleRow.Add(nameLabel);

            if (!string.IsNullOrEmpty(data.FilePath))
            {
                item.Add(CreateLocationRow(data));
            }

            if (!string.IsNullOrEmpty(data.StackTrace))
            {
                var stackBtn = new Button(() => EditorGUIUtility.systemCopyBuffer = data.StackTrace)
                {
                    text = "复制堆栈"
                };
                stackBtn.style.height = 20;
                stackBtn.style.marginTop = 4;
                stackBtn.style.alignSelf = Align.FlexStart;
                stackBtn.tooltip = $"复制 {data.TargetType}.{data.MethodName} 的注册堆栈";
                item.Add(stackBtn);
            }

            return item;
        }

        /// <summary>
        /// 创建位置展示行。
        /// </summary>
        private VisualElement CreateLocationRow(ListenerDisplayData data)
        {
            var locationRow = new VisualElement();
            locationRow.style.flexDirection = FlexDirection.Row;
            locationRow.style.alignItems = Align.Center;

            var shortPath = data.FilePath;
            if (shortPath.Length > 40)
            {
                shortPath = "..." + shortPath[^37..];
            }

            var pathLabel = new Label($"注册位置: {shortPath}:{data.LineNumber}");
            pathLabel.style.flexGrow = 1;
            pathLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            pathLabel.style.fontSize = 11;
            locationRow.Add(pathLabel);

            var jumpBtn = new Button(() => OpenFileAtLine(data.FilePath, data.LineNumber)) { text = "跳转" };
            jumpBtn.style.height = 20;
            jumpBtn.style.paddingLeft = 8;
            jumpBtn.style.paddingRight = 8;
            locationRow.Add(jumpBtn);

            return locationRow;
        }

        #endregion
    }
}
#endif
