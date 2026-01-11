#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 震动/触觉反馈文档
    /// </summary>
    internal static class InputKitDocHaptic
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "震动/触觉反馈",
                Description = "InputKit 提供手柄震动控制，支持预设模式和自定义震动曲线（需要 InputSystem）。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "预设震动",
                        Code = @"// 使用预设震动模式
InputKit.PlayHaptic(HapticPreset.Light);    // 轻微震动
InputKit.PlayHaptic(HapticPreset.Medium);   // 中等震动
InputKit.PlayHaptic(HapticPreset.Heavy);    // 强烈震动
InputKit.PlayHaptic(HapticPreset.Pulse);    // 脉冲震动
InputKit.PlayHaptic(HapticPreset.Success);  // 成功反馈
InputKit.PlayHaptic(HapticPreset.Error);    // 错误反馈
InputKit.PlayHaptic(HapticPreset.Selection);// 选择反馈

// 游戏中的应用
void OnPlayerHit(float damage)
{
    if (damage > 50f)
        InputKit.PlayHaptic(HapticPreset.Heavy);
    else if (damage > 20f)
        InputKit.PlayHaptic(HapticPreset.Medium);
    else
        InputKit.PlayHaptic(HapticPreset.Light);
}

void OnItemPickup()
{
    InputKit.PlayHaptic(HapticPreset.Selection);
}

void OnQuestComplete()
{
    InputKit.PlayHaptic(HapticPreset.Success);
}",
                        Explanation = "预设模式覆盖常见场景，参数已调优，开箱即用。"
                    },
                    new()
                    {
                        Title = "自定义震动",
                        Code = @"// 简单参数：左马达、右马达、持续时间
InputKit.PlayHaptic(
    leftMotor: 0.5f,   // 左马达强度 0-1（低频，重震动）
    rightMotor: 0.8f,  // 右马达强度 0-1（高频，轻震动）
    duration: 0.3f     // 持续时间（秒）
);

// 使用 HapticPattern 自定义曲线
var pattern = new HapticPattern
{
    Duration = 1f,
    LeftMotorCurve = AnimationCurve.EaseInOut(0, 0, 1, 1),
    RightMotorCurve = AnimationCurve.Linear(0, 0.5f, 1, 0)
};
InputKit.PlayHaptic(pattern);

// 创建渐强震动
var buildUp = new HapticPattern
{
    Duration = 0.5f,
    LeftMotorCurve = new AnimationCurve(
        new Keyframe(0, 0),
        new Keyframe(0.5f, 0.3f),
        new Keyframe(1, 1)
    ),
    RightMotorCurve = new AnimationCurve(
        new Keyframe(0, 0),
        new Keyframe(1, 0.8f)
    )
};

// 创建脉冲序列
var pulsePattern = new HapticPattern
{
    Duration = 0.6f,
    LeftMotorCurve = new AnimationCurve(
        new Keyframe(0, 1), new Keyframe(0.1f, 0),
        new Keyframe(0.2f, 1), new Keyframe(0.3f, 0),
        new Keyframe(0.4f, 1), new Keyframe(0.5f, 0)
    )
};",
                        Explanation = "HapticPattern 使用 AnimationCurve 定义震动强度随时间的变化。"
                    },
                    new()
                    {
                        Title = "全局控制",
                        Code = @"// 设置全局震动强度（用于设置菜单）
InputKit.HapticIntensity = 0.5f;  // 50% 强度

// 完全禁用震动
InputKit.HapticEnabled = false;

// 从设置加载
void LoadHapticSettings()
{
    InputKit.HapticEnabled = PlayerPrefs.GetInt(""HapticEnabled"", 1) == 1;
    InputKit.HapticIntensity = PlayerPrefs.GetFloat(""HapticIntensity"", 1f);
}

// 保存设置
void SaveHapticSettings()
{
    PlayerPrefs.SetInt(""HapticEnabled"", InputKit.HapticEnabled ? 1 : 0);
    PlayerPrefs.SetFloat(""HapticIntensity"", InputKit.HapticIntensity);
}

// 停止当前震动
InputKit.StopHaptic();",
                        Explanation = "全局强度会乘以每次震动的强度，方便用户调节体验。"
                    },
                    new()
                    {
                        Title = "更新与生命周期",
                        Code = @"// 在 Update 中更新震动系统
void Update()
{
    InputKit.UpdateHaptic();
}

// 或者使用 MonoBehaviour 封装
public class HapticUpdater : MonoBehaviour
{
    void Update()
    {
        InputKit.UpdateHaptic();
    }
    
    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            InputKit.StopHaptic();
        }
    }
    
    void OnDestroy()
    {
        InputKit.StopHaptic();
    }
}",
                        Explanation = "UpdateHaptic 处理自定义模式的曲线采样和超时停止，需要每帧调用。"
                    }
                }
            };
        }
    }
}
#endif
