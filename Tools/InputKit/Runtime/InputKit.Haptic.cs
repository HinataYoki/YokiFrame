#if YOKIFRAME_INPUTSYSTEM_SUPPORT
using UnityEngine;
using UnityEngine.InputSystem;

namespace YokiFrame
{
    /// <summary>
    /// InputKit - 震动/触觉反馈
    /// </summary>
    public static partial class InputKit
    {
        private static float sHapticIntensity = 1f;
        private static bool sHapticEnabled = true;
        private static float sHapticEndTime;
        private static HapticPattern sCurrentPattern;
        private static float sPatternStartTime;

        #region 属性

        /// <summary>全局震动强度（0-1）</summary>
        public static float HapticIntensity
        {
            get => sHapticIntensity;
            set => sHapticIntensity = Mathf.Clamp01(value);
        }

        /// <summary>是否启用震动</summary>
        public static bool HapticEnabled
        {
            get => sHapticEnabled;
            set
            {
                sHapticEnabled = value;
                if (!value) StopHaptic();
            }
        }

        #endregion

        #region 震动控制

        /// <summary>
        /// 播放震动（预设模式）
        /// </summary>
        public static void PlayHaptic(HapticPreset preset)
        {
            if (!sHapticEnabled) return;

            var (left, right, duration) = GetPresetValues(preset);
            PlayHapticInternal(left, right, duration);
        }

        /// <summary>
        /// 播放震动（自定义模式）
        /// </summary>
        public static void PlayHaptic(HapticPattern pattern)
        {
            if (!sHapticEnabled || pattern == default) return;

            sCurrentPattern = pattern;
            sPatternStartTime = Time.unscaledTime;
            sHapticEndTime = sPatternStartTime + pattern.Duration;
        }

        /// <summary>
        /// 播放震动（简单参数）
        /// </summary>
        public static void PlayHaptic(float leftMotor, float rightMotor, float duration)
        {
            if (!sHapticEnabled) return;
            PlayHapticInternal(leftMotor, rightMotor, duration);
        }

        /// <summary>
        /// 停止所有震动
        /// </summary>
        public static void StopHaptic()
        {
            sCurrentPattern = default;
            sHapticEndTime = 0f;

            var gamepad = Gamepad.current;
            if (gamepad != default)
            {
                gamepad.SetMotorSpeeds(0f, 0f);
            }
        }

        /// <summary>
        /// 设置全局震动强度
        /// </summary>
        public static void SetHapticIntensity(float intensity)
        {
            HapticIntensity = intensity;
        }

        #endregion

        #region 更新

        /// <summary>
        /// 更新震动（需要在 Update 中调用）
        /// </summary>
        public static void UpdateHaptic()
        {
            if (!sHapticEnabled) return;

            var gamepad = Gamepad.current;
            if (gamepad == default) return;

            float currentTime = Time.unscaledTime;

            // 处理自定义模式
            if (sCurrentPattern != default)
            {
                if (currentTime >= sHapticEndTime)
                {
                    StopHaptic();
                    return;
                }

                float elapsed = currentTime - sPatternStartTime;
                float normalizedTime = elapsed / sCurrentPattern.Duration;
                sCurrentPattern.Evaluate(normalizedTime, out float left, out float right);

                gamepad.SetMotorSpeeds(
                    left * sHapticIntensity,
                    right * sHapticIntensity
                );
            }
            // 处理简单震动超时
            else if (sHapticEndTime > 0f && currentTime >= sHapticEndTime)
            {
                StopHaptic();
            }
        }

        #endregion

        #region 内部方法

        private static void PlayHapticInternal(float left, float right, float duration)
        {
            var gamepad = Gamepad.current;
            if (gamepad == default) return;

            sCurrentPattern = default;
            sHapticEndTime = Time.unscaledTime + duration;

            gamepad.SetMotorSpeeds(
                left * sHapticIntensity,
                right * sHapticIntensity
            );
        }

        private static (float left, float right, float duration) GetPresetValues(HapticPreset preset)
        {
            return preset switch
            {
                HapticPreset.Light => (0.2f, 0.2f, 0.1f),
                HapticPreset.Medium => (0.5f, 0.5f, 0.15f),
                HapticPreset.Heavy => (1f, 1f, 0.2f),
                HapticPreset.Pulse => (0.8f, 0.3f, 0.1f),
                HapticPreset.Success => (0.3f, 0.6f, 0.15f),
                HapticPreset.Error => (0.8f, 0.2f, 0.25f),
                HapticPreset.Selection => (0.1f, 0.3f, 0.05f),
                _ => (0.5f, 0.5f, 0.15f)
            };
        }

        /// <summary>
        /// 重置震动系统（内部调用）
        /// </summary>
        internal static void ResetHaptic()
        {
            StopHaptic();
            sHapticIntensity = 1f;
            sHapticEnabled = true;
        }

        #endregion
    }
}

#endif