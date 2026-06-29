#if !GODOT
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using YokiFrame;
using UnityObject = UnityEngine.Object;

namespace YokiFrame.Unity
{
    /// <summary>
    /// IEngineLogger 的 Unity 实现，转发到 UnityEngine.Debug。
    /// </summary>
    public sealed class UnityEngineLogger : IEngineLoggerWithStackTrace
    {
        /// <inheritdoc />
        [HideInCallstack]
        public void Log(LogLevel level, string message, object context = null)
        {
            Log(level, message, context, null);
        }

        /// <inheritdoc />
        [HideInCallstack]
        public void Log(LogLevel level, string message, object context, string stackTrace)
        {
            LogWithUnityStack(level, message, context as UnityObject);
        }

        internal static IEngineLogger WrapLegacyLogger(IEngineLogger logger)
        {
            if (logger == null || logger is IEngineLoggerWithStackTrace)
                return logger;

            return new UnityStackTraceEngineLoggerAdapter(logger);
        }

        [HideInCallstack]
        internal static void LogWithUnityStack(LogLevel level, string message, UnityObject contextObject)
        {
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
                default:
                    Debug.Log(message, contextObject);
                    break;
            }
        }

        internal static LogLevel ToLogLevel(LogType logType, LogLevel fallbackLevel)
        {
            switch (logType)
            {
                case LogType.Warning:
                    return LogLevel.Warning;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return LogLevel.Error;
                case LogType.Log:
                    return fallbackLevel == LogLevel.Debug ? LogLevel.Debug : LogLevel.Info;
                default:
                    return fallbackLevel;
            }
        }
    }

    internal sealed class UnityStackTraceEngineLoggerAdapter : IEngineLoggerWithStackTrace
    {
        private static readonly object sUnityLogHandlerLock = new object();
        private readonly IEngineLogger mInner;

        public UnityStackTraceEngineLoggerAdapter(IEngineLogger inner)
        {
            mInner = inner;
        }

        [HideInCallstack]
        public void Log(LogLevel level, string message, object context = null)
        {
            mInner.Log(level, message, context);
        }

        [HideInCallstack]
        public void Log(LogLevel level, string message, object context, string stackTrace)
        {
            CapturingUnityLogHandler captureHandler;
            lock (sUnityLogHandlerLock)
            {
                var unityLogger = Debug.unityLogger;
                var previousHandler = unityLogger.logHandler;
                captureHandler = new CapturingUnityLogHandler();

                try
                {
                    unityLogger.logHandler = captureHandler;
                    mInner.Log(level, message, context);
                }
                finally
                {
                    unityLogger.logHandler = previousHandler;
                }
            }

            for (var i = 0; i < captureHandler.CapturedLogs.Count; i++)
            {
                var capturedLog = captureHandler.CapturedLogs[i];
                var capturedLevel = UnityEngineLogger.ToLogLevel(capturedLog.Type, level);
                UnityEngineLogger.LogWithUnityStack(capturedLevel, capturedLog.Message, capturedLog.Context);
            }
        }
    }

    internal sealed class CapturingUnityLogHandler : ILogHandler
    {
        public readonly List<CapturedUnityLog> CapturedLogs = new List<CapturedUnityLog>();

        [HideInCallstack]
        public void LogFormat(LogType logType, UnityObject context, string format, params object[] args)
        {
            CapturedLogs.Add(new CapturedUnityLog(logType, FormatMessage(format, args), context));
        }

        [HideInCallstack]
        public void LogException(Exception exception, UnityObject context)
        {
            CapturedLogs.Add(new CapturedUnityLog(LogType.Exception, exception != null ? exception.ToString() : "null exception", context));
        }

        private static string FormatMessage(string format, object[] args)
        {
            if (string.IsNullOrEmpty(format))
                return string.Empty;

            if (args == null || args.Length == 0)
                return format;

            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }

    internal readonly struct CapturedUnityLog
    {
        public readonly LogType Type;
        public readonly string Message;
        public readonly UnityObject Context;

        public CapturedUnityLog(LogType type, string message, UnityObject context)
        {
            Type = type;
            Message = message;
            Context = context;
        }
    }

}
#endif
