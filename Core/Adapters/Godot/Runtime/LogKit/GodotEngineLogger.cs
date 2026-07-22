#if GODOT
using System.Text;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// 将 Core LogKit 的日志请求转发到 Godot GD 日志 API。
    /// </summary>
    public sealed class GodotEngineLogger : IEngineLogger
    {
        /// <summary>
        /// 写入不带显式堆栈的日志请求。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志内容。</param>
        /// <param name="context">Godot 上下文对象；会作为文本补充到日志中。</param>
        public void Log(LogLevel level, string message, object context = null)
        {
            string finalMessage = FormatMessage(message, context);
            GodotLogKitPlayerOverlay.Record(level, finalMessage);
            WriteToGodot(level, finalMessage);
        }

        /// <summary>
        /// 根据 LogKit 等级选择 Godot GD 的对应输出方法。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">最终输出文本。</param>
        private static void WriteToGodot(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    GD.PushWarning(message);
                    break;
                case LogLevel.Error:
                    GD.PushError(message);
                    break;
                default:
                    GD.Print(message);
                    break;
            }
        }

        /// <summary>
        /// 合并日志正文和上下文；常规日志保持零额外段落，只有存在上下文时才追加。
        /// </summary>
        /// <param name="message">日志正文。</param>
        /// <param name="context">Godot 上下文对象。</param>
        /// <returns>最终输出文本。</returns>
        private static string FormatMessage(string message, object context)
        {
            string finalMessage = message ?? string.Empty;
            if (context == null)
            {
                return finalMessage;
            }

            var builder = new StringBuilder(finalMessage.Length + 128);
            builder.Append(finalMessage);
            builder.AppendLine();
            builder.Append("Context: ");
            builder.Append(context);

            return builder.ToString();
        }
    }
}
#endif
