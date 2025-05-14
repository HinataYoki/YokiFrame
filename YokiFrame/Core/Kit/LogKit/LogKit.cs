using UnityEngine;

namespace YokiFrame
{
    public class LogKit
    {
        public enum LogLevel { None, Error, Warning, All }
        public static LogLevel Level { get; set; } = LogLevel.All;
        
        public static void Log(string message, Object context = null)
        {
            if (context != null)
            {
                Debug.Log($"{message}", context);
            }
            else
            {
                Debug.Log($"{message}");
            }
        }

        public static void Log<T>(string message, Object context = null)
        {
            if (Level >= LogLevel.Warning)
            {
                if (context != null)
                {
                    Debug.Log($"[{typeof(T).Name}]: {message}", context);
                }
                else
                {
                    Debug.Log($"[{typeof(T).Name}]: {message}");
                }
            }
        }

        public static void Warning(string message, Object context = null)
        {
            if (context != null)
            {
                Debug.LogWarning($"{message}", context);
            }
            else
            {
                Debug.LogWarning($"{message}");
            }
        }

        public static void Warning<T>(string message, Object context = null)
        {
            if (Level >= LogLevel.Warning)
            {
                if (context != null)
                {
                    Debug.LogWarning($"[{typeof(T).Name}]: {message}", context);
                }
                else
                {
                    Debug.LogWarning($"[{typeof(T).Name}]: {message}");
                }
            }
        }

        public static void Error(string message, Object context = null)
        {
            if (context != null)
            {
                Debug.LogError($"{message}", context);
            }
            else
            {
                Debug.LogError($"{message}");
            }
        }

        public static void Error<T>(string message, Object context = null)
        {
            if (Level >= LogLevel.Error)
            {
                if (context != null)
                {
                    Debug.LogError($"[{typeof(T).Name}]: {message}", context);
                }
                else
                {
                    Debug.LogError($"[{typeof(T).Name}]: {message}");
                }
            }
        }

        public static void Exception(System.Exception message, Object context = null)
        {
            if (context != null)
            {
                Debug.LogException(message, context);
            }
            else
            {
                Debug.LogException(message);
            }
        }
    }
}