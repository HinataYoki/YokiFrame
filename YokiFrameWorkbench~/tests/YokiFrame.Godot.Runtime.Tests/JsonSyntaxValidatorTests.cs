using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 验证三宿主共用的纯 C# JSON 语法状态机边界。
/// </summary>
public sealed class JsonSyntaxValidatorTests
{
    /// <summary>
    /// 验证对象、数组、字符串转义、Unicode 转义和标准字面量均可通过。
    /// </summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"a\":[1,true,null]}")]
    [InlineData("{\"text\":\"quote\\\" slash\\\\ newline\\n\"}")]
    [InlineData("{\"text\":\"\\u4e2d\\u6587\"}")]
    [InlineData("-12.5e+2")]
    public void ValidJsonIsAccepted(string json)
    {
        YokiFrameJsonSyntaxValidator.EnsureValidJson(json);
    }

    /// <summary>
    /// 验证缺少分隔符、未闭合结构、尾随字符和非法数字均被拒绝。
    /// </summary>
    [Theory]
    [InlineData("{\"a\":1 \"b\":2}")]
    [InlineData("{\"a\":1")]
    [InlineData("[1,]")]
    [InlineData("{\"a\":\"unterminated}")]
    [InlineData("{\"a\":\"bad\\uZZZZ\"}")]
    [InlineData("01")]
    [InlineData("1.")]
    [InlineData("1e")]
    [InlineData("{} trailing")]
    public void InvalidJsonIsRejected(string json)
    {
        Assert.Throws<FormatException>(() => YokiFrameJsonSyntaxValidator.EnsureValidJson(json));
    }

    /// <summary>
    /// 验证空 payload 保持旧命令兼容语义，按空对象处理。
    /// </summary>
    [Fact]
    public void EmptyPayloadIsAcceptedAsEmptyObject()
    {
        YokiFrameJsonSyntaxValidator.EnsureValidJson(string.Empty);
        YokiFrameJsonSyntaxValidator.EnsureValidJson("   ");
    }
}
