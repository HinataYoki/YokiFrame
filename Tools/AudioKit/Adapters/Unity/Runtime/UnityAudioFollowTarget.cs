#if UNITY_2022_3_OR_NEWER
using UnityEngine;
using NumericsVector3 = System.Numerics.Vector3;

namespace YokiFrame.Unity
{
    /// <summary>把 Unity Transform 适配为 AudioKit 窄跟随目标。</summary>
    public sealed class UnityAudioFollowTarget : IAudioFollowTarget
    {
        private readonly Transform mTransform;

        /// <summary>创建绑定指定 Transform 生命周期的位置目标。</summary>
        public UnityAudioFollowTarget(Transform transform)
        {
            mTransform = transform;
        }

        /// <summary>获取 Transform 名称；对象已销毁时返回空文本。</summary>
        public string Name => mTransform != null ? mTransform.name : string.Empty;

        /// <summary>获取 Unity 对象是否仍有效。</summary>
        public bool IsAlive => mTransform != null;

        /// <summary>获取当前世界位置；对象已销毁时返回零。</summary>
        public NumericsVector3 Position
        {
            get
            {
                if (mTransform == null) return NumericsVector3.Zero;
                UnityEngine.Vector3 position = mTransform.position;
                return new NumericsVector3(position.x, position.y, position.z);
            }
        }
    }
}
#endif
