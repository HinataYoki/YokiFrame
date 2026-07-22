#if UNITY_5_3_OR_NEWER
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 将 Core LogKit 的日志请求转发到 UnityEngine.Debug。
    /// </summary>
    public sealed class UnityEngineLogger : IEngineLogger
    {
        /// <summary>
        /// 写入不带显式堆栈的日志请求。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志内容。</param>
        /// <param name="context">Unity 对象上下文；非 Unity 对象会被忽略。</param>
        [HideInCallstack]
        public void Log(LogLevel level, string message, object context = null)
        {
            UnityObject contextObject = context as UnityObject;
            UnityLogKitPlayerOverlay.Record(level, message);
            WriteToUnity(level, message, contextObject);
        }

        /// <summary>
        /// 根据 LogKit 等级选择 Unity Debug 的对应输出方法。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志内容。</param>
        /// <param name="contextObject">Unity 对象上下文。</param>
        [HideInCallstack]
        private static void WriteToUnity(LogLevel level, string message, UnityObject contextObject)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(message, contextObject);
                    break;
                case LogLevel.Error:
                    Debug.LogError(message, contextObject);
                    break;
                default:
                    Debug.Log(message, contextObject);
                    break;
            }
        }
    }
}
#endif
