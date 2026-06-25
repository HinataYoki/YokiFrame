#if !GODOT
using UnityEngine;
using YokiFrame;

namespace YokiFrame.Unity
{
    /// <summary>
    /// IEngineLogger 的 Unity 实现，转发到 UnityEngine.Debug。
    /// </summary>
    public sealed class UnityEngineLogger : IEngineLogger
    {
        /// <inheritdoc />
        public void Log(LogLevel level, string message, object context = null)
        {
            var contextObject = context as Object;
            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(message, contextObject);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(message, contextObject);
                    break;
                case LogLevel.Error:
                    Debug.LogError(message, contextObject);
                    break;
            }
        }
    }
}
#endif
