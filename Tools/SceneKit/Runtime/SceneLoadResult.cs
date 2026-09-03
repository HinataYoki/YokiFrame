using System;

namespace YokiFrame
{
    /// <summary>表示一次 SceneKit 场景加载结果。</summary>
    public readonly struct SceneLoadResult : IEquatable<SceneLoadResult>
    {
        /// <summary>创建场景加载结果。</summary>
        public SceneLoadResult(SceneHandle scene)
        {
            Scene = scene;
        }

        /// <summary>获取场景句柄。</summary>
        public SceneHandle Scene { get; }

        /// <summary>获取加载是否成功。</summary>
        public bool Succeeded => Scene.IsValid;

        /// <inheritdoc />
        public bool Equals(SceneLoadResult other) => Scene.Equals(other.Scene);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SceneLoadResult other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Scene.GetHashCode();

        /// <summary>比较两个场景加载结果是否相等。</summary>
        public static bool operator ==(SceneLoadResult left, SceneLoadResult right) => left.Equals(right);

        /// <summary>比较两个场景加载结果是否不等。</summary>
        public static bool operator !=(SceneLoadResult left, SceneLoadResult right) => !left.Equals(right);
    }
}
