#if GODOT
using Godot;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// IEngineLogger 的 Godot 实现，转发到 GD.Print 系列 API。
    /// </summary>
    public sealed class GodotEngineLogger : IEngineLogger
    {
        public void Log(LogLevel level, string message, object context = null)
        {
            var finalMessage = context == null
                ? message
                : message + " | Context: " + context;

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
    }
}
#endif
