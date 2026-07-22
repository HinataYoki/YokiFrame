using YokiFrame;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Protocol.Validation;

/// <summary>
/// 校验 engine、kit、snapshot、requestId 等会进入路径的安全标识。
/// </summary>
public static class SafeIdValidator
{
    /// <summary>
    /// 判断字符串是否符合 YokiFrame FileBridge 的安全 ID 规则。
    /// </summary>
    /// <param name="value">待检查的标识。</param>
    /// <returns>安全时返回 true，否则返回 false。</returns>
    public static bool IsSafeId(string? value)
    {
        return value != null && YokiFrameSafeIdContract.IsSafeId(value);
    }

    /// <summary>
    /// 校验并返回安全标识；失败时抛出带标准错误码的协议异常。
    /// </summary>
    /// <param name="value">待检查的标识。</param>
    /// <param name="fieldName">调用侧字段名，用于错误提示。</param>
    /// <returns>已确认安全的标识。</returns>
    public static string EnsureSafeId(string? value, string fieldName)
    {
        if (IsSafeId(value))
        {
            return value!;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "InvalidSafeId",
            $"{fieldName} must contain only letters, digits, '-', '_' or '.', and cannot contain path traversal.",
            $"Pass a safe {fieldName} such as unity-editor, FsmKit or state.",
            Array.Empty<string>()));
    }
}
