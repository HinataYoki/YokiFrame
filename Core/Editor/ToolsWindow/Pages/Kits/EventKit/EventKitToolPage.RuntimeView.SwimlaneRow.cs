#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 运行时视图 - 泳道行渲染与动画
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 泳道行渲染

        /// <summary>
        /// 创建泳道行
        /// </summary>
        private VisualElement CreateSwimlaneRow(EventInfo info)
        {
            var row = new VisualElement
            {
                name = $"swimlane_{info.EventType}_{info.EventKey}",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 8, paddingBottom = 8, paddingLeft = 8, paddingRight = 8,
                    marginBottom = 4,
                    backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f)),
                    borderTopLeftRadius = 4, borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                }
            };

            // 左栏：发送方
            row.Add(CreateSenderColumn(info));

            // 中栏：事件枢纽
            var hubColumn = CreateEventHubColumn(info);
            var hubKey = $"{info.EventType}_{info.EventKey}";
            mEventHubs[hubKey] = hubColumn.Q("event-hub");
            row.Add(hubColumn);

            // 右栏：接收方
            var receiverColumn = CreateReceiverColumn(info);
            mReceiverContainers[hubKey] = receiverColumn;
            row.Add(receiverColumn);

            // 点击选中
            row.RegisterCallback<ClickEvent>(_ => SelectEvent(info));

            return row;
        }

        /// <summary>
        /// 创建发送方列
        /// </summary>
        private VisualElement CreateSenderColumn(EventInfo info)
        {
            var column = new VisualElement
            {
                style = { flexGrow = 1, flexBasis = 0, alignItems = Align.FlexEnd, paddingRight = 10 }
            };

            // 最后发送者占位
            column.Add(new Label("—")
            {
                name = "sender-label",
                style = { fontSize = 11, color = new StyleColor(new Color(1f, 0.8f, 0.5f)) }
            });

            // 箭头
            var senderArrow = new Image { image = EditorTools.KitIcons.GetTexture(EditorTools.KitIcons.ARROW_RIGHT) };
            senderArrow.style.width = 16;
            senderArrow.style.height = 16;
            senderArrow.style.marginTop = 2;
            senderArrow.tintColor = YokiFrameUIComponents.PulseFire;
            column.Add(senderArrow);

            return column;
        }

        /// <summary>
        /// 创建事件枢纽列
        /// </summary>
        private VisualElement CreateEventHubColumn(EventInfo info)
        {
            var column = new VisualElement { style = { width = 220, alignItems = Align.Center } };

            // 事件卡片
            var hub = new VisualElement
            {
                name = "event-hub",
                style =
                {
                    backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.28f)),
                    paddingLeft = 16, paddingRight = 16, paddingTop = 8, paddingBottom = 8,
                    borderTopLeftRadius = 6, borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6, borderBottomRightRadius = 6,
                    alignItems = Align.Center,
                    minWidth = 160,
                    borderLeftWidth = 1, borderRightWidth = 1, borderTopWidth = 1, borderBottomWidth = 1,
                    borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.35f)),
                    borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.35f)),
                    borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.35f)),
                    borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.35f))
                }
            };
            column.Add(hub);

            // 类型标签
            hub.Add(new Label(info.EventType)
            {
                style = { fontSize = 9, color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary) }
            });

            // 事件名
            hub.Add(new Label(info.EventKey)
            {
                style =
                {
                    fontSize = 12,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary)
                }
            });

            // 触发次数
            hub.Add(new Label($"×{info.TriggerCount}")
            {
                name = "trigger-count",
                style = { fontSize = 10, color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary), marginTop = 2 }
            });

            return column;
        }

        /// <summary>
        /// 创建接收方列
        /// </summary>
        private VisualElement CreateReceiverColumn(EventInfo info)
        {
            var column = new VisualElement
            {
                name = "receiver-column",
                style = { flexGrow = 1, flexBasis = 0, alignItems = Align.FlexStart, paddingLeft = 10 }
            };

            // 箭头
            var receiverArrow = new Image { image = EditorTools.KitIcons.GetTexture(EditorTools.KitIcons.ARROW_RIGHT) };
            receiverArrow.style.width = 16;
            receiverArrow.style.height = 16;
            receiverArrow.tintColor = YokiFrameUIComponents.PulseReceive;
            column.Add(receiverArrow);

            // 监听者数量容器
            var listenerRow = new VisualElement
            {
                name = "listener-row",
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 }
            };
            column.Add(listenerRow);

            // 监听者图标
            var listenerIcon = new Image { image = EditorTools.KitIcons.GetTexture(EditorTools.KitIcons.LISTENER) };
            listenerIcon.style.width = 12;
            listenerIcon.style.height = 12;
            listenerIcon.style.marginRight = 4;
            listenerRow.Add(listenerIcon);

            // 监听者数量
            listenerRow.Add(new Label($"{info.ListenerCount}")
            {
                name = "listener-count",
                style = { fontSize = 11, color = new StyleColor(new Color(0.6f, 0.9f, 0.7f)) }
            });

            return column;
        }

        #endregion

        #region 泳道动画

        /// <summary>
        /// 播放事件触发动画（发送脉冲 -> 事件中心 -> 接收扩散）
        /// </summary>
        private void PlayEventTriggerAnimation(string eventType, string eventKey)
        {
            var rowKey = $"{eventType}_{eventKey}";
            
            if (!mSwimlaneRows.TryGetValue(rowKey, out var row)) return;
            if (!mEventHubs.TryGetValue(rowKey, out var hub)) return;
            if (!mReceiverContainers.TryGetValue(rowKey, out var receiverColumn)) return;

            // 获取发送方列（第一个子元素）
            var senderColumn = row.ElementAt(0);
            if (senderColumn == null) return;

            // 阶段 1：发送脉冲（黄色，从发送方到事件中心）
            YokiFrameUIComponents.PlayFlightPulse(
                mAnimationLayer,
                senderColumn,
                hub,
                YokiFrameUIComponents.PulseFire,
                () =>
                {
                    // 事件中心高亮
                    YokiFrameUIComponents.PlayHighlightFlash(hub, YokiFrameUIComponents.PulseFire);

                    // 阶段 2：接收扩散（绿色，从事件中心到接收方）
                    YokiFrameUIComponents.PlayFlightPulse(
                        mAnimationLayer,
                        hub,
                        receiverColumn,
                        YokiFrameUIComponents.PulseReceive,
                        () =>
                        {
                            // 接收方高亮
                            YokiFrameUIComponents.PlayHighlightFlash(receiverColumn, YokiFrameUIComponents.PulseReceive);
                        });
                });
        }

        /// <summary>
        /// 更新泳道活跃状态（更新触发次数显示等）
        /// </summary>
        private void UpdateActivityStates()
        {
            foreach (var info in mEventInfos)
            {
                var rowKey = $"{info.EventType}_{info.EventKey}";
                
                // 更新事件中心的触发次数
                if (mEventHubs.TryGetValue(rowKey, out var hub))
                {
                    var countLabel = hub.Q<Label>("trigger-count");
                    if (countLabel != null)
                        countLabel.text = $"×{info.TriggerCount}";
                }
                
                // 更新接收方的监听者数量
                if (mReceiverContainers.TryGetValue(rowKey, out var receiverColumn))
                {
                    var listenerLabel = receiverColumn.Q<Label>("listener-count");
                    if (listenerLabel != null)
                        listenerLabel.text = $"{info.ListenerCount}";
                }
            }
        }

        #endregion
    }
}
#endif
