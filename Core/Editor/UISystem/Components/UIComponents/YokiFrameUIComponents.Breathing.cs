#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 呼吸动画效果
    /// 实现元素透明度/边框的周期性变化，暗示"正在运行"
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 呼吸动画常量

        private const float BREATHING_CYCLE = 1.5f;     // 呼吸周期（秒）
        private const float BREATHING_MIN_ALPHA = 0.7f; // 最小透明度
        private const float BREATHING_MAX_ALPHA = 1.0f; // 最大透明度

        #endregion

        #region 呼吸状态管理

        /// <summary>
        /// 呼吸状态数据
        /// </summary>
        private class BreathingState
        {
            public VisualElement Target;
            public Color BaseColor;
            public bool IsActive;
            public BreathingMode Mode;
        }

        /// <summary>
        /// 呼吸动画模式
        /// </summary>
        public enum BreathingMode
        {
            Opacity,        // 透明度变化
            BorderGlow,     // 边框发光
            BackgroundGlow  // 背景发光
        }

        private static readonly Dictionary<int, BreathingState> sBreathingStates = new(32);

        #endregion

        #region 公共 API

        /// <summary>
        /// 注册呼吸动画
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="baseColor">基础颜色</param>
        /// <param name="mode">动画模式</param>
        public static void RegisterBreathing(
            VisualElement element, 
            Color baseColor, 
            BreathingMode mode = BreathingMode.BorderGlow)
        {
            if (element == null) return;

            var id = element.GetHashCode();
            sBreathingStates[id] = new BreathingState
            {
                Target = element,
                BaseColor = baseColor,
                IsActive = false,
                Mode = mode
            };

            // 清理回调
            element.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                sBreathingStates.Remove(id);
            });
        }

        /// <summary>
        /// 设置呼吸动画激活状态
        /// </summary>
        public static void SetBreathingActive(VisualElement element, bool active)
        {
            if (element == null) return;

            var id = element.GetHashCode();
            if (sBreathingStates.TryGetValue(id, out var state))
            {
                state.IsActive = active;
                if (!active)
                {
                    // 重置到基础状态
                    ResetBreathingVisuals(state);
                }
            }
        }

        /// <summary>
        /// 更新所有呼吸动画
        /// </summary>
        public static void UpdateAllBreathing()
        {
            var time = (float)EditorApplication.timeSinceStartup;
            
            foreach (var kvp in sBreathingStates)
            {
                var state = kvp.Value;
                if (state.IsActive && state.Target != null)
                {
                    UpdateBreathingVisuals(state, time);
                }
            }
        }

        /// <summary>
        /// 创建带呼吸效果的状态卡片
        /// </summary>
        /// <param name="stateName">状态名称</param>
        /// <param name="isActive">是否为当前激活状态</param>
        /// <param name="isVisited">是否已访问过</param>
        /// <param name="visitCount">访问次数</param>
        public static VisualElement CreateStateCard(
            string stateName, 
            bool isActive, 
            bool isVisited,
            int visitCount = 0)
        {
            var card = new VisualElement();
            card.name = "state-card";
            card.style.minWidth = 80;
            card.style.paddingLeft = Spacing.MD;
            card.style.paddingRight = Spacing.MD;
            card.style.paddingTop = Spacing.SM;
            card.style.paddingBottom = Spacing.SM;
            card.style.marginRight = Spacing.SM;
            card.style.marginBottom = Spacing.SM;
            card.style.borderTopLeftRadius = Radius.LG;
            card.style.borderTopRightRadius = Radius.LG;
            card.style.borderBottomLeftRadius = Radius.LG;
            card.style.borderBottomRightRadius = Radius.LG;
            card.style.alignItems = Align.Center;

            // 根据状态设置样式
            if (isActive)
            {
                // 当前激活状态 - 亮绿色
                card.style.backgroundColor = new StyleColor(new Color(0.2f, 0.35f, 0.2f));
                card.style.borderLeftWidth = 2;
                card.style.borderRightWidth = 2;
                card.style.borderTopWidth = 2;
                card.style.borderBottomWidth = 2;
                card.style.borderLeftColor = new StyleColor(Colors.BrandSuccess);
                card.style.borderRightColor = new StyleColor(Colors.BrandSuccess);
                card.style.borderTopColor = new StyleColor(Colors.BrandSuccess);
                card.style.borderBottomColor = new StyleColor(Colors.BrandSuccess);
                card.style.scale = new StyleScale(new Scale(new Vector3(1.05f, 1.05f, 1f)));

                // 注册呼吸动画
                RegisterBreathing(card, Colors.BrandSuccess, BreathingMode.BorderGlow);
                SetBreathingActive(card, true);
            }
            else if (isVisited)
            {
                // 已访问状态 - 深灰色
                card.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.25f));
                card.style.borderLeftWidth = 1;
                card.style.borderRightWidth = 1;
                card.style.borderTopWidth = 1;
                card.style.borderBottomWidth = 1;
                card.style.borderLeftColor = new StyleColor(Colors.BorderDefault);
                card.style.borderRightColor = new StyleColor(Colors.BorderDefault);
                card.style.borderTopColor = new StyleColor(Colors.BorderDefault);
                card.style.borderBottomColor = new StyleColor(Colors.BorderDefault);
            }
            else
            {
                // 未触达状态 - 极淡灰色
                card.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.17f));
                card.style.borderLeftWidth = 1;
                card.style.borderRightWidth = 1;
                card.style.borderTopWidth = 1;
                card.style.borderBottomWidth = 1;
                card.style.borderLeftColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
                card.style.borderRightColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
                card.style.borderTopColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
                card.style.borderBottomColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
            }

            // 状态名称
            var nameLabel = new Label(stateName);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            nameLabel.style.color = new StyleColor(isActive 
                ? Colors.TextPrimary 
                : (isVisited ? Colors.TextSecondary : Colors.TextTertiary));
            card.Add(nameLabel);

            // 访问次数（悬停显示）
            if (visitCount > 0)
            {
                var countBadge = new Label($"×{visitCount}");
                countBadge.style.fontSize = 9;
                countBadge.style.color = new StyleColor(Colors.TextTertiary);
                countBadge.style.marginTop = 2;
                card.Add(countBadge);
            }

            return card;
        }

        #endregion

        #region 内部实现

        /// <summary>
        /// 更新呼吸视觉效果
        /// </summary>
        private static void UpdateBreathingVisuals(BreathingState state, float time)
        {
            // 使用正弦波计算呼吸值 (0 ~ 1)
            var breathValue = (Mathf.Sin(time * Mathf.PI * 2f / BREATHING_CYCLE) + 1f) * 0.5f;
            var alpha = Mathf.Lerp(BREATHING_MIN_ALPHA, BREATHING_MAX_ALPHA, breathValue);

            switch (state.Mode)
            {
                case BreathingMode.Opacity:
                    state.Target.style.opacity = alpha;
                    break;

                case BreathingMode.BorderGlow:
                    var glowColor = state.BaseColor;
                    glowColor.a = alpha;
                    state.Target.style.borderLeftColor = new StyleColor(glowColor);
                    state.Target.style.borderRightColor = new StyleColor(glowColor);
                    state.Target.style.borderTopColor = new StyleColor(glowColor);
                    state.Target.style.borderBottomColor = new StyleColor(glowColor);
                    break;

                case BreathingMode.BackgroundGlow:
                    var bgColor = state.BaseColor;
                    bgColor.a = alpha * 0.3f;
                    state.Target.style.backgroundColor = new StyleColor(bgColor);
                    break;
            }
        }

        /// <summary>
        /// 重置呼吸视觉效果
        /// </summary>
        private static void ResetBreathingVisuals(BreathingState state)
        {
            switch (state.Mode)
            {
                case BreathingMode.Opacity:
                    state.Target.style.opacity = 1f;
                    break;

                case BreathingMode.BorderGlow:
                    state.Target.style.borderLeftColor = new StyleColor(state.BaseColor);
                    state.Target.style.borderRightColor = new StyleColor(state.BaseColor);
                    state.Target.style.borderTopColor = new StyleColor(state.BaseColor);
                    state.Target.style.borderBottomColor = new StyleColor(state.BaseColor);
                    break;

                case BreathingMode.BackgroundGlow:
                    var bgColor = state.BaseColor;
                    bgColor.a = 0.3f;
                    state.Target.style.backgroundColor = new StyleColor(bgColor);
                    break;
            }
        }

        #endregion
    }
}
#endif
