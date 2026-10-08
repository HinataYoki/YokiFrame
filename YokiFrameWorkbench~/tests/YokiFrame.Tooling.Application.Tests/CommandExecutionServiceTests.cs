using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 Workbench 与 CLI 共用的命令执行用例，确保入口层不会各自实现传输选择。
/// </summary>
public sealed class CommandExecutionServiceTests
{
    /// <summary>
    /// 验证无 payload 的 System/ping 优先使用 FastChannel，并向 CLI 暴露明确传输类型。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_空载荷SystemPing_走FastChannel()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();

        CommandExecutionResult result = await new CommandExecutionService(recorder.Client).ExecuteAsync(
            "unity-editor",
            "System",
            "ping",
            "{}",
            "cli",
            2500,
            CancellationToken.None);

        Assert.Equal(1, recorder.FastChannelCallCount);
        Assert.Equal(0, recorder.FileBridgeCallCount);
        Assert.Equal("fast-channel", result.Transport);
        Assert.Equal(string.Empty, result.CommandPath);
        Assert.Equal(string.Empty, result.ResponsePath);
    }

    /// <summary>
    /// 验证 Host 声明为只读的 System 命令即使携带查询 payload 也走 FastChannel，并保留 payload。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SystemPing携带载荷_走FastChannel并保留载荷()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();

        CommandExecutionResult result = await new CommandExecutionService(recorder.Client).ExecuteAsync(
            "unity-editor",
            "System",
            "ping",
            "{\"detail\":true}",
            "cli",
            2500,
            CancellationToken.None);

        Assert.Equal(1, recorder.FastChannelCallCount);
        Assert.Equal(0, recorder.FileBridgeCallCount);
        Assert.Equal("{\"detail\":true}", recorder.LastFastChannelPayloadJson);
        Assert.Equal("fast-channel", result.Transport);
        Assert.Equal(string.Empty, result.CommandPath);
        Assert.Equal(string.Empty, result.ResponsePath);
    }
}
