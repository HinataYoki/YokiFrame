#if UNITY_EDITOR
using System;

namespace YokiFrame
{
    /// <summary>描述一次 UIKit Editor 生成或绑定操作的稳定结果。</summary>
    [Serializable]
    internal sealed class UIKitPanelGenerationResult
    {
        public string message;
        public string prefabPath;
        public string panelScriptPath;
        public string designerScriptPath;
        public bool scriptsChanged;
        public int bindCount;
        public int warningCount;

        /// <summary>把结果序列化为命令 response payload。</summary>
        internal string ToJson()
        {
            return UnityEngine.JsonUtility.ToJson(this);
        }
    }
}
#endif
