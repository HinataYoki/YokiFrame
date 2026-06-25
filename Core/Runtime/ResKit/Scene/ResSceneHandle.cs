using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示由 ResKit 场景后端返回的跨引擎场景句柄。
    /// </summary>
    public struct ResSceneHandle : IEquatable<ResSceneHandle>
    {
        public readonly string SceneName;
        public readonly int BuildIndex;
        public readonly bool IsValid;

        public ResSceneHandle(string sceneName, int buildIndex, bool isValid)
        {
            SceneName = sceneName;
            BuildIndex = buildIndex;
            IsValid = isValid;
        }

        public bool Equals(ResSceneHandle other)
        {
            return SceneName == other.SceneName && BuildIndex == other.BuildIndex && IsValid == other.IsValid;
        }

        public override bool Equals(object obj)
        {
            return obj is ResSceneHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SceneName, BuildIndex, IsValid);
        }
    }
}
