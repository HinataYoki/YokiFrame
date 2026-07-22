using System;

namespace YokiFrame
{
    /// <summary>表示由 SceneKit 返回的跨引擎场景句柄。</summary>
    public readonly struct SceneHandle : IEquatable<SceneHandle>
    {
        /// <summary>创建场景句柄。</summary>
        public SceneHandle(string sceneName, int buildIndex, bool isValid)
        {
            SceneName = sceneName ?? string.Empty;
            BuildIndex = buildIndex;
            IsValid = isValid;
        }

        /// <summary>获取场景名称或 Provider 路径。</summary>
        public string SceneName { get; }

        /// <summary>获取场景构建索引。</summary>
        public int BuildIndex { get; }

        /// <summary>获取句柄是否有效。</summary>
        public bool IsValid { get; }

        /// <inheritdoc />
        public bool Equals(SceneHandle other)
        {
            return string.Equals(SceneName, other.SceneName, StringComparison.Ordinal)
                && BuildIndex == other.BuildIndex
                && IsValid == other.IsValid;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is SceneHandle other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(SceneName, BuildIndex, IsValid);
        }

        /// <summary>比较两个场景句柄是否相等。</summary>
        public static bool operator ==(SceneHandle left, SceneHandle right) => left.Equals(right);

        /// <summary>比较两个场景句柄是否不等。</summary>
        public static bool operator !=(SceneHandle left, SceneHandle right) => !left.Equals(right);
    }
}
