#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 溶解动画效果
    /// 实现元素的淡出 + 缩小效果，动画结束后自动移除
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 溶解动画常量

        private const float DISSOLVE_DURATION = 0.5f;   // 溶解时长（秒）
        private const float DISSOLVE_MIN_SCALE = 0.8f;  // 最小缩放

        #endregion

        #region 溶解状态管理

        /// <summary>
        /// 溶解状态
        /// </summary>
        private class DissolveState
        {
            public VisualElement Target;
            public double StartTime;
            public Color HighlightColor;
            public System.Action OnComplete;
            public bool IsActive;
        }

        private static readonly List<DissolveState> sDissolveStates = new(16);

        #endregion

        #region 公共 API

        /// <summary>
        /// 播放溶解动画
        /// </summary>
        /// <param name="target">目标元素</param>
        /// <param name="highlightColor">高亮颜色（溶解前闪烁）</param>
        /// <param name="onComplete">完成回调（元素移除后）</param>
        public static void PlayDissolve(
            VisualElement target,
            Color? highlightColor = null,
            System.Action onComplete = null)
        {
            if (target == null) return;

            var state = new DissolveState
            {
                Target = target,
                StartTime = EditorApplication.timeSinceStartup,
                HighlightColor = highlightColor ?? PulseUnregister,
                OnComplete = onComplete,
                IsActive = true
            };

            // 设置初始高亮
            target.style.borderLeftColor = new StyleColor(state.HighlightColor);
            target.style.borderRightColor = new StyleColor(state.HighlightColor);
            target.style.borderTopColor = new StyleColor(state.HighlightColor);
            target.style.borderBottomColor = new StyleColor(state.HighlightColor);
            target.style.borderLeftWidth = 2;
            target.style.borderRightWidth = 2;
            target.style.borderTopWidth = 2;
            target.style.borderBottomWidth = 2;

            sDissolveStates.Add(state);
        }

        /// <summary>
        /// 更新所有溶解动画（在 OnUpdate 中调用）
        /// </summary>
        public static void UpdateAllDissolves()
        {
            var now = EditorApplication.timeSinceStartup;

            for (var i = sDissolveStates.Count - 1; i >= 0; i--)
            {
                var state = sDissolveStates[i];
                if (!state.IsActive || state.Target == null)
                {
                    sDissolveStates.RemoveAt(i);
                    continue;
                }

                var elapsed = (float)(now - state.StartTime);
                var progress = Mathf.Clamp01(elapsed / DISSOLVE_DURATION);

                if (progress >= 1f)
                {
                    // 动画完成，移除元素
                    CompleteDissolve(state, i);
                }
                else
                {
                    // 更新透明度和缩放
                    UpdateDissolveVisuals(state, progress);
                }
            }
        }

        /// <summary>
        /// 清理所有溶解动画
        /// </summary>
        public static void ClearAllDissolves()
        {
            foreach (var state in sDissolveStates)
            {
                state.IsActive = false;
            }
            sDissolveStates.Clear();
        }

        #endregion

        #region 内部实现

        /// <summary>
        /// 更新溶解视觉效果
        /// </summary>
        private static void UpdateDissolveVisuals(DissolveState state, float progress)
        {
            var target = state.Target;

            // 透明度：1.0 -> 0.0
            target.style.opacity = 1f - progress;

            // 缩放：1.0 -> 0.8（使用 transform）
            var scale = Mathf.Lerp(1f, DISSOLVE_MIN_SCALE, progress);
            target.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));

            // 边框颜色渐隐
            var borderColor = state.HighlightColor;
            borderColor.a = 1f - progress;
            target.style.borderLeftColor = new StyleColor(borderColor);
            target.style.borderRightColor = new StyleColor(borderColor);
            target.style.borderTopColor = new StyleColor(borderColor);
            target.style.borderBottomColor = new StyleColor(borderColor);
        }

        /// <summary>
        /// 完成溶解动画
        /// </summary>
        private static void CompleteDissolve(DissolveState state, int index)
        {
            state.IsActive = false;

            // 移除元素
            state.Target?.RemoveFromHierarchy();

            // 回调
            state.OnComplete?.Invoke();

            sDissolveStates.RemoveAt(index);
        }

        #endregion

        #region 高亮闪烁效果

        /// <summary>
        /// 播放高亮闪烁效果（接收脉冲到达时）
        /// </summary>
        /// <param name="target">目标元素</param>
        /// <param name="color">高亮颜色</param>
        /// <param name="duration">持续时间</param>
        public static void PlayHighlightFlash(VisualElement target, Color color, float duration = 0.3f)
        {
            if (target == null) return;

            // 保存原始边框
            var originalBorderColor = target.resolvedStyle.borderLeftColor;
            var originalBorderWidth = target.resolvedStyle.borderLeftWidth;

            // 设置高亮
            target.style.borderLeftColor = new StyleColor(color);
            target.style.borderRightColor = new StyleColor(color);
            target.style.borderTopColor = new StyleColor(color);
            target.style.borderBottomColor = new StyleColor(color);
            target.style.borderLeftWidth = 2;
            target.style.borderRightWidth = 2;
            target.style.borderTopWidth = 2;
            target.style.borderBottomWidth = 2;

            // 延迟恢复
            var startTime = EditorApplication.timeSinceStartup;
            void RestoreCallback()
            {
                if (EditorApplication.timeSinceStartup - startTime >= duration)
                {
                    target.style.borderLeftColor = new StyleColor(originalBorderColor);
                    target.style.borderRightColor = new StyleColor(originalBorderColor);
                    target.style.borderTopColor = new StyleColor(originalBorderColor);
                    target.style.borderBottomColor = new StyleColor(originalBorderColor);
                    target.style.borderLeftWidth = originalBorderWidth;
                    target.style.borderRightWidth = originalBorderWidth;
                    target.style.borderTopWidth = originalBorderWidth;
                    target.style.borderBottomWidth = originalBorderWidth;
                    EditorApplication.update -= RestoreCallback;
                }
            }
            EditorApplication.update += RestoreCallback;
        }

        #endregion
    }
}
#endif
