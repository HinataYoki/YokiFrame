using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示由引擎后端返回的跨引擎场景句柄。
    /// </summary>
    public struct SceneHandle : IEquatable<SceneHandle>
    {
        /// <summary>
        /// 场景名称。
        /// </summary>
        public readonly string SceneName;

        /// <summary>
        /// 构建索引。
        /// </summary>
        public readonly int BuildIndex;

        /// <summary>
        /// 句柄是否有效。
        /// </summary>
        public readonly bool IsValid;

        /// <summary>
        /// 创建场景句柄。
        /// </summary>
        /// <param name="sceneName">场景名称。</param>
        /// <param name="buildIndex">构建索引。</param>
        /// <param name="isValid">句柄是否有效。</param>
        public SceneHandle(string sceneName, int buildIndex, bool isValid)
        {
            SceneName = sceneName;
            BuildIndex = buildIndex;
            IsValid = isValid;
        }

        /// <inheritdoc />
        public bool Equals(SceneHandle other)
        {
            return SceneName == other.SceneName && BuildIndex == other.BuildIndex && IsValid == other.IsValid;
        }

        public static bool operator ==(SceneHandle left, SceneHandle right) => left.Equals(right);

        public static bool operator !=(SceneHandle left, SceneHandle right) => !left.Equals(right);

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
    }
}
