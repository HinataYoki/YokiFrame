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
}
