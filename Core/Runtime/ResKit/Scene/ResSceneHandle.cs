using System;

namespace YokiFrame
{
    /// <summary>表示由 ResKit 场景 Provider 返回的跨引擎场景句柄。</summary>
    public readonly struct ResSceneHandle : IEquatable<ResSceneHandle>
    {
        private readonly string mSceneName;

        /// <summary>创建场景句柄。</summary>
        public ResSceneHandle(string sceneName, int buildIndex, bool isValid)
        {
            mSceneName = sceneName ?? string.Empty;
            BuildIndex = buildIndex;
            IsValid = isValid;
        }

        /// <summary>获取场景名称或 Provider 路径。</summary>
        public string SceneName => mSceneName ?? string.Empty;

        /// <summary>获取场景构建索引；不支持时为负数。</summary>
        public int BuildIndex { get; }

        /// <summary>获取句柄是否有效。</summary>
        public bool IsValid { get; }

        /// <inheritdoc />
        public bool Equals(ResSceneHandle other)
        {
            return string.Equals(SceneName, other.SceneName, StringComparison.Ordinal)
                && BuildIndex == other.BuildIndex
                && IsValid == other.IsValid;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ResSceneHandle other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(SceneName, BuildIndex, IsValid);
        }

        /// <summary>比较两个场景句柄是否相等。</summary>
        public static bool operator ==(ResSceneHandle left, ResSceneHandle right) => left.Equals(right);

        /// <summary>比较两个场景句柄是否不等。</summary>
        public static bool operator !=(ResSceneHandle left, ResSceneHandle right) => !left.Equals(right);
    }
}
