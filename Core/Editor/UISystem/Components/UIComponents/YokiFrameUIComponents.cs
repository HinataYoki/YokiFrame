#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 现代化 UI 组件工厂
    /// 提供统一风格的 UI 组件创建方法
    /// 使用 partial class 按功能模块拆分
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 设计令牌常量

        /// <summary>
        /// 颜色常量 - 统一的设计系统配色
        /// </summary>
        public static class Colors
        {
            // 品牌色
            public static readonly Color BrandPrimary = new(0.13f, 0.59f, 0.95f);      // #2196F3
            public static readonly Color BrandPrimaryHover = new(0.26f, 0.65f, 0.96f);
            public static readonly Color BrandSuccess = new(0.30f, 0.69f, 0.31f);       // #4CAF50
            public static readonly Color BrandDanger = new(0.96f, 0.26f, 0.21f);        // #F44336
            public static readonly Color BrandWarning = new(1f, 0.60f, 0f);             // #FF9800
            
            // 层级背景色
            public static readonly Color LayerCard = new(0.18f, 0.18f, 0.21f);
            public static readonly Color LayerElevated = new(0.20f, 0.22f, 0.24f);
            public static readonly Color LayerHover = new(0.23f, 0.24f, 0.27f);
            public static readonly Color LayerToolbar = new(0.15f, 0.15f, 0.15f);
            public static readonly Color LayerFilterBar = new(0.13f, 0.13f, 0.13f);
            public static readonly Color LayerTabBar = new(0.12f, 0.12f, 0.12f);
            public static readonly Color LayerSection = new(0.18f, 0.18f, 0.18f);
            
            // 文本色
            public static readonly Color TextPrimary = new(0.94f, 0.94f, 0.96f);
            public static readonly Color TextSecondary = new(0.71f, 0.73f, 0.76f);
            public static readonly Color TextTertiary = new(0.51f, 0.53f, 0.57f);
            
            // 边框色
            public static readonly Color BorderDefault = new(0.22f, 0.23f, 0.25f);
            public static readonly Color BorderLight = new(0.2f, 0.2f, 0.2f);
            
            // 状态颜色（用于状态指示器）
            public static readonly Color StatusSuccess = new(0.30f, 0.69f, 0.31f);
            public static readonly Color StatusWarning = new(1f, 0.60f, 0f);
            public static readonly Color StatusError = new(0.96f, 0.26f, 0.21f);
            public static readonly Color StatusInfo = new(0.13f, 0.59f, 0.95f);
            
            // 徽章颜色
            public static readonly Color BadgeDefault = new(0.25f, 0.25f, 0.28f);
            public static readonly Color BadgeSuccess = new(0.20f, 0.45f, 0.22f);
            public static readonly Color BadgeWarning = new(0.50f, 0.35f, 0.10f);
            public static readonly Color BadgeError = new(0.50f, 0.20f, 0.18f);
            public static readonly Color BadgeInfo = new(0.15f, 0.35f, 0.55f);
            
            // 文件状态颜色
            public static readonly Color FileExists = new(0.30f, 0.69f, 0.31f);
            public static readonly Color FileNotExists = new(0.71f, 0.73f, 0.76f);
        }

        /// <summary>
        /// 间距常量
        /// </summary>
        public static class Spacing
        {
            public const float XS = 4f;
            public const float SM = 8f;
            public const float MD = 12f;
            public const float LG = 16f;
            public const float XL = 20f;
        }

        /// <summary>
        /// 圆角常量
        /// </summary>
        public static class Radius
        {
            public const float SM = 3f;
            public const float MD = 4f;
            public const float LG = 6f;
        }

        #endregion

        #region 幽灵按钮

        /// <summary>
        /// 创建幽灵按钮（仅边框，hover 显示背景）
        /// </summary>
        /// <param name="name">按钮名称</param>
        /// <param name="text">按钮文本</param>
        /// <param name="onClick">点击回调</param>
        /// <param name="isDanger">是否为危险操作（红色边框）</param>
        /// <returns>幽灵按钮</returns>
        public static Button CreateGhostButton(string name, string text, Action onClick = null, bool isDanger = false)
        {
            var btn = new Button(onClick) { name = name, text = text };
            btn.style.fontSize = 9;
            btn.style.width = 24;
            btn.style.height = 20;
            btn.style.marginLeft = 4;
            btn.style.paddingLeft = btn.style.paddingRight = 0;
            btn.style.paddingTop = btn.style.paddingBottom = 0;
            btn.style.backgroundColor = new StyleColor(Color.clear);
            btn.style.borderLeftWidth = btn.style.borderRightWidth =
                btn.style.borderTopWidth = btn.style.borderBottomWidth = 1;

            var borderColor = isDanger ? Colors.BrandDanger : Colors.TextTertiary;
            var textColor = isDanger ? Colors.BrandDanger : Colors.TextSecondary;
            btn.style.borderLeftColor = btn.style.borderRightColor =
                btn.style.borderTopColor = btn.style.borderBottomColor = new StyleColor(borderColor);
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
                btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 3;
            btn.style.color = new StyleColor(textColor);

            // Hover 效果
            btn.RegisterCallback<MouseEnterEvent>(evt =>
            {
                btn.style.backgroundColor = new StyleColor(Colors.LayerHover);
            });
            btn.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                btn.style.backgroundColor = new StyleColor(Color.clear);
            });

            return btn;
        }

        #endregion

        #region 过滤按钮

        /// <summary>
        /// 创建过滤按钮（toggle 风格，激活时高亮）
        /// </summary>
        /// <param name="text">按钮文本</param>
        /// <param name="isActive">是否激活</param>
        /// <param name="onClick">点击回调</param>
        /// <returns>过滤按钮</returns>
        public static Button CreateFilterButton(string text, bool isActive, Action onClick)
        {
            var btn = new Button(onClick)
            {
                text = text,
                style =
                {
                    fontSize = 10,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 2,
                    paddingBottom = 2,
                    marginRight = 2,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    backgroundColor = new StyleColor(isActive ? Colors.BrandPrimary : Colors.LayerCard)
                }
            };
            return btn;
        }

        /// <summary>
        /// 设置过滤按钮激活状态
        /// </summary>
        /// <param name="btn">按钮</param>
        /// <param name="isActive">是否激活</param>
        public static void SetFilterButtonActive(Button btn, bool isActive)
        {
            if (btn == default) return;
            btn.style.backgroundColor = new StyleColor(isActive ? Colors.BrandPrimary : Colors.LayerCard);
        }

        #endregion

        #region HUD 卡片

        /// <summary>
        /// 创建 HUD 指标卡片
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="value">数值</param>
        /// <param name="accentColor">强调色</param>
        /// <returns>卡片元素和数值标签</returns>
        public static (VisualElement card, Label valueLabel) CreateHudCard(string title, string value, Color accentColor)
        {
            var card = new VisualElement();
            card.style.flexGrow = 1;
            card.style.marginLeft = card.style.marginRight = 4;
            card.style.paddingTop = card.style.paddingBottom = 8;
            card.style.paddingLeft = card.style.paddingRight = 12;
            card.style.backgroundColor = new StyleColor(Colors.LayerCard);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 6;
            card.style.alignItems = Align.Center;

            // 标题
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 10;
            titleLabel.style.color = new StyleColor(Colors.TextTertiary);
            titleLabel.style.marginBottom = 4;
            card.Add(titleLabel);

            // 数值
            var valueLabel = new Label(value);
            valueLabel.style.fontSize = 20;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = new StyleColor(accentColor);
            card.Add(valueLabel);

            return (card, valueLabel);
        }

        #endregion

        #region 表格组件

        /// <summary>
        /// 创建表格头部列
        /// </summary>
        /// <param name="text">列标题</param>
        /// <param name="width">固定宽度（0 表示自动填充）</param>
        /// <param name="textAlign">文本对齐</param>
        /// <returns>表头标签</returns>
        public static Label CreateTableHeaderColumn(string text, float width = 0, TextAnchor textAlign = TextAnchor.MiddleLeft)
        {
            var label = new Label(text)
            {
                style =
                {
                    fontSize = 10,
                    color = new StyleColor(Colors.TextTertiary)
                }
            };

            if (width > 0)
            {
                label.style.width = width;
                label.style.flexShrink = 0;
            }
            else
            {
                label.style.flexGrow = 1;
                label.style.flexShrink = 1;
            }

            label.style.unityTextAlign = textAlign;
            return label;
        }

        /// <summary>
        /// 创建表格头部容器
        /// </summary>
        /// <param name="height">高度</param>
        /// <returns>表头容器</returns>
        public static VisualElement CreateTableHeader(float height = 22f)
        {
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = height,
                    paddingLeft = 8,
                    paddingRight = 8,
                    backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f)),
                    borderBottomWidth = 1,
                    borderBottomColor = new StyleColor(Colors.BorderLight)
                }
            };
            return header;
        }

        /// <summary>
        /// 获取斑马纹背景色
        /// </summary>
        /// <param name="index">行索引</param>
        /// <returns>背景色</returns>
        public static Color GetZebraRowColor(int index)
        {
            return index % 2 == 0
                ? new Color(0.14f, 0.14f, 0.16f)
                : new Color(0.16f, 0.16f, 0.18f);
        }

        #endregion

        #region 堆栈解析工具

        /// <summary>
        /// 解析堆栈追踪获取调用来源
        /// 过滤系统和框架相关的堆栈帧
        /// </summary>
        /// <param name="stackTrace">堆栈追踪字符串</param>
        /// <param name="additionalFilters">额外的过滤关键词</param>
        /// <returns>调用来源方法名</returns>
        public static string ParseStackTraceSource(string stackTrace, params string[] additionalFilters)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "Unknown";

            var lines = stackTrace.Split('\n');
            foreach (var line in lines)
            {
                // 跳过系统和框架相关的堆栈帧
                if (line.Contains("System.Environment")) continue;
                if (line.Contains("UnityEngine.")) continue;
                if (line.Contains("UnityEditor.")) continue;

                // 检查额外的过滤关键词
                var shouldSkip = false;
                foreach (var filter in additionalFilters)
                {
                    if (line.Contains(filter))
                    {
                        shouldSkip = true;
                        break;
                    }
                }
                if (shouldSkip) continue;

                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.StartsWith("at "))
                {
                    trimmed = trimmed.Substring(3);
                }

                // 提取方法名（去掉参数列表）
                var parenIndex = trimmed.IndexOf('(');
                if (parenIndex > 0)
                {
                    trimmed = trimmed.Substring(0, parenIndex);
                }

                // 确保是有效的方法名（包含类名.方法名格式）
                if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > 3 && trimmed.Contains("."))
                {
                    return trimmed;
                }
            }

            return "Unknown";
        }

        #endregion
    }
}
#endif
