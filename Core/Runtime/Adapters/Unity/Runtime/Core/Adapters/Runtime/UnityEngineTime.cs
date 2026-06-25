#if !GODOT
using UnityEngine;
using YokiFrame;

namespace YokiFrame.Unity
{
    /// <summary>
    /// IEngineTime 的 Unity 实现，转发到 UnityEngine.Time。
    /// </summary>
    public sealed class UnityEngineTime : IEngineTime
    {
        /// <inheritdoc />
        public float DeltaTime => Time.deltaTime;

        /// <inheritdoc />
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;

        /// <inheritdoc />
        public float RealtimeSinceStartup => Time.realtimeSinceStartup;
    }
}
#endif
