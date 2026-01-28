using System.Diagnostics;

namespace YokiFrame
{
    /// <summary>
    /// KitLogger - 条件编译日志宏
    /// 仅在编辑器或开发版本中输出，Release 版本自动移除
    /// </summary>
    public static partial class KitLogger
    {
        /// <summary>
        /// 调试日志（仅编辑器/开发版本）
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DebugLog(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        /// <summary>
        /// 调试日志（仅编辑器/开发版本）
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DebugLog(string message, UnityEngine.Object context)
        {
            UnityEngine.Debug.Log(message, context);
        }

        /// <summary>
        /// 调试警告（仅编辑器/开发版本）
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DebugWarning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        /// <summary>
        /// 调试警告（仅编辑器/开发版本）
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DebugWarning(string message, UnityEngine.Object context)
        {
            UnityEngine.Debug.LogWarning(message, context);
        }

        /// <summary>
        /// 调试错误（仅编辑器/开发版本）
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DebugError(string message)
        {
            UnityEngine.Debug.LogError(message);
        }

        /// <summary>
        /// 调试错误（仅编辑器/开发版本）
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DebugError(string message, UnityEngine.Object context)
        {
            UnityEngine.Debug.LogError(message, context);
        }
    }
}
