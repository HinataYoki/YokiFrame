namespace YokiFrame.Protocol.FastChannel;

/// <summary>
/// 定义 FastChannel 支持的传输类型常量，避免 JSON enum 数值影响协议可读性。
/// </summary>
public static class FastChannelTransport
{
    /// <summary>
    /// 表示当前 engine 未启用 FastChannel。
    /// </summary>
    public const string None = "none";

    /// <summary>
    /// 表示 Windows 本机 Named Pipe 通道。
    /// </summary>
    public const string NamedPipe = "namedPipe";

    /// <summary>
    /// 表示 macOS / Linux 本机 Unix Domain Socket 通道。
    /// </summary>
    public const string UnixDomainSocket = "unixDomainSocket";

    /// <summary>
    /// 表示仅用于远程调试或特殊集成的 HTTP loopback 通道。
    /// </summary>
    public const string Http = "http";

    /// <summary>
    /// 表示仅用于浏览器面板或远程调试的 WebSocket 通道。
    /// </summary>
    public const string WebSocket = "webSocket";
}
