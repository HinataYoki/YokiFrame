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
        
        // 搜索过滤
        private TextField mRuntimeSearchField;
        private string mRuntimeSearchText = "";

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

            var titleIcon = new Image { image = KitIcons.GetTexture(KitIcons.EVENT) };
            titleIcon.style.width = 14;
            titleIcon.style.height = 14;
            titleIcon.style.marginRight = 6;
            header.Add(titleIcon);
            
            var title = new Label("实时事件流")
            {
                style =
                {
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary)
                }
            };
            header.Add(title);
            
            // 搜索框
            var searchContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginLeft = 16,
                    backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.12f)),
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    paddingLeft = 6,
                    paddingRight = 6
                }
            };
            
            var searchIcon = new Image { image = KitIcons.GetTexture(KitIcons.TARGET) };
            searchIcon.style.width = 12;
            searchIcon.style.height = 12;
            searchIcon.style.marginRight = 4;
            searchIcon.tintColor = new Color(0.5f, 0.5f, 0.55f);
            searchContainer.Add(searchIcon);
            
            mRuntimeSearchField = new TextField
            {
                style =
                {
                    width = 150,
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0
                }
            };
            mRuntimeSearchField.Q("unity-text-input").style.backgroundColor = new StyleColor(Color.clear);
            mRuntimeSearchField.Q("unity-text-input").style.borderTopWidth = 0;
            mRuntimeSearchField.Q("unity-text-input").style.borderBottomWidth = 0;
            mRuntimeSearchField.Q("unity-text-input").style.borderLeftWidth = 0;
            mRuntimeSearchField.Q("unity-text-input").style.borderRightWidth = 0;
            mRuntimeSearchField.Q("unity-text-input").style.paddingLeft = 2;
            mRuntimeSearchField.Q("unity-text-input").style.paddingRight = 2;
            mRuntimeSearchField.Q("unity-text-input").style.fontSize = 11;
            
            // 占位符提示
            var placeholder = new Label("搜索事件...")
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 22,
                    top = 3,
                    fontSize = 11,
                    color = new StyleColor(new Color(0.4f, 0.4f, 0.45f))
                }
            };
            searchContainer.Add(placeholder);
            
            mRuntimeSearchField.RegisterValueChangedCallback(evt =>
            {
                mRuntimeSearchText = evt.newValue?.ToLowerInvariant() ?? "";
                placeholder.style.display = string.IsNullOrEmpty(evt.newValue) 
                    ? DisplayStyle.Flex 
                    : DisplayStyle.None;
                ApplyRuntimeSearchFilter();
            });
            searchContainer.Add(mRuntimeSearchField);
            
            // 清除按钮
            var clearBtn = new Button(() =>
            {
                mRuntimeSearchField.value = "";
                mRuntimeSearchText = "";
                placeholder.style.display = DisplayStyle.Flex;
                ApplyRuntimeSearchFilter();
            })
            {
                text = "×",
                style =
                {
                    width = 16,
                    height = 16,
                    marginLeft = 2,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0,
                    paddingBottom = 0,
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    backgroundColor = new StyleColor(Color.clear),
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    color = new StyleColor(new Color(0.5f, 0.5f, 0.55f))
                }
            };
            clearBtn.RegisterCallback<MouseEnterEvent>(_ => clearBtn.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.85f)));
            clearBtn.RegisterCallback<MouseLeaveEvent>(_ => clearBtn.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f)));
            searchContainer.Add(clearBtn);
            
            header.Add(searchContainer);
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
        /// 应用搜索过滤
        /// </summary>
        private void ApplyRuntimeSearchFilter()
        {
            if (mSwimlaneRows.Count == 0) return;
            
            int visibleCount = 0;
            foreach (var kvp in mSwimlaneRows)
            {
                // 从 key 中提取事件名（格式：EventType_EventKey）
                var parts = kvp.Key.Split('_');
                var eventType = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";
                var eventKey = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";
                
                bool isVisible = string.IsNullOrEmpty(mRuntimeSearchText) ||
                                 eventType.Contains(mRuntimeSearchText) ||
                                 eventKey.Contains(mRuntimeSearchText);
                
                kvp.Value.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
                if (isVisible) visibleCount++;
            }
            
            // 更新计数
            var countLabel = mSwimlaneContainer?.parent?.parent?.Q<Label>("swimlane-count");
            if (countLabel != null)
            {
                if (string.IsNullOrEmpty(mRuntimeSearchText))
                    countLabel.text = $"{mEventInfos.Count} 个活跃事件";
                else
                    countLabel.text = $"{visibleCount}/{mEventInfos.Count} 个匹配";
            }
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
            var senderHeader = new VisualElement { style = { flexGrow = 1, flexBasis = 0, alignItems = Align.FlexEnd, flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
            var senderIcon = new Image { image = KitIcons.GetTexture(KitIcons.SEND) };
            senderIcon.style.width = 12;
            senderIcon.style.height = 12;
            senderIcon.style.marginRight = 4;
            senderHeader.Add(senderIcon);
            senderHeader.Add(new Label("发送方")
            {
                style = { fontSize = 11, color = new StyleColor(YokiFrameUIComponents.PulseFire) }
            });
            row.Add(senderHeader);

            // 中栏：事件
            var hubHeader = new VisualElement { style = { width = 220, alignItems = Align.Center, flexDirection = FlexDirection.Row, justifyContent = Justify.Center } };
            var eventIcon = new Image { image = KitIcons.GetTexture(KitIcons.EVENT) };
            eventIcon.style.width = 12;
            eventIcon.style.height = 12;
            eventIcon.style.marginRight = 4;
            hubHeader.Add(eventIcon);
            hubHeader.Add(new Label("事件")
            {
                style = { fontSize = 11, color = new StyleColor(YokiFrameUIComponents.Colors.TextSecondary) }
            });
            row.Add(hubHeader);

            // 右栏：接收方
            var receiverHeader = new VisualElement { style = { flexGrow = 1, flexBasis = 0, alignItems = Align.FlexStart, flexDirection = FlexDirection.Row } };
            var receiveIcon = new Image { image = KitIcons.GetTexture(KitIcons.RECEIVE) };
            receiveIcon.style.width = 12;
            receiveIcon.style.height = 12;
            receiveIcon.style.marginRight = 4;
            receiverHeader.Add(receiveIcon);
            receiverHeader.Add(new Label("接收方")
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
