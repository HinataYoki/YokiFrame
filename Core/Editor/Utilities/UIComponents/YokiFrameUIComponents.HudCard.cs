#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - HUD 卡片视图
    /// 提供指标卡片、状态 HUD 等仪表盘组件
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region HudCard

        /// <summary>
        /// HUD 卡片配置
        /// </summary>
        public class HudCardConfig
        {
            /// <summary>
            /// 卡片标题
            /// </summary>
            public string Title { get; set; }

            /// <summary>
            /// 初始值
            /// </summary>
            public string Value { get; set; } = "0";

            /// <summary>
            /// 强调色（用于数值显示）
            /// </summary>
            public Color AccentColor { get; set; } = Colors.TextSecondary;

            /// <summary>
            /// 图标 ID（可选）
            /// </summary>
            public string IconId { get; set; }

            /// <summary>
            /// 标题字号
            /// </summary>
            public int TitleFontSize { get; set; } = 10;

            /// <summary>
            /// 数值字号
            /// </summary>
            public int ValueFontSize { get; set; } = 20;

            /// <summary>
            /// 是否显示左侧强调边框
            /// </summary>
            public bool ShowAccentBorder { get; set; } = false;

            /// <summary>
            /// 卡片最小宽度
            /// </summary>
            public float MinWidth { get; set; } = 80f;
        }

        /// <summary>
        /// HUD 卡片结果
        /// </summary>
        public class HudCardResult
        {
            /// <summary>
            /// 卡片根元素
            /// </summary>
            public VisualElement Card { get; set; }

            /// <summary>
            /// 标题标签
            /// </summary>
            public Label TitleLabel { get; set; }

            /// <summary>
            /// 数值标签
            /// </summary>
            public Label ValueLabel { get; set; }

            /// <summary>
            /// 图标元素（如果有）
            /// </summary>
            public Image IconImage { get; set; }

            /// <summary>
            /// 更新数值
            /// </summary>
            /// <param name="value">新数值</param>
            public void SetValue(string value)
            {
                if (ValueLabel != null)
                    ValueLabel.text = value;
            }

            /// <summary>
            /// 更新数值和颜色
            /// </summary>
            /// <param name="value">新数值</param>
            /// <param name="color">新颜色</param>
            public void SetValue(string value, Color color)
            {
                if (ValueLabel != null)
                {
                    ValueLabel.text = value;
                    ValueLabel.style.color = new StyleColor(color);
                }
            }

            /// <summary>
            /// 更新标题
            /// </summary>
            /// <param name="title">新标题</param>
            public void SetTitle(string title)
            {
                if (TitleLabel != null)
                    TitleLabel.text = title;
            }
        }

        /// <summary>
        /// 创建 HUD 指标卡片（增强版）
        /// 支持图标、强调边框等配置
        /// </summary>
        /// <param name="config">卡片配置</param>
        /// <returns>HUD 卡片结果</returns>
        /// <example>
        /// <code>
        /// var card = YokiFrameUIComponents.CreateHudCardEx(new HudCardConfig
        /// {
        ///     Title = "Active",
        ///     Value = "0",
        ///     AccentColor = Colors.BrandWarning,
        ///     IconId = KitIcons.POOLKIT,
        ///     ShowAccentBorder = true
        /// });
        /// hudContainer.Add(card.Card);
        /// card.SetValue("42");
        /// </code>
        /// </example>
        public static HudCardResult CreateHudCardEx(HudCardConfig config)
        {
            config ??= new HudCardConfig();

            var result = new HudCardResult();

            // 卡片容器
            result.Card = new VisualElement();
            result.Card.AddToClassList("yoki-hud-card");
            result.Card.style.flexGrow = 1;
            result.Card.style.marginLeft = 4;
            result.Card.style.marginRight = 4;
            result.Card.style.paddingTop = 8;
            result.Card.style.paddingBottom = 8;
            result.Card.style.paddingLeft = 12;
            result.Card.style.paddingRight = 12;
            result.Card.style.backgroundColor = new StyleColor(Colors.LayerCard);
            result.Card.style.borderTopLeftRadius = 6;
            result.Card.style.borderTopRightRadius = 6;
            result.Card.style.borderBottomLeftRadius = 6;
            result.Card.style.borderBottomRightRadius = 6;
            result.Card.style.alignItems = Align.Center;
            result.Card.style.minWidth = config.MinWidth;

            // 强调边框
            if (config.ShowAccentBorder)
            {
                result.Card.style.borderLeftWidth = 3;
                result.Card.style.borderLeftColor = new StyleColor(config.AccentColor);
                result.Card.style.alignItems = Align.FlexStart;
            }

            // 图标
            if (!string.IsNullOrEmpty(config.IconId))
            {
                result.IconImage = new Image { image = KitIcons.GetTexture(config.IconId) };
                result.IconImage.AddToClassList("yoki-hud-card__icon");
                result.IconImage.style.width = 16;
                result.IconImage.style.height = 16;
                result.IconImage.style.marginBottom = 4;
                result.IconImage.tintColor = config.AccentColor;
                result.Card.Add(result.IconImage);
            }

            // 标题
            result.TitleLabel = new Label(config.Title);
            result.TitleLabel.AddToClassList("yoki-hud-card__title");
            result.TitleLabel.style.fontSize = config.TitleFontSize;
            result.TitleLabel.style.color = new StyleColor(Colors.TextTertiary);
            result.TitleLabel.style.marginBottom = 4;
            result.Card.Add(result.TitleLabel);

            // 数值
            result.ValueLabel = new Label(config.Value);
            result.ValueLabel.AddToClassList("yoki-hud-card__value");
            result.ValueLabel.style.fontSize = config.ValueFontSize;
            result.ValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            result.ValueLabel.style.color = new StyleColor(config.AccentColor);
            result.Card.Add(result.ValueLabel);

            return result;
        }

        /// <summary>
        /// 创建 HUD 卡片行容器
        /// 用于水平排列多个 HUD 卡片
        /// </summary>
        /// <param name="cards">卡片配置列表</param>
        /// <returns>卡片行容器和卡片结果列表</returns>
        /// <example>
        /// <code>
        /// var (container, cards) = YokiFrameUIComponents.CreateHudCardRow(new[]
        /// {
        ///     new HudCardConfig { Title = "Total", AccentColor = Colors.TextSecondary },
        ///     new HudCardConfig { Title = "Active", AccentColor = Colors.BrandWarning },
        ///     new HudCardConfig { Title = "Inactive", AccentColor = Colors.BrandSuccess },
        ///     new HudCardConfig { Title = "Peak", AccentColor = Colors.BrandPrimary }
        /// });
        /// root.Add(container);
        /// cards[1].SetValue("42");
        /// </code>
        /// </example>
        public static (VisualElement container, List<HudCardResult> cards) CreateHudCardRow(
            IEnumerable<HudCardConfig> configs)
        {
            var container = new VisualElement();
            container.AddToClassList("yoki-hud-card-row");
            container.style.flexDirection = FlexDirection.Row;
            container.style.justifyContent = Justify.SpaceAround;
            container.style.paddingTop = 12;
            container.style.paddingBottom = 12;
            container.style.paddingLeft = 12;
            container.style.paddingRight = 12;
            container.style.backgroundColor = new StyleColor(Colors.LayerSection);
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = new StyleColor(Colors.BorderLight);

            var cards = new List<HudCardResult>();
            foreach (var config in configs)
            {
                var card = CreateHudCardEx(config);
                cards.Add(card);
                container.Add(card.Card);
            }

            return (container, cards);
        }

        /// <summary>
        /// HUD 状态面板配置
        /// </summary>
        public class HudStatePanelConfig
        {
            /// <summary>
            /// 主标题（小字）
            /// </summary>
            public string Label { get; set; } = "CURRENT STATE";

            /// <summary>
            /// 主状态文本（大字）
            /// </summary>
            public string StateText { get; set; } = "—";

            /// <summary>
            /// 副标题（如上一状态）
            /// </summary>
            public string SubText { get; set; }

            /// <summary>
            /// 右侧计时器标签
            /// </summary>
            public string TimerLabel { get; set; } = "DURATION";

            /// <summary>
            /// 右侧计时器值
            /// </summary>
            public string TimerValue { get; set; } = "0.0s";

            /// <summary>
            /// 右侧状态文本
            /// </summary>
            public string StatusText { get; set; }

            /// <summary>
            /// 状态颜色
            /// </summary>
            public Color StateColor { get; set; } = Colors.BrandPrimary;
        }

        /// <summary>
        /// HUD 状态面板结果
        /// </summary>
        public class HudStatePanelResult
        {
            /// <summary>
            /// 根容器
            /// </summary>
            public VisualElement Root { get; set; }

            /// <summary>
            /// 主状态标签
            /// </summary>
            public Label StateLabel { get; set; }

            /// <summary>
            /// 副标题标签
            /// </summary>
            public Label SubTextLabel { get; set; }

            /// <summary>
            /// 计时器标签
            /// </summary>
            public Label TimerLabel { get; set; }

            /// <summary>
            /// 状态文本标签
            /// </summary>
            public Label StatusLabel { get; set; }

            /// <summary>
            /// 更新状态
            /// </summary>
            public void SetState(string state, Color? color = null)
            {
                if (StateLabel != null)
                {
                    StateLabel.text = state;
                    if (color.HasValue)
                        StateLabel.style.color = new StyleColor(color.Value);
                }
            }

            /// <summary>
            /// 更新计时器
            /// </summary>
            public void SetTimer(string value)
            {
                if (TimerLabel != null)
                    TimerLabel.text = value;
            }

            /// <summary>
            /// 更新副标题
            /// </summary>
            public void SetSubText(string text, bool visible = true)
            {
                if (SubTextLabel != null)
                {
                    SubTextLabel.text = text;
                    SubTextLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            /// <summary>
            /// 更新状态文本
            /// </summary>
            public void SetStatus(string text, Color? color = null)
            {
                if (StatusLabel != null)
                {
                    StatusLabel.text = text;
                    if (color.HasValue)
                        StatusLabel.style.color = new StyleColor(color.Value);
                }
            }
        }

        /// <summary>
        /// 创建 HUD 状态面板
        /// 用于显示当前状态大字 + 持续时间的仪表盘布局
        /// </summary>
        /// <param name="config">面板配置</param>
        /// <returns>HUD 状态面板结果</returns>
        /// <example>
        /// <code>
        /// var panel = YokiFrameUIComponents.CreateHudStatePanel(new HudStatePanelConfig
        /// {
        ///     Label = "CURRENT STATE",
        ///     StateText = "Idle",
        ///     SubText = "← Prev: Walking",
        ///     TimerValue = "2.5s",
        ///     StatusText = "Running",
        ///     StateColor = Colors.BrandSuccess
        /// });
        /// root.Add(panel.Root);
        /// panel.SetState("Running");
        /// panel.SetTimer("3.2s");
        /// </code>
        /// </example>
        public static HudStatePanelResult CreateHudStatePanel(HudStatePanelConfig config)
        {
            config ??= new HudStatePanelConfig();

            var result = new HudStatePanelResult();

            // 根容器
            result.Root = new VisualElement();
            result.Root.AddToClassList("yoki-hud-state-panel");
            result.Root.style.flexDirection = FlexDirection.Row;
            result.Root.style.paddingTop = 16;
            result.Root.style.paddingBottom = 16;
            result.Root.style.paddingLeft = 20;
            result.Root.style.paddingRight = 20;
            result.Root.style.backgroundColor = new StyleColor(Colors.LayerSection);
            result.Root.style.borderBottomWidth = 1;
            result.Root.style.borderBottomColor = new StyleColor(Colors.BorderLight);

            // 左侧：状态信息
            var leftArea = new VisualElement();
            leftArea.AddToClassList("yoki-hud-state-panel__left");
            leftArea.style.flexGrow = 1;
            result.Root.Add(leftArea);

            // 小标题
            var labelElement = new Label(config.Label);
            labelElement.AddToClassList("yoki-hud-state-panel__label");
            labelElement.style.fontSize = 10;
            labelElement.style.color = new StyleColor(Colors.TextTertiary);
            labelElement.style.marginBottom = 4;
            leftArea.Add(labelElement);

            // 主状态（大字）
            result.StateLabel = new Label(config.StateText);
            result.StateLabel.AddToClassList("yoki-hud-state-panel__state");
            result.StateLabel.style.fontSize = 24;
            result.StateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            result.StateLabel.style.color = new StyleColor(config.StateColor);
            leftArea.Add(result.StateLabel);

            // 副标题
            result.SubTextLabel = new Label(config.SubText ?? string.Empty);
            result.SubTextLabel.AddToClassList("yoki-hud-state-panel__subtext");
            result.SubTextLabel.style.fontSize = 11;
            result.SubTextLabel.style.color = new StyleColor(Colors.TextTertiary);
            result.SubTextLabel.style.marginTop = 4;
            result.SubTextLabel.style.display = string.IsNullOrEmpty(config.SubText) 
                ? DisplayStyle.None 
                : DisplayStyle.Flex;
            leftArea.Add(result.SubTextLabel);

            // 右侧：计时器
            var rightArea = new VisualElement();
            rightArea.AddToClassList("yoki-hud-state-panel__right");
            rightArea.style.alignItems = Align.FlexEnd;
            result.Root.Add(rightArea);

            // 计时器标签
            var timerLabelElement = new Label(config.TimerLabel);
            timerLabelElement.AddToClassList("yoki-hud-state-panel__timer-label");
            timerLabelElement.style.fontSize = 10;
            timerLabelElement.style.color = new StyleColor(Colors.TextTertiary);
            timerLabelElement.style.marginBottom = 4;
            rightArea.Add(timerLabelElement);

            // 计时器值
            result.TimerLabel = new Label(config.TimerValue);
            result.TimerLabel.AddToClassList("yoki-hud-state-panel__timer-value");
            result.TimerLabel.style.fontSize = 20;
            result.TimerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            result.TimerLabel.style.color = new StyleColor(Colors.TextPrimary);
            rightArea.Add(result.TimerLabel);

            // 状态文本
            if (!string.IsNullOrEmpty(config.StatusText))
            {
                result.StatusLabel = new Label(config.StatusText);
                result.StatusLabel.AddToClassList("yoki-hud-state-panel__status");
                result.StatusLabel.style.fontSize = 11;
                result.StatusLabel.style.color = new StyleColor(Colors.BrandSuccess);
                result.StatusLabel.style.marginTop = 4;
                rightArea.Add(result.StatusLabel);
            }

            return result;
        }

        #endregion
    }
}
#endif
