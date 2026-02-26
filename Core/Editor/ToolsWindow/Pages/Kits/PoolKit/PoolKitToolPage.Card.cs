#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKitToolPage - 活跃对象卡片创建
    /// </summary>
    public partial class PoolKitToolPage
    {
        #region 卡片常量

        private const float CARD_HEADER_HEIGHT = 42f;
        private const float ACTION_BUTTON_WIDTH = 56f;
        private const float ACTION_BUTTON_HEIGHT = 24f;
        private const float LONG_USAGE_WARNING_THRESHOLD = 60f; // 60 秒未回收视为长时间占用

        #endregion

        #region 卡片创建

        private VisualElement CreateActiveObjectCard(ActiveObjectInfo info, int index)
        {
            var cardId = info.GetHashCode();
            var isExpanded = mExpandedCards.Contains(cardId);
            
            // 计算使用时长
            var usageDuration = Time.realtimeSinceStartup - info.SpawnTime;
            var isLongUsage = usageDuration > LONG_USAGE_WARNING_THRESHOLD;

            var card = new VisualElement { name = $"card-{index}", userData = info };
            card.style.marginLeft = card.style.marginRight = 6;
            card.style.marginTop = card.style.marginBottom = 3;
            card.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerCard);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 6;
            card.style.borderLeftWidth = card.style.borderRightWidth =
                card.style.borderTopWidth = card.style.borderBottomWidth = 1;
            
            // 长时间占用时使用警告色边框
            var borderColor = isLongUsage 
                ? YokiFrameUIComponents.Colors.BrandWarning 
                : YokiFrameUIComponents.Colors.BorderDefault;
            card.style.borderLeftColor = card.style.borderRightColor =
                card.style.borderTopColor = card.style.borderBottomColor = new StyleColor(borderColor);

            var header = CreateCardHeader(info, cardId, isExpanded, usageDuration, isLongUsage);
            card.Add(header);

            var stackContent = CreateStackContent(info);
            stackContent.name = "stack-content";
            stackContent.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(stackContent);

            return card;
        }

        private VisualElement CreateCardHeader(ActiveObjectInfo info, int cardId, bool isExpanded, float usageDuration, bool isLongUsage)
        {
            var header = new VisualElement
            {
                name = "card-header", // 添加 name 以便后续查找
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = CARD_HEADER_HEIGHT,
                    paddingLeft = 8,
                    paddingRight = 8
                }
            };
            header.pickingMode = PickingMode.Position;

            var arrow = new Label(isExpanded ? "v" : ">")
            {
                name = "arrow",
                style =
                {
                    fontSize = 10,
                    width = 16,
                    color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary),
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            header.Add(arrow);

            // 左侧信息区域
            var infoArea = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column,
                    justifyContent = Justify.Center
                }
            };
            header.Add(infoArea);

            // 对象名
            var objName = GetObjectDisplayName(info.Obj);
            var nameLabel = new Label(objName)
            {
                style =
                {
                    fontSize = 12,
                    color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary),
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis
                }
            };
            infoArea.Add(nameLabel);

            // 第二行：调用来源 + 使用时长
            var secondRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 2
                }
            };
            infoArea.Add(secondRow);

            // 调用来源
            var source = YokiFrameUIComponents.ParseStackTraceSource(
                info.StackTrace, "PoolDebugger", "PoolKit", "SafePoolKit", "SimplePoolKit");
            var sourceLabel = new Label(source)
            {
                style =
                {
                    fontSize = 10,
                    color = new StyleColor(new Color(1f, 0.76f, 0.03f)),
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    flexShrink = 1
                }
            };
            secondRow.Add(sourceLabel);

            // 使用时长标签
            var durationText = FormatDuration(usageDuration);
            var durationColor = isLongUsage 
                ? YokiFrameUIComponents.Colors.BrandWarning 
                : YokiFrameUIComponents.Colors.TextTertiary;
            
            var durationLabel = new Label($" | {durationText}")
            {
                name = "duration-label", // 添加 name 以便后续更新
                style =
                {
                    fontSize = 10,
                    color = new StyleColor(durationColor),
                    marginLeft = 4,
                    flexShrink = 0
                }
            };
            durationLabel.tooltip = isLongUsage 
                ? $"警告：对象已使用 {durationText}，可能存在泄漏" 
                : $"使用时长：{durationText}";
            secondRow.Add(durationLabel);

            // 右侧操作按钮区域
            var buttonArea = CreateCardButtonArea(info);
            header.Add(buttonArea);

            header.RegisterCallback<ClickEvent>(evt =>
            {
                // 检查点击目标或其父级是否为按钮
                var target = evt.target as VisualElement;
                while (target != default && target != header)
                {
                    if (target is Button) return;
                    target = target.parent;
                }
                ToggleCardExpansion(header.parent, cardId);
            });

            return header;
        }

        private VisualElement CreateCardButtonArea(ActiveObjectInfo info)
        {
            var buttonArea = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginLeft = 8
                }
            };

            var gotoBtn = CreateActionButton(KitIcons.CODE, "代码", () => OnGotoSourceCode(info));
            gotoBtn.tooltip = "跳转到借出代码位置";
            buttonArea.Add(gotoBtn);

            return buttonArea;
        }

        /// <summary>
        /// 创建带图标和文字的操作按钮
        /// </summary>
        private static Button CreateActionButton(string iconId, string text, Action onClick, bool isDanger = false)
        {
            var btn = new Button(onClick)
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    width = ACTION_BUTTON_WIDTH,
                    height = ACTION_BUTTON_HEIGHT,
                    marginLeft = 4,
                    paddingLeft = 6,
                    paddingRight = 6,
                    backgroundColor = new StyleColor(Color.clear),
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            var borderColor = isDanger ? YokiFrameUIComponents.Colors.BrandDanger : YokiFrameUIComponents.Colors.TextTertiary;
            btn.style.borderLeftColor = btn.style.borderRightColor =
                btn.style.borderTopColor = btn.style.borderBottomColor = new StyleColor(borderColor);

            var icon = new Image { image = KitIcons.GetTexture(iconId) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.style.marginRight = 4;
            icon.tintColor = isDanger ? YokiFrameUIComponents.Colors.BrandDanger : YokiFrameUIComponents.Colors.TextSecondary;
            btn.Add(icon);

            var label = new Label(text)
            {
                style =
                {
                    fontSize = 10,
                    color = new StyleColor(isDanger ? YokiFrameUIComponents.Colors.BrandDanger : YokiFrameUIComponents.Colors.TextSecondary)
                }
            };
            btn.Add(label);

            btn.RegisterCallback<MouseEnterEvent>(static evt =>
            {
                if (evt.target is Button b)
                    b.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerHover);
            });
            btn.RegisterCallback<MouseLeaveEvent>(static evt =>
            {
                if (evt.target is Button b)
                    b.style.backgroundColor = new StyleColor(Color.clear);
            });

            return btn;
        }

        /// <summary>
        /// 格式化时长显示
        /// </summary>
        private static string FormatDuration(float seconds)
        {
            if (seconds < 60f)
                return $"{seconds:F1}s";
            if (seconds < 3600f)
                return $"{(int)(seconds / 60)}m {(int)(seconds % 60)}s";
            return $"{(int)(seconds / 3600)}h {(int)((seconds % 3600) / 60)}m";
        }

        #endregion
        // 堆栈 UI 构建已移至 PoolKitToolPage.StackTrace.cs
    }
}
#endif