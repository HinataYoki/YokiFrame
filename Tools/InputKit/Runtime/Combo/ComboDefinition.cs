using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 连招定义（ScriptableObject）
    /// </summary>
    [CreateAssetMenu(fileName = "NewCombo", menuName = "YokiFrame/InputKit/Combo Definition")]
    public class ComboDefinition : ScriptableObject
    {
        /// <summary>连招 ID</summary>
        [Tooltip("连招唯一标识符")]
        public string ComboId;
        
        /// <summary>连招步骤</summary>
        [Tooltip("连招的输入步骤序列")]
        public ComboStep[] Steps;
        
        /// <summary>步骤间最大间隔（秒）</summary>
        [Tooltip("两个步骤之间允许的最大时间间隔")]
        [Range(0.1f, 2f)]
        public float WindowBetweenSteps = 0.3f;
        
        /// <summary>是否要求精确顺序</summary>
        [Tooltip("是否要求按精确顺序输入")]
        public bool RequireExactOrder = true;
        
        /// <summary>是否允许中断</summary>
        [Tooltip("连招进行中是否允许被其他输入中断")]
        public bool AllowInterrupt = false;

        /// <summary>步骤数量</summary>
        public int StepCount => Steps != default ? Steps.Length : 0;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(ComboId))
            {
                ComboId = name;
            }
        }
    }
}
