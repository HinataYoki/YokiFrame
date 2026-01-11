using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 输入上下文定义
    /// </summary>
    [CreateAssetMenu(fileName = "NewInputContext", menuName = "YokiFrame/InputKit/Input Context")]
    public class InputContext : ScriptableObject
    {
        /// <summary>上下文名称</summary>
        [Tooltip("上下文唯一标识符")]
        public string ContextName;
        
        /// <summary>优先级（高优先级阻断低优先级）</summary>
        [Tooltip("优先级，数值越大优先级越高")]
        public int Priority;
        
        /// <summary>启用的 ActionMap</summary>
        [Tooltip("在此上下文中启用的 ActionMap 列表")]
        public string[] EnabledActionMaps;
        
        /// <summary>阻断的 Action</summary>
        [Tooltip("在此上下文中被阻断的 Action 列表")]
        public string[] BlockedActions;
        
        /// <summary>是否阻断所有低优先级输入</summary>
        [Tooltip("是否阻断所有优先级更低的上下文的输入")]
        public bool BlockAllLowerPriority;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(ContextName))
            {
                ContextName = name;
            }
        }
    }
}
