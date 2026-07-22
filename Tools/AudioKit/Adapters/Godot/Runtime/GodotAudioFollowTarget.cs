#if GODOT
using Godot;
using NumericsVector3 = System.Numerics.Vector3;

namespace YokiFrame.Godot
{
    /// <summary>把 Godot Node3D 适配为 AudioKit 窄跟随目标。</summary>
    public sealed class GodotAudioFollowTarget : IAudioFollowTarget
    {
        private readonly Node3D mNode;

        /// <summary>创建绑定指定 Node3D 生命周期的位置目标。</summary>
        public GodotAudioFollowTarget(Node3D node)
        {
            mNode = node;
        }

        /// <summary>获取节点名称；节点失效时返回空文本。</summary>
        public string Name => IsAlive ? mNode.Name.ToString() : string.Empty;

        /// <summary>获取 Godot 对象是否仍有效。</summary>
        public bool IsAlive => mNode != null && GodotObject.IsInstanceValid(mNode);

        /// <summary>获取节点世界位置；节点失效时返回零。</summary>
        public NumericsVector3 Position
        {
            get
            {
                if (!IsAlive) return NumericsVector3.Zero;
                Vector3 position = mNode.GlobalPosition;
                return new NumericsVector3(position.X, position.Y, position.Z);
            }
        }
    }
}
#endif
