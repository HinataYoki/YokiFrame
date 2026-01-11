using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 震动模式定义
    /// </summary>
    [CreateAssetMenu(fileName = "NewHapticPattern", menuName = "YokiFrame/InputKit/Haptic Pattern")]
    public class HapticPattern : ScriptableObject
    {
        /// <summary>左马达曲线</summary>
        [Tooltip("左马达（低频）强度曲线，X轴为时间(0-1)，Y轴为强度(0-1)")]
        public AnimationCurve LeftMotorCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 0f);
        
        /// <summary>右马达曲线</summary>
        [Tooltip("右马达（高频）强度曲线，X轴为时间(0-1)，Y轴为强度(0-1)")]
        public AnimationCurve RightMotorCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 0f);
        
        /// <summary>持续时长（秒）</summary>
        [Tooltip("震动持续时长")]
        [Range(0.01f, 5f)]
        public float Duration = 0.2f;

        /// <summary>
        /// 获取指定时间点的马达强度
        /// </summary>
        public void Evaluate(float normalizedTime, out float left, out float right)
        {
            left = LeftMotorCurve != default ? LeftMotorCurve.Evaluate(normalizedTime) : 0f;
            right = RightMotorCurve != default ? RightMotorCurve.Evaluate(normalizedTime) : 0f;
        }
    }
}
