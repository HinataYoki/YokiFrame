#if GODOT
using Godot;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// IEngineLogger 的 Godot 实现，转发到 GD.Print 系列 API。
    /// </summary>
    public sealed class GodotEngineLogger : IEngineLoggerWithStackTrace
    {
        public void Log(LogLevel level, string message, object context = null)
        {
            Log(level, message, context, null);
        }

        public void Log(LogLevel level, string message, object context, string stackTrace)
        {
            var finalMessage = context == null
                ? message
                : message + " | Context: " + context;
            finalMessage = AppendStackTrace(finalMessage, stackTrace);

            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    GD.Print(finalMessage);
                    break;
                case LogLevel.Warning:
                    GD.PushWarning(finalMessage);
                    break;
                case LogLevel.Error:
                    GD.PushError(finalMessage);
                    break;
            }
        }

        internal static IEngineLogger WrapLegacyLogger(IEngineLogger logger)
        {
            if (logger == null || logger is IEngineLoggerWithStackTrace)
                return logger;

            return new GodotStackTraceEngineLoggerAdapter(logger);
        }

        internal static string AppendStackTrace(string message, string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return message;

            var finalMessage = message ?? string.Empty;
            if (finalMessage.IndexOf(stackTrace, System.StringComparison.Ordinal) >= 0)
                return finalMessage;

            return finalMessage + "\n" + stackTrace;
        }
    }

    internal sealed class GodotStackTraceEngineLoggerAdapter : IEngineLoggerWithStackTrace
    {
        private readonly IEngineLogger mInner;

        public GodotStackTraceEngineLoggerAdapter(IEngineLogger inner)
        {
            mInner = inner;
        }

        public void Log(LogLevel level, string message, object context = null)
        {
            mInner.Log(level, message, context);
        }

        public void Log(LogLevel level, string message, object context, string stackTrace)
        {
            mInner.Log(level, GodotEngineLogger.AppendStackTrace(message, stackTrace), context);
        }
    }
}
#endif
