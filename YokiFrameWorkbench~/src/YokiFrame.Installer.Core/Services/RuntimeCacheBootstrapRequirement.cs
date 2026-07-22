namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 为保留既有 InvalidDataException 契约的 Runtime 缓存前置条件错误提供稳定的结构化标识。
/// </summary>
public static class RuntimeCacheBootstrapRequirement
{
    private const string DATA_KEY = "YokiFrame.RuntimeCacheBootstrapRequired";

    /// <summary>
    /// 创建可由 Application 和 Installer 识别的 Runtime 缓存前置条件异常。
    /// </summary>
    /// <param name="message">面向 Installer、CLI 和日志的完整恢复说明。</param>
    /// <returns>保留 InvalidDataException 类型且带恢复标识的异常。</returns>
    public static InvalidDataException Create(string message)
    {
        InvalidDataException exception = new(message);
        exception.Data[DATA_KEY] = true;
        return exception;
    }

    /// <summary>
    /// 判断异常是否表示需要从当前源码包构建项目 Runtime 缓存的可恢复前置条件。
    /// </summary>
    /// <param name="exception">Installer Core、Application 或 UI 捕获到的异常。</param>
    /// <returns>包含本模块结构化标识时返回 true。</returns>
    public static bool IsRequired(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is InvalidDataException
            && exception.Data[DATA_KEY] is true;
    }
}
