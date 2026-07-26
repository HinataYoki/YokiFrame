#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 定义所有宿主与工具共同遵守的 FastChannel v1 framing 常量和消息类型。
    /// </summary>
    public static class YokiFrameFastChannelContract
    {
        /// <summary>
        /// FastChannel 固定魔数，按大端字节序写入后为 ASCII 文本 YFCH。
        /// </summary>
        public const uint MAGIC = 0x59464348u;

        /// <summary>
        /// 当前 FastChannel wire contract 版本。
        /// </summary>
        public const ushort PROTOCOL_VERSION = 1;

        /// <summary>
        /// 固定 header 总字节数：magic、version、message kind、flags 和 payload length。
        /// </summary>
        public const int HEADER_SIZE = 12;

        /// <summary>
        /// 单个完整 FastChannel frame 的最大字节数，包含固定 header。
        /// </summary>
        public const int MAX_FRAME_BYTES = 128 * 1024;

        /// <summary>
        /// 单个 FastChannel frame 的最大 payload 字节数，避免 header 后额外分配超大缓冲区。
        /// </summary>
        public const int MAX_PAYLOAD_BYTES = MAX_FRAME_BYTES - HEADER_SIZE;

        /// <summary>
        /// Registry 中只读命令能力键使用的 Kit/action 分隔符。
        /// </summary>
        public const char COMMAND_KEY_SEPARATOR = '/';

        /// <summary>
        /// magic 在固定 header 中的字节偏移。
        /// </summary>
        public const int MAGIC_OFFSET = 0;

        /// <summary>
        /// protocol version 在固定 header 中的字节偏移。
        /// </summary>
        public const int PROTOCOL_VERSION_OFFSET = 4;

        /// <summary>
        /// message kind 在固定 header 中的字节偏移。
        /// </summary>
        public const int MESSAGE_KIND_OFFSET = 6;

        /// <summary>
        /// flags 在固定 header 中的字节偏移。
        /// </summary>
        public const int FLAGS_OFFSET = 7;

        /// <summary>
        /// payload length 在固定 header 中的字节偏移。
        /// </summary>
        public const int PAYLOAD_LENGTH_OFFSET = 8;

        /// <summary>
        /// 判断消息类型是否属于当前 v1 协议允许的固定集合。
        /// </summary>
        /// <param name="messageKind">待判断的消息类型。</param>
        /// <returns>属于当前版本允许消息类型时返回 true。</returns>
        public static bool IsKnownMessageKind(YokiFrameFastChannelMessageKind messageKind)
        {
            return messageKind == YokiFrameFastChannelMessageKind.Hello
                || messageKind == YokiFrameFastChannelMessageKind.HelloAck
                || messageKind == YokiFrameFastChannelMessageKind.Command
                || messageKind == YokiFrameFastChannelMessageKind.Response
                || messageKind == YokiFrameFastChannelMessageKind.Error;
        }

        /// <summary>
        /// 创建 registry endpoint 使用的稳定 Kit/action 能力键。
        /// </summary>
        /// <param name="kit">已通过 SafeId 校验的 Kit。</param>
        /// <param name="action">已通过 SafeId 校验的 action。</param>
        /// <returns>区分大小写的稳定能力键。</returns>
        public static string CreateCommandKey(string kit, string action)
        {
            return kit + COMMAND_KEY_SEPARATOR + action;
        }
    }

    /// <summary>
    /// 表示 FastChannel v1 固定 header 中的消息类型。
    /// </summary>
    public enum YokiFrameFastChannelMessageKind : byte
    {
        /// <summary>
        /// Client 发起的会话、engine 与 generation 校验请求。
        /// </summary>
        Hello = 1,

        /// <summary>
        /// Host 对已校验 Hello 的成功确认。
        /// </summary>
        HelloAck = 2,

        /// <summary>
        /// Client 发送的只读 command 请求。
        /// </summary>
        Command = 3,

        /// <summary>
        /// Host 对 command 的终态响应。
        /// </summary>
        Response = 4,

        /// <summary>
        /// Host 或 Client 发送的协议、握手或执行错误。
        /// </summary>
        Error = 5
    }
}
#endif
