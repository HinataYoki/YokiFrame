using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Validation;

namespace YokiFrame.Protocol.Tests;

/// <summary>
/// 覆盖 FileBridge 路径片段使用的 safe ID 校验规则。
/// </summary>
public sealed class SafeIdValidatorTests
{
    /// <summary>
    /// 验证常见 engine、kit、snapshot 和 requestId 标识可通过校验。
    /// </summary>
    [Theory]
    [InlineData("unity-editor")]
    [InlineData("FsmKit")]
    [InlineData("state")]
    [InlineData("cli-1783429791734-b01525ba")]
    public void SafeIdsAreAccepted(string value)
    {
        Assert.True(SafeIdValidator.IsSafeId(value));
    }

    /// <summary>
    /// 验证路径穿越、空值和不安全字符会被拒绝。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("../evil")]
    [InlineData("bad/name")]
    [InlineData("bad\\name")]
    [InlineData(".hidden")]
    [InlineData("bad..name")]
    public void UnsafeIdsAreRejected(string value)
    {
        Assert.False(SafeIdValidator.IsSafeId(value));
        Assert.Throws<YokiFrameProtocolException>(() => SafeIdValidator.EnsureSafeId(value, "value"));
    }
}
