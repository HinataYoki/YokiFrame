#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示 FastChannel framing 的跨宿主可预期错误；Adapter 可据此关闭连接，工具侧可映射为标准 CLI 错误。
    /// </summary>
    public sealed class YokiFrameFastChannelProtocolException : Exception
    {
        /// <summary>
        /// 创建带稳定错误码和恢复建议的 FastChannel 协议异常。
        /// </summary>
        /// <param name="code">稳定错误码。</param>
        /// <param name="message">错误说明。</param>
        /// <param name="suggestion">恢复建议。</param>
        public YokiFrameFastChannelProtocolException(string code, string message, string suggestion)
            : base(message)
        {
            Code = string.IsNullOrWhiteSpace(code) ? "FastChannelProtocolError" : code;
            Suggestion = string.IsNullOrWhiteSpace(suggestion)
                ? "Close the channel and use FileBridge fallback."
                : suggestion;
        }

        /// <summary>
        /// 获取可跨宿主稳定识别的错误码。
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// 获取建议的恢复动作。
        /// </summary>
        public string Suggestion { get; }
    }
}
#endif
