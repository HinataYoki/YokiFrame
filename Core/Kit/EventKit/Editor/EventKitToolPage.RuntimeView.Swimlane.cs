#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 运行时视图 - 动态泳道布局
    /// 复用代码扫描的三栏布局：发送者 -> 事件中心 -> 接收者
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 泳道字段

        private VisualElement mSwimlaneContainer;       // 泳道容器
        private VisualElement mAnimationLayer;          // 动画层（绝对定位，用于飞行脉冲）
        private readonly Dictionary<string, VisualElement> mSwimlaneRows = new(32);
        private readonly Dictionary<string, VisualElement> mEventHubs = new(32);
        private readonly Dictionary<string, VisualElement> mReceiverContainers = new(32);

        #endregion

        #region 构建泳道视图

        /// <summary>
        /// 创建泳道面板（左侧 70%）
        /// </summary>
        private VisualElement CreateSwimlanePanel()
        {
            var panel = new VisualElement();
            panel.name = "swimlane-panel";
            panel.style.flexGrow = 1;
            panel.style.position = Position.Relative;

            // 标题栏
            panel.Add(CreateSwimlaneHeader());

            // 列标题（发送方 / 事件 / 接收方）
            panel.Add(CreateSwimlaneColumnHeader());

            // 泳道滚动区域
            var scrollView = new ScrollView { style = { flexGrow = 1 } };
            panel.Add(scrollView);

            mSwimlaneContainer = new VisualElement
            {
                name = "swimlane-container",
                style = { paddingLeft = 8, paddingRight = 8, paddingTop = 8 }
            };
            scrollView.Add(mSwimlaneContainer);

            // 动画层（绝对定位，覆盖整个面板）
            mAnimationLayer = new VisualElement
            {
                name = "animation-layer",
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 }
            };
            panel.Add(mAnimationLayer);

            return panel;
        }

        /// <summary>
        /// 创建泳道标题栏
        /// </summary>
        private VisualElement CreateSwimlaneHeader()
        {
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 12, paddingRight = 12, paddingTop = 8, paddingBottom = 8,
                    backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.17f)),
                    borderBottomWidth = 1,
                    borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.22f))
                }
            };

            var title = new Label("⚡ 实时事件流")
            {
                style =
                {
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary)
                }
            };
            header.Add(title);
            header.Add(new VisualElement { style = { flexGrow = 1 } });

            var countLabel = new Label
            {
                name = "swimlane-count",
                style = { fontSize = 11, color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary) }
            };
            header.Add(countLabel);

            return header;
        }

        /// <summary>
        /// 创建列标题行
        /// </summary>
        private VisualElement CreateSwimlaneColumnHeader()
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 6, paddingBottom = 6, paddingLeft = 12, paddingRight = 12,
                    backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f)),
                    borderBottomWidth = 1,
                    borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.22f))
                }
            };

            // 左栏：发送方
            var senderHeader = new VisualElement { style = { flexGrow = 1, flexBasis = 0, alignItems = Align.FlexEnd } };
            senderHeader.Add(new Label("📤 发送方")
            {
                style = { fontSize = 11, color = new StyleColor(YokiFrameUIComponents.PulseFire) }
            });
            row.Add(senderHeader);

            // 中栏：事件
            var hubHeader = new VisualElement { style = { width = 220, alignItems = Align.Center } };
            hubHeader.Add(new Label("⚡ 事件")
            {
                style = { fontSize = 11, color = new StyleColor(YokiFrameUIComponents.Colors.TextSecondary) }
            });
            row.Add(hubHeader);

            // 右栏：接收方
            var receiverHeader = new VisualElement { style = { flexGrow = 1, flexBasis = 0, alignItems = Align.FlexStart } };
            receiverHeader.Add(new Label("📥 接收方")
            {
                style = { fontSize = 11, color = new StyleColor(YokiFrameUIComponents.PulseReceive) }
            });
            row.Add(receiverHeader);

            return row;
        }

        /// <summary>
        /// 重建泳道列表
        /// </summary>
        private void RebuildSwimlanes()
        {
            mSwimlaneContainer.Clear();
            mSwimlaneRows.Clear();
            mEventHubs.Clear();
            mReceiverContainers.Clear();

            var countLabel = mSwimlaneContainer.parent?.parent?.Q<Label>("swimlane-count");
            if (countLabel != null)
                countLabel.text = $"{mEventInfos.Count} 个活跃事件";

            if (mEventInfos.Count == 0)
            {
                mSwimlaneContainer.Add(CreateEmptyState("暂无活跃事件流"));
                return;
            }

            foreach (var info in mEventInfos)
            {
                var row = CreateSwimlaneRow(info);
                var rowKey = $"{info.EventType}_{info.EventKey}";
                mSwimlaneRows[rowKey] = row;
                mSwimlaneContainer.Add(row);
            }
        }

        #endregion
    }
}
#endif
