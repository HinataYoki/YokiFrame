#if UNITY_2022_3_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace YokiFrame
{
    /// <summary>
    /// Unity UI 绑定标记的序列化兼容基类；字段名称与 2.0-pre 保持稳定。
    /// </summary>
    public abstract class AbstractBind : MonoBehaviour, IBind
    {
        /// <summary>绑定语义；旧 Prefab 数值按 BindType 原样读取。</summary>
        [FormerlySerializedAs("bind")]
        public BindType Bind = BindType.Member;

        /// <summary>生成字段名称；为空时由 Editor 扫描器使用节点名称。</summary>
        [FormerlySerializedAs("mName")]
        public string Name;

        /// <summary>旧版 Inspector 记录的自动推断类型名称。</summary>
        [FormerlySerializedAs("autoType")]
        public string AutoType;

        /// <summary>旧版 Inspector 记录的手动类型名称。</summary>
        [FormerlySerializedAs("customType")]
        public string CustomType;

        /// <summary>生成器最终使用的类型名称。</summary>
        [FormerlySerializedAs("type")]
        public string Type;

        /// <summary>生成字段的注释文本。</summary>
        [FormerlySerializedAs("comment")]
        public string Comment;

        [SerializeField]
        private Component mTarget;

        [SerializeField]
        private List<BindMemberTarget> mMemberTargets = new();

        /// <summary>获取或设置显式绑定的组件目标；为空时兼容旧 Type/AutoType 解析。</summary>
        public Component Target
        {
            get => mTarget;
            set => mTarget = value;
        }

        /// <summary>
        /// 获取 Editor 使用的多 Member 目标；列表为空时继续按旧单目标字段解析。
        /// </summary>
        internal List<BindMemberTarget> MemberTargets
        {
            get
            {
                if (mMemberTargets == null)
                    mMemberTargets = new List<BindMemberTarget>();
                return mMemberTargets;
            }
        }

        BindType IBind.Bind => Bind;
        string IBind.Name => Name;
        string IBind.Type => Type;
        string IBind.Comment => Comment;

        /// <summary>获取当前绑定节点 Transform。</summary>
        public Transform Transform => transform;
    }
}
#endif
