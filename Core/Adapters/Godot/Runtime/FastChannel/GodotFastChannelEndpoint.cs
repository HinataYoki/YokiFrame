#if GODOT && TOOLS

using System.Collections.Generic;
namespace YokiFrame
{
    /// <summary>
    /// 描述 Godot Runtime 写入 engine registry 的强类型 FastChannel endpoint。
    /// </summary>
    internal sealed class GodotFastChannelEndpoint
    {
        /// <summary>
        /// 获取或设置 FastChannel wire contract 版本。
        /// </summary>
        public int ProtocolVersion { get; set; } = YokiFrameFastChannelContract.PROTOCOL_VERSION;

        /// <summary>
        /// 获取或设置 endpoint 所属 engine 标识。
        /// </summary>
        public string EngineId { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置 endpoint 所属宿主会话。
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置 endpoint 所属宿主 generation。
        /// </summary>
        public long Generation { get; set; }

        /// <summary>
        /// 获取或设置本机传输类型。
        /// </summary>
        public string Transport { get; set; } = GodotFastChannelTransport.None;

        /// <summary>
        /// 获取或设置 pipe 名称或 Unix socket 路径。
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置工具侧当前是否可以尝试连接。
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 获取或设置 FastChannel 不可用时的可靠回落通道。
        /// </summary>
        public string Fallback { get; set; } = "filebridge";

        /// <summary>
        /// 获取或设置 Host 当前允许通过 FastChannel 执行的只读 Kit/action 能力键。
        /// </summary>
        public List<string> ReadOnlyCommands { get; set; } = new List<string>();

        /// <summary>
        /// 创建 listener 尚未就绪或平台不支持时发布的禁用 endpoint。
        /// </summary>
        /// <param name="engineId">engine 标识。</param>
        /// <param name="sessionId">当前会话标识。</param>
        /// <param name="generation">当前 generation。</param>
        /// <returns>显式回落 FileBridge 的禁用 endpoint。</returns>
        public static GodotFastChannelEndpoint Disabled(
            string engineId,
            string sessionId,
            long generation)
        {
            return new GodotFastChannelEndpoint
            {
                EngineId = engineId,
                SessionId = sessionId,
                Generation = generation
            };
        }

        /// <summary>
        /// 创建 Windows Named Pipe 已就绪 endpoint。
        /// </summary>
        /// <param name="engineId">engine 标识。</param>
        /// <param name="sessionId">当前会话标识。</param>
        /// <param name="generation">当前 generation。</param>
        /// <param name="pipeName">当前用户范围内的安全 pipe 名称。</param>
        /// <returns>启用的 Named Pipe endpoint。</returns>
        public static GodotFastChannelEndpoint NamedPipe(
            string engineId,
            string sessionId,
            long generation,
            string pipeName)
        {
            return CreateEnabled(
                engineId,
                sessionId,
                generation,
                GodotFastChannelTransport.NamedPipe,
                pipeName);
        }

        /// <summary>
        /// 创建 macOS 或 Linux Unix Domain Socket 已就绪 endpoint。
        /// </summary>
        /// <param name="engineId">engine 标识。</param>
        /// <param name="sessionId">当前会话标识。</param>
        /// <param name="generation">当前 generation。</param>
        /// <param name="socketPath">当前 Host 创建的绝对 socket 路径。</param>
        /// <returns>启用的 Unix Domain Socket endpoint。</returns>
        public static GodotFastChannelEndpoint UnixDomainSocket(
            string engineId,
            string sessionId,
            long generation,
            string socketPath)
        {
            return CreateEnabled(
                engineId,
                sessionId,
                generation,
                GodotFastChannelTransport.UnixDomainSocket,
                socketPath);
        }

        /// <summary>
        /// 创建已完成底层 bind 的启用 endpoint，并固定 FileBridge fallback。
        /// </summary>
        /// <param name="engineId">engine 标识。</param>
        /// <param name="sessionId">当前会话标识。</param>
        /// <param name="generation">当前 generation。</param>
        /// <param name="transport">本机传输类型。</param>
        /// <param name="endpoint">传输 endpoint 文本。</param>
        /// <returns>启用 endpoint。</returns>
        private static GodotFastChannelEndpoint CreateEnabled(
            string engineId,
            string sessionId,
            long generation,
            string transport,
            string endpoint)
        {
            return new GodotFastChannelEndpoint
            {
                EngineId = engineId,
                SessionId = sessionId,
                Generation = generation,
                Transport = transport,
                Endpoint = endpoint,
                Enabled = true,
                ReadOnlyCommands = new List<string>()
            };
        }
    }

    /// <summary>
    /// 固定 Godot Runtime 与工具侧 registry contract 共用的本机传输名称。
    /// </summary>
    internal static class GodotFastChannelTransport
    {
        public const string None = "none";
        public const string NamedPipe = "namedPipe";
        public const string UnixDomainSocket = "unixDomainSocket";
    }

    /// <summary>
    /// 表示 FastChannel Hello 与 HelloAck 共享的会话身份 payload。
    /// </summary>
    internal sealed class GodotFastChannelSessionIdentity
    {
        public string EngineId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public long Generation { get; set; }
    }

    /// <summary>
    /// 表示无法继续当前 FastChannel 连接时写入 Error frame 的稳定诊断。
    /// </summary>
    internal sealed class GodotFastChannelError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
    }
}
#endif
