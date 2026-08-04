namespace YokiFrame.Protocol.Results;

/// <summary>
/// 描述命令执行结果的可观察状态。
/// </summary>
public enum CommandOutcomeState
{
    /// <summary>
    /// 已收到成功的 terminal response。
    /// </summary>
    Succeeded,

    /// <summary>
    /// 已收到明确表示失败的 terminal response，或请求在发送前被拒绝。
    /// </summary>
    Failed,

    /// <summary>
    /// 请求已经尝试发送，但在期限内没有可验证的 terminal response；Runtime 可能仍在执行。
    /// </summary>
    Unknown,

    /// <summary>
    /// 调用方取消了等待；该状态不表示 Runtime 一定没有执行请求。
    /// </summary>
    Cancelled
}
