#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 脉冲动画效果
    /// 实现从左到右的冲刺效果，然后颜色渐退
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 常量

        private const float PULSE_RUSH_DURATION = 0.15f;    // 冲刺持续时间（秒）
        private const float PULSE_FADE_DURATION = 1.5f;     // 渐退持续时间（秒）
        private const float PULSE_TOTAL_DURATION = PULSE_RUSH_DURATION + PULSE_FADE_DURATION;

        #endregion

        #region 脉冲状态管理

        /// <summary>
        /// 脉冲状态数据
        /// </summary>
        private class PulseState
        {
            public double TriggerTime;      // 触发时间
            public VisualElement Strip;     // 脉冲条元素
            public VisualElement Trail;     // 拖尾元素
            public Color ActiveColor;       // 激活颜色
            public Color IdleColor;         // 闲置颜色
        }

        private static readonly Dictionary<int, PulseState> sPulseStates = new(64);

        #endregion

        #region 公共 API

        /// <summary>
        /// 创建脉冲条容器（包含脉冲条和拖尾）
        /// </summary>
        /// <param name="activeColor">激活时的颜色（默认红色）</param>
        /// <param name="idleColor">闲置时的颜色（默认深灰）</param>
        /// <returns>脉冲条容器</returns>
        public static VisualElement CreatePulseStrip(
            Color? activeColor = null, 
            Color? idleColor = null)
        {
            var container = new VisualElement();
            container.name = "pulse-container";
            container.style.position = Position.Absolute;
            container.style.left = 0;
            container.style.top = 0;
            container.style.bottom = 0;
            container.style.right = 0;
            container.style.overflow = Overflow.Hidden;
            container.pickingMode = PickingMode.Ignore;

            // 拖尾（渐变背景）
            var trail = new VisualElement();
            trail.name = "pulse-trail";
            trail.style.position = Position.Absolute;
            trail.style.left = 0;
            trail.style.top = 0;
            trail.style.bottom = 0;
            trail.style.width = 0; // 初始宽度为 0
            trail.pickingMode = PickingMode.Ignore;
            container.Add(trail);

            // 脉冲条（前端亮色）
            var strip = new VisualElement();
            strip.name = "pulse-strip";
            strip.style.position = Position.Absolute;
            strip.style.left = 0;
            strip.style.top = 0;
            strip.style.bottom = 0;
            strip.style.width = 4;
            strip.pickingMode = PickingMode.Ignore;
            container.Add(strip);

            // 初始化状态
            var state = new PulseState
            {
                TriggerTime = 0,
                Strip = strip,
                Trail = trail,
                ActiveColor = activeColor ?? new Color(1f, 0.3f, 0.3f),  // 亮红
                IdleColor = idleColor ?? new Color(0.2f, 0.2f, 0.2f)     // 深灰
            };

            // 设置初始颜色
            strip.style.backgroundColor = new StyleColor(state.IdleColor);
            trail.style.backgroundColor = new StyleColor(Color.clear);

            // 注册状态
            var id = container.GetHashCode();
            sPulseStates[id] = state;

            // 清理回调
            container.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                sPulseStates.Remove(id);
            });

            return container;
        }

        /// <summary>
        /// 触发脉冲动画
        /// </summary>
        /// <param name="pulseContainer">脉冲条容器（由 CreatePulseStrip 创建）</param>
        public static void TriggerPulse(VisualElement pulseContainer)
        {
            if (pulseContainer == null) return;

            var id = pulseContainer.GetHashCode();
            if (!sPulseStates.TryGetValue(id, out var state)) return;

            state.TriggerTime = EditorApplication.timeSinceStartup;
        }

        /// <summary>
        /// 更新脉冲动画状态（在 OnUpdate 中调用）
        /// </summary>
        /// <param name="pulseContainer">脉冲条容器</param>
        /// <param name="lastTriggerTime">最后触发时间（EditorApplication.timeSinceStartup）</param>
        public static void UpdatePulse(VisualElement pulseContainer, double lastTriggerTime)
        {
            if (pulseContainer == null) return;

            var id = pulseContainer.GetHashCode();
            if (!sPulseStates.TryGetValue(id, out var state)) return;

            // 使用传入的触发时间更新状态
            if (lastTriggerTime > state.TriggerTime)
            {
                state.TriggerTime = lastTriggerTime;
            }

            UpdatePulseVisuals(state, pulseContainer);
        }

        /// <summary>
        /// 批量更新所有脉冲动画
        /// </summary>
        public static void UpdateAllPulses()
        {
            foreach (var kvp in sPulseStates)
            {
                var state = kvp.Value;
                if (state.Strip?.parent?.parent != null)
                {
                    UpdatePulseVisuals(state, state.Strip.parent);
                }
            }
        }

        #endregion

        #region 内部实现

        /// <summary>
        /// 更新脉冲视觉效果
        /// </summary>
        private static void UpdatePulseVisuals(PulseState state, VisualElement container)
        {
            var now = EditorApplication.timeSinceStartup;
            var elapsed = (float)(now - state.TriggerTime);

            if (elapsed < 0 || state.TriggerTime <= 0)
            {
                // 未触发状态
                SetIdleState(state);
                return;
            }

            if (elapsed > PULSE_TOTAL_DURATION)
            {
                // 动画结束
                SetIdleState(state);
                return;
            }

            var containerWidth = container.resolvedStyle.width;
            if (float.IsNaN(containerWidth) || containerWidth <= 0)
                containerWidth = 300; // 默认宽度

            if (elapsed < PULSE_RUSH_DURATION)
            {
                // 冲刺阶段：脉冲条从左到右快速移动
                var rushProgress = elapsed / PULSE_RUSH_DURATION;
                var easeProgress = EaseOutQuad(rushProgress);
                
                // 脉冲条位置
                var stripX = easeProgress * containerWidth;
                state.Strip.style.left = stripX;
                state.Strip.style.backgroundColor = new StyleColor(state.ActiveColor);

                // 拖尾宽度跟随脉冲条
                state.Trail.style.width = stripX;
                var trailColor = state.ActiveColor;
                trailColor.a = 0.4f * (1f - rushProgress * 0.5f);
                state.Trail.style.backgroundColor = new StyleColor(trailColor);
            }
            else
            {
                // 渐退阶段：脉冲条停在右边，拖尾颜色渐退
                var fadeElapsed = elapsed - PULSE_RUSH_DURATION;
                var fadeProgress = fadeElapsed / PULSE_FADE_DURATION;
                var easeFade = EaseOutQuad(fadeProgress);

                // 脉冲条保持在右边，颜色渐退
                state.Strip.style.left = containerWidth;
                var stripColor = Color.Lerp(state.ActiveColor, state.IdleColor, easeFade);
                state.Strip.style.backgroundColor = new StyleColor(stripColor);

                // 拖尾颜色渐退
                state.Trail.style.width = containerWidth;
                var trailColor = state.ActiveColor;
                trailColor.a = 0.3f * (1f - easeFade);
                state.Trail.style.backgroundColor = new StyleColor(trailColor);
            }
        }

        /// <summary>
        /// 设置闲置状态
        /// </summary>
        private static void SetIdleState(PulseState state)
        {
            state.Strip.style.left = 0;
            state.Strip.style.backgroundColor = new StyleColor(state.IdleColor);
            state.Trail.style.width = 0;
            state.Trail.style.backgroundColor = new StyleColor(Color.clear);
        }

        /// <summary>
        /// 缓出二次方缓动函数
        /// </summary>
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        #endregion
    }
}
#endif
