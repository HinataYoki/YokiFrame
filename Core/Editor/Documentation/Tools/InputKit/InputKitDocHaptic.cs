#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 震动反馈文档
    /// </summary>
    internal static class InputKitDocHaptic
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "震动反馈",
                Description = "手柄震动控制，支持预设和自定义曲线。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "预设震动",
                        Code = @"InputKit.PlayHaptic(HapticPreset.Light);
InputKit.PlayHaptic(HapticPreset.Medium);
InputKit.PlayHaptic(HapticPreset.Heavy);
InputKit.PlayHaptic(HapticPreset.Pulse);
InputKit.PlayHaptic(HapticPreset.Success);
InputKit.PlayHaptic(HapticPreset.Error);

// 游戏应用
void OnHit(float damage)
{
    var preset = damage > 50f ? HapticPreset.Heavy 
               : damage > 20f ? HapticPreset.Medium 
               : HapticPreset.Light;
    InputKit.PlayHaptic(preset);
}",
                        Explanation = "预设覆盖常见场景，开箱即用。"
                    },
                    new()
                    {
                        Title = "自定义震动",
                        Code = @"// 简单参数
InputKit.PlayHaptic(
    leftMotor: 0.5f,   // 低频重震
    rightMotor: 0.8f,  // 高频轻震
    duration: 0.3f
);

// 曲线模式
var pattern = new HapticPattern
{
    Duration = 1f,
    LeftMotorCurve = AnimationCurve.EaseInOut(0, 0, 1, 1),
    RightMotorCurve = AnimationCurve.Linear(0, 0.5f, 1, 0)
};
InputKit.PlayHaptic(pattern);",
                        Explanation = "HapticPattern 用 AnimationCurve 定义强度变化。"
                    },
                    new()
                    {
                        Title = "全局控制",
                        Code = @"// 强度（设置菜单用）
InputKit.HapticIntensity = 0.5f;

// 开关
InputKit.HapticEnabled = false;

// 停止
InputKit.StopHaptic();

// Update 中更新（处理曲线采样）
InputKit.UpdateHaptic();",
                        Explanation = "全局强度乘以每次震动强度。"
                    }
                }
            };
        }
    }
}
#endif
