#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 保存一个 Member Bind 的组件引用与独立生成字段名。
    /// </summary>
    [Serializable]
    internal sealed class BindMemberTarget
    {
        [SerializeField]
        private Component mTarget;

        [SerializeField]
        private string mName;

        /// <summary>获取或设置 Bind 所在 GameObject 上的目标组件。</summary>
        internal Component Target
        {
            get => mTarget;
            set => mTarget = value;
        }

        /// <summary>获取或设置该组件生成的独立字段名。</summary>
        internal string Name
        {
            get => mName;
            set => mName = value;
        }
    }
}
#endif
