namespace YokiFrame.Protocol.Results;

/// <summary>
/// 表示协议层可预期失败；CLI 会把该异常转换成标准 JSON 错误。
/// </summary>
public sealed class YokiFrameProtocolException : Exception
{
    /// <summary>
    /// 使用标准错误对象创建协议异常。
    /// </summary>
    /// <param name="error">可被 CLI 直接输出的错误信息。</param>
    public YokiFrameProtocolException(YokiFrameError error)
        : base(error.Message)
    {
        Error = error;
    }

    /// <summary>
    /// 获取标准错误信息。
    /// </summary>
    public YokiFrameError Error { get; }

    /// <summary>获取关联请求标识；非请求类错误为空。</summary>
    public string RequestId => Error.RequestId;

    /// <summary>获取关联宿主标识；非宿主错误为空。</summary>
    public string EngineId => Error.EngineId;

    /// <summary>获取产生错误的传输标识；未知时为空。</summary>
    public string Transport => Error.Transport;

    /// <summary>获取可复查证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths => Error.EvidencePaths;
}
