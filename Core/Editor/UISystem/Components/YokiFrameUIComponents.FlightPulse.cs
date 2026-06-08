#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 飞行脉冲动画
    /// 实现从起点到终点的光点飞行效果，支持对象池复用
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 飞行脉冲常量

        private const float FLIGHT_DURATION = 0.35f;        // 飞行时长（秒）- 稍慢更明显
        private const float GLOW_SIZE = 14f;                // 光点大小 - 更大
        private const float TRAIL_COUNT = 3;                // 拖尾数量
        private const int POOL_INITIAL_SIZE = 16;           // 对象池初始大小

        // 预定义颜色
        public static readonly Color PulseFire = new(1f, 0.76f, 0.03f);     // #FFC107 黄色（发送）
        public static readonly Color PulseReceive = new(0.30f, 0.69f, 0.31f); // #4CAF50 绿色（接收）
        public static readonly Color PulseUnregister = new(0.13f, 0.59f, 0.95f); // #2196F3 蓝色（注销）

        #endregion

        #region 飞行脉冲状态

        /// <summary>
        /// 飞行脉冲状态
        /// </summary>
        private class FlightPulseState
        {
            public VisualElement Glow;          // 主光点元素
            public VisualElement[] Trails;      // 拖尾元素
            public VisualElement Container;     // 容器（用于坐标转换）
            public Vector2 StartPos;            // 起点位置
            public Vector2 EndPos;              // 终点位置
            public double StartTime;            // 开始时间
            public Color GlowColor;             // 光点颜色
            public bool IsActive;               // 是否激活
            public System.Action OnComplete;    // 完成回调
        }

        // 对象池
        private static readonly List<FlightPulseState> sFlightPool = new(POOL_INITIAL_SIZE);
        private static readonly List<FlightPulseState> sActiveFlights = new(32);

        #endregion

        #region 公共 API

        /// <summary>
        /// 播放飞行脉冲动画
        /// </summary>
        /// <param name="container">动画容器（需要设置 position: relative）</param>
        /// <param name="startElement">起点元素</param>
        /// <param name="endElement">终点元素</param>
        /// <param name="color">光点颜色</param>
        /// <param name="onComplete">完成回调</param>
        public static void PlayFlightPulse(
            VisualElement container,
            VisualElement startElement,
            VisualElement endElement,
            Color color,
            System.Action onComplete = null)
        {
            if (container == null || startElement == null || endElement == null) return;

            // 计算相对于容器的位置
            var startPos = GetElementCenter(startElement, container);
            var endPos = GetElementCenter(endElement, container);

            PlayFlightPulseAtPosition(container, startPos, endPos, color, onComplete);
        }

        /// <summary>
        /// 在指定位置播放飞行脉冲
        /// </summary>
        public static void PlayFlightPulseAtPosition(
            VisualElement container,
            Vector2 startPos,
            Vector2 endPos,
            Color color,
            System.Action onComplete = null)
        {
            if (container == null) return;

            // 从池中获取或创建状态
            var state = GetOrCreateFlightState();
            state.Container = container;
            state.StartPos = startPos;
            state.EndPos = endPos;
            state.StartTime = EditorApplication.timeSinceStartup;
            state.GlowColor = color;
            state.IsActive = true;
            state.OnComplete = onComplete;

            // 创建或复用光点元素
            if (state.Glow == null)
            {
                state.Glow = CreateGlowElement(GLOW_SIZE);
                state.Trails = new VisualElement[(int)TRAIL_COUNT];
                for (var i = 0; i < TRAIL_COUNT; i++)
                {
                    var trailSize = GLOW_SIZE * (0.7f - i * 0.15f);
                    state.Trails[i] = CreateGlowElement(trailSize);
                }
            }

            // 设置初始状态 - 主光点
            SetGlowPosition(state.Glow, startPos, GLOW_SIZE);
            state.Glow.style.backgroundColor = new StyleColor(color);
            state.Glow.style.opacity = 1f;
            state.Glow.style.display = DisplayStyle.Flex;

            // 设置拖尾
            for (var i = 0; i < state.Trails.Length; i++)
            {
                var trail = state.Trails[i];
                var trailSize = GLOW_SIZE * (0.7f - i * 0.15f);
                SetGlowPosition(trail, startPos, trailSize);
                var trailColor = color;
                trailColor.a = 0.6f - i * 0.15f;
                trail.style.backgroundColor = new StyleColor(trailColor);
                trail.style.opacity = 0.8f - i * 0.2f;
                trail.style.display = DisplayStyle.Flex;
            }

            // 添加到容器（先添加拖尾，再添加主光点，确保主光点在最上层）
            for (var i = state.Trails.Length - 1; i >= 0; i--)
            {
                if (state.Trails[i].parent != container)
                {
                    state.Trails[i].RemoveFromHierarchy();
                    container.Add(state.Trails[i]);
                }
            }
            if (state.Glow.parent != container)
            {
                state.Glow.RemoveFromHierarchy();
                container.Add(state.Glow);
            }

            sActiveFlights.Add(state);
        }

        /// <summary>
        /// 设置光点位置
        /// </summary>
        private static void SetGlowPosition(VisualElement glow, Vector2 pos, float size)
        {
            glow.style.left = pos.x - size / 2;
            glow.style.top = pos.y - size / 2;
        }

        /// <summary>
        /// 更新所有飞行脉冲动画（在 OnUpdate 中调用）
        /// </summary>
        public static void UpdateAllFlightPulses()
        {
            var now = EditorApplication.timeSinceStartup;

            for (var i = sActiveFlights.Count - 1; i >= 0; i--)
            {
                var state = sActiveFlights[i];
                if (!state.IsActive) continue;

                var elapsed = (float)(now - state.StartTime);
                var progress = Mathf.Clamp01(elapsed / FLIGHT_DURATION);

                if (progress >= 1f)
                {
                    CompleteFlightPulse(state, i);
                }
                else
                {
                    // 更新主光点位置（使用缓出曲线）
                    var easedProgress = EaseOutCubic(progress);
                    var currentPos = Vector2.Lerp(state.StartPos, state.EndPos, easedProgress);
                    SetGlowPosition(state.Glow, currentPos, GLOW_SIZE);

                    // 主光点脉动效果（大小变化）
                    var pulse = 1f + Mathf.Sin(progress * Mathf.PI * 4) * 0.15f;
                    state.Glow.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                    state.Glow.style.opacity = 1f;

                    // 更新拖尾位置（延迟跟随）
                    for (var j = 0; j < state.Trails.Length; j++)
                    {
                        var trailDelay = (j + 1) * 0.08f;
                        var trailProgress = Mathf.Clamp01(easedProgress - trailDelay);
                        var trailPos = Vector2.Lerp(state.StartPos, state.EndPos, trailProgress);
                        var trailSize = GLOW_SIZE * (0.7f - j * 0.15f);
                        SetGlowPosition(state.Trails[j], trailPos, trailSize);
                        state.Trails[j].style.opacity = (0.7f - j * 0.2f) * (1f - progress * 0.5f);
                    }
                }
            }
        }

        /// <summary>
        /// 清理所有飞行脉冲
        /// </summary>
        public static void ClearAllFlightPulses()
        {
            foreach (var state in sActiveFlights)
            {
                if (state.Glow != null)
                {
                    state.Glow.style.display = DisplayStyle.None;
                    state.Glow.RemoveFromHierarchy();
                }
                if (state.Trails != null)
                {
                    foreach (var trail in state.Trails)
                    {
                        trail.style.display = DisplayStyle.None;
                        trail.RemoveFromHierarchy();
                    }
                }
                state.IsActive = false;
            }
            sActiveFlights.Clear();
        }

        #endregion

        #region 内部实现

        /// <summary>
        /// 创建光点元素
        /// </summary>
        private static VisualElement CreateGlowElement(float size)
        {
            var glow = new VisualElement();
            glow.name = "flight-glow";
            glow.style.position = Position.Absolute;
            glow.style.width = size;
            glow.style.height = size;
            glow.style.borderTopLeftRadius = size / 2;
            glow.style.borderTopRightRadius = size / 2;
            glow.style.borderBottomLeftRadius = size / 2;
            glow.style.borderBottomRightRadius = size / 2;
            glow.pickingMode = PickingMode.Ignore;

            // 添加发光效果（通过边框模拟光晕）
            var borderWidth = Mathf.Max(2f, size * 0.2f);
            glow.style.borderLeftWidth = borderWidth;
            glow.style.borderRightWidth = borderWidth;
            glow.style.borderTopWidth = borderWidth;
            glow.style.borderBottomWidth = borderWidth;
            
            var borderColor = new Color(1f, 1f, 1f, 0.8f);
            glow.style.borderLeftColor = new StyleColor(borderColor);
            glow.style.borderRightColor = new StyleColor(borderColor);
            glow.style.borderTopColor = new StyleColor(borderColor);
            glow.style.borderBottomColor = new StyleColor(borderColor);

            return glow;
        }

        /// <summary>
        /// 从池中获取或创建飞行状态
        /// </summary>
        private static FlightPulseState GetOrCreateFlightState()
        {
            // 查找可复用的状态
            foreach (var state in sFlightPool)
            {
                if (!state.IsActive)
                {
                    return state;
                }
            }

            // 创建新状态
            var newState = new FlightPulseState();
            sFlightPool.Add(newState);
            return newState;
        }

        /// <summary>
        /// 完成飞行脉冲
        /// </summary>
        private static void CompleteFlightPulse(FlightPulseState state, int index)
        {
            state.IsActive = false;
            state.Glow.style.display = DisplayStyle.None;
            state.Glow.style.scale = new StyleScale(Scale.None());
            
            if (state.Trails != null)
            {
                foreach (var trail in state.Trails)
                    trail.style.display = DisplayStyle.None;
            }
            
            state.OnComplete?.Invoke();
            sActiveFlights.RemoveAt(index);
        }

        /// <summary>
        /// 获取元素相对于容器的中心位置
        /// </summary>
        private static Vector2 GetElementCenter(VisualElement element, VisualElement container)
        {
            var elementRect = element.worldBound;
            var containerRect = container.worldBound;

            // 转换为相对于容器的坐标
            var relativeX = elementRect.center.x - containerRect.x;
            var relativeY = elementRect.center.y - containerRect.y;

            return new Vector2(relativeX, relativeY);
        }

        /// <summary>
        /// 缓出三次方曲线
        /// </summary>
        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        #endregion
    }
}
#endif
