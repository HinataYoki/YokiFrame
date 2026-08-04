using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.Tests.Commands;

/// <summary>
/// 覆盖统一 Client 的可靠 FileBridge command 写入与 terminal response 轮询。
/// </summary>
public sealed class YokiFrameClientCommandTests
{
    /// <summary>
    /// 验证命令只在原子写入完成后出现，并能读取匹配 requestId 的 terminal response。
    /// </summary>
    [Fact]
    public async Task SendCommandWritesPendingEnvelopeAndReadsTerminalResponse()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var client = new YokiFrameClient(projectRoot);
            var sendTask = client.SendCommandAsync(
                "unity-editor",
                "System",
                "ping",
                "{}",
                "client-tests",
                2000,
                CancellationToken.None);
            var commandPath = await WaitForPendingCommandAsync(client.Paths.GetCommandsRoot("unity-editor"));
            var envelope = CommandEnvelope.FromJson(await File.ReadAllTextAsync(commandPath));
            var responsePath = client.Paths.GetResponsePath("unity-editor", envelope.RequestId);
            Directory.CreateDirectory(Path.GetDirectoryName(responsePath)!);
            await File.WriteAllTextAsync(responsePath, CreateSuccessResponseJson(envelope));

            var result = await sendTask;

            Assert.Equal(commandPath, result.CommandPath);
            Assert.Equal(responsePath, result.ResponsePath);
            Assert.Equal(envelope.RequestId, result.Response.RequestId);
            Assert.Equal("Success", result.Response.Status);
            Assert.Empty(Directory.EnumerateFiles(client.Paths.GetCommandsRoot("unity-editor"), "*.tmp"));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证响应协议版本、requestId、engineId 或状态任一错配时，可靠 FileBridge 不会接受污染证据。
    /// </summary>
    /// <param name="invalidField">需要故意写错的响应字段。</param>
    [Theory]
    [InlineData("protocolVersion")]
    [InlineData("requestId")]
    [InlineData("engineId")]
    [InlineData("status")]
    public async Task SendCommandRejectsMismatchedTerminalResponse(string invalidField)
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var client = new YokiFrameClient(projectRoot);
            var sendTask = client.SendCommandAsync(
                "unity-editor",
                "System",
                "ping",
                "{}",
                "client-tests",
                2000,
                CancellationToken.None);
            var commandPath = await WaitForPendingCommandAsync(client.Paths.GetCommandsRoot("unity-editor"));
            var envelope = CommandEnvelope.FromJson(await File.ReadAllTextAsync(commandPath));
            var responsePath = client.Paths.GetResponsePath("unity-editor", envelope.RequestId);
            Directory.CreateDirectory(Path.GetDirectoryName(responsePath)!);
            await File.WriteAllTextAsync(responsePath, CreateMismatchedResponseJson(envelope, invalidField));

            var exception = await Assert.ThrowsAsync<YokiFrameProtocolException>(() => sendTask);

            Assert.Equal("FileBridgeResponseMismatch", exception.Error.Code);
            Assert.Contains(responsePath, exception.Error.EvidencePaths);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证 request status 能区分 pending、processing、terminal、deadletter 和不存在证据。
    /// </summary>
    [Fact]
    public void ReadCommandStatusReturnsObservableStateAndEvidence()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var client = new YokiFrameClient(projectRoot);
            var engineId = "unity-editor";
            var requestId = "cli-status-request";
            var pendingPath = client.Paths.GetPendingCommandPath(engineId, requestId);
            Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
            File.WriteAllText(pendingPath, "{}");

            var pending = client.ReadCommandStatus(engineId, requestId);
            Assert.Equal(CommandRequestState.Pending, pending.State);
            Assert.Contains(pendingPath, pending.EvidencePaths);

            var processingPath = Path.Combine(
                client.Paths.GetCommandsRoot(engineId),
                "processing",
                requestId + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(processingPath)!);
            File.Move(pendingPath, processingPath);
            var processing = client.ReadCommandStatus(engineId, requestId);
            Assert.Equal(CommandRequestState.Processing, processing.State);

            var responsePath = client.Paths.GetResponsePath(engineId, requestId);
            Directory.CreateDirectory(Path.GetDirectoryName(responsePath)!);
            File.WriteAllText(
                responsePath,
                "{\"protocolVersion\":2,\"requestId\":\""
                + requestId
                + "\",\"engineId\":\""
                + engineId
                + "\",\"status\":\"Success\",\"completedAtUtc\":\"2026-07-01T00:00:00Z\"}");
            var succeeded = client.ReadCommandStatus(engineId, requestId);
            Assert.Equal(CommandRequestState.Succeeded, succeeded.State);
            Assert.True(succeeded.IsTerminal);
            Assert.NotNull(succeeded.Response);

            File.Delete(responsePath);
            File.Delete(processingPath);
            var deadletterInfoPath = Path.Combine(
                client.Paths.GetCommandsRoot(engineId),
                "deadletter",
                requestId + "-deadletter.json");
            Directory.CreateDirectory(Path.GetDirectoryName(deadletterInfoPath)!);
            File.WriteAllText(deadletterInfoPath, "{\"errorCode\":\"InvalidPayload\"}");
            var deadletter = client.ReadCommandStatus(engineId, requestId);
            Assert.Equal(CommandRequestState.Deadletter, deadletter.State);

            File.WriteAllText(
                deadletterInfoPath,
                "{\"errorCode\":\"CommandExecutionUnknown\"}");
            var unknown = client.ReadCommandStatus(engineId, requestId);
            Assert.Equal(CommandRequestState.Unknown, unknown.State);
            Assert.True(unknown.IsTerminal);

            var missing = client.ReadCommandStatus(engineId, "missing-request");
            Assert.Equal(CommandRequestState.NotFound, missing.State);
            Assert.False(missing.IsTerminal);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证 request status 不会把未知状态或污染关联字段的 response 当作失败终态。
    /// </summary>
    [Theory]
    [InlineData("status")]
    [InlineData("requestId")]
    [InlineData("engineId")]
    public void ReadCommandStatusRejectsInvalidTerminalResponse(string invalidField)
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            const string engineId = "unity-editor";
            const string requestId = "cli-status-invalid";
            using var client = new YokiFrameClient(projectRoot);
            var responsePath = client.Paths.GetResponsePath(engineId, requestId);
            Directory.CreateDirectory(Path.GetDirectoryName(responsePath)!);
            var responseStatus = invalidField == "status" ? "InProgress" : "Success";
            var responseRequestId = invalidField == "requestId" ? "other-request" : requestId;
            var responseEngineId = invalidField == "engineId" ? "other-engine" : engineId;
            File.WriteAllText(
                responsePath,
                "{\"protocolVersion\":2,\"requestId\":\""
                + responseRequestId
                + "\",\"engineId\":\""
                + responseEngineId
                + "\",\"status\":\""
                + responseStatus
                + "\"}");

            var exception = Assert.Throws<YokiFrameProtocolException>(
                () => client.ReadCommandStatus(engineId, requestId));

            Assert.Equal("FileBridgeResponseMismatch", exception.Error.Code);
            Assert.Contains(responsePath, exception.Error.EvidencePaths);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 等待原子 move 完成后的 pending command 文件出现，避免读取同目录临时文件。
    /// </summary>
    /// <param name="commandsRoot">pending command 目录。</param>
    /// <returns>已完成写入的 command 文件路径。</returns>
    private static async Task<string> WaitForPendingCommandAsync(string commandsRoot)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (Directory.Exists(commandsRoot))
            {
                var commandPath = Directory.EnumerateFiles(commandsRoot, "*.json").SingleOrDefault();
                if (commandPath != null)
                {
                    return commandPath;
                }
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("等待 pending command 文件超时。");
    }

    /// <summary>
    /// 创建与指定命令匹配的成功响应 JSON。
    /// </summary>
    /// <param name="envelope">已读取的命令信封。</param>
    /// <returns>terminal response JSON。</returns>
    private static string CreateSuccessResponseJson(CommandEnvelope envelope)
    {
        return "{\"protocolVersion\":2,\"requestId\":\"" + envelope.RequestId
            + "\",\"engineId\":\"" + envelope.EngineId
            + "\",\"status\":\"Success\",\"resultJson\":\"{}\",\"errorCode\":\"\",\"errorMessage\":\"\"}";
    }

    /// <summary>
    /// 创建仅一个关联字段不合法的响应，用于覆盖可靠控制面的完整匹配门禁。
    /// </summary>
    /// <param name="envelope">当前命令信封。</param>
    /// <param name="invalidField">需要写错的字段名。</param>
    /// <returns>故意不匹配的 terminal response JSON。</returns>
    private static string CreateMismatchedResponseJson(CommandEnvelope envelope, string invalidField)
    {
        var protocolVersion = invalidField == "protocolVersion"
            ? envelope.ProtocolVersion + 1
            : envelope.ProtocolVersion;
        var requestId = invalidField == "requestId" ? "other-request" : envelope.RequestId;
        var engineId = invalidField == "engineId" ? "other-engine" : envelope.EngineId;
        var status = invalidField == "status" ? string.Empty : "Success";
        return "{\"protocolVersion\":" + protocolVersion
            + ",\"requestId\":\"" + requestId
            + "\",\"engineId\":\"" + engineId
            + "\",\"status\":\"" + status
            + "\",\"resultJson\":\"{}\",\"errorCode\":\"\",\"errorMessage\":\"\"}";
    }

    /// <summary>
    /// 创建唯一测试项目根目录。
    /// </summary>
    /// <returns>测试项目根路径。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-command-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// 清理测试创建的项目目录；目录未创建时不执行操作。
    /// </summary>
    /// <param name="projectRoot">测试项目根目录。</param>
    private static void DeleteProjectRoot(string projectRoot)
    {
        if (Directory.Exists(projectRoot))
        {
            Directory.Delete(projectRoot, true);
        }
    }
}
