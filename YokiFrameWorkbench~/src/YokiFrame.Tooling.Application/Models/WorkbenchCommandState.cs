namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述 Workbench 发送 System 命令后的响应状态。
/// </summary>
public sealed class WorkbenchCommandState
{
    /// <summary>
    /// 创建命令响应状态。
    /// </summary>
    /// <param name="action">命令 action。</param>
    /// <param name="ok">命令是否成功得到响应。</param>
    /// <param name="status">Runtime response 状态。</param>
    /// <param name="resultJson">业务结果 JSON。</param>
    /// <param name="errorMessage">失败说明。</param>
    public WorkbenchCommandState(string action, bool ok, string status, string resultJson, string errorMessage)
        : this("System", action, ok, status, resultJson, errorMessage)
    {
    }

    /// <summary>
    /// 创建命令响应状态。
    /// </summary>
    /// <param name="kit">命令 Kit。</param>
    /// <param name="action">命令 action。</param>
    /// <param name="ok">命令是否成功得到响应。</param>
    /// <param name="status">Runtime response 状态。</param>
    /// <param name="resultJson">业务结果 JSON。</param>
    /// <param name="errorMessage">失败说明。</param>
    public WorkbenchCommandState(string kit, string action, bool ok, string status, string resultJson, string errorMessage)
    {
        Kit = kit;
        Action = action;
        Ok = ok;
        Status = status;
        ResultJson = resultJson;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 获取命令 Kit。
    /// </summary>
    public string Kit { get; }

    /// <summary>
    /// 获取命令 action。
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// 获取命令是否成功得到 terminal response。
    /// </summary>
    public bool Ok { get; }

    /// <summary>
    /// 获取 Runtime response 状态。
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// 获取业务结果 JSON。
    /// </summary>
    public string ResultJson { get; }

    /// <summary>
    /// 获取失败说明。
    /// </summary>
    public string ErrorMessage { get; }
}
