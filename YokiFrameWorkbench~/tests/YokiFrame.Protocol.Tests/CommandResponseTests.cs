using System.Text.Json;
using YokiFrame.Protocol.FileBridge;

namespace YokiFrame.Protocol.Tests;

/// <summary>
/// 覆盖 FileBridge terminal response 的严格 JSON 边界。
/// </summary>
public sealed class CommandResponseTests
{
    /// <summary>
    /// 验证 JSON 顶层为 null 时不会静默生成空响应。
    /// </summary>
    [Fact]
    public void NullCommandResponseIsRejected()
    {
        Assert.Throws<JsonException>(() => CommandResponse.FromJson("null"));
    }
}
