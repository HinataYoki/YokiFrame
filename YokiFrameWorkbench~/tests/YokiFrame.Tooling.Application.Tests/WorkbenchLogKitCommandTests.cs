using System.Reflection;
using System.Text.Json;
using YokiFrame.Client;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 LogKit Application 用例的结构化 payload 和原子命令结果。</summary>
public sealed class WorkbenchLogKitCommandTests
{
    private const string STATE_JSON = """
        {"schemaVersion":1,"diagnosticVersion":2,"settingsVersion":3,"stats":{"loggerName":"TestLogger","hasLogger":true,"enabled":true,"minimumLevel":"Debug","historyCount":0,"droppedCount":0},"settings":{"enabled":true,"minimumLevel":"Debug","saveLogInEditor":false,"saveLogInPlayer":true,"enableIMGUIInPlayer":false,"enableEncryption":true,"maxQueueSize":20000,"maxSameLogCount":50,"maxRetentionDays":15,"maxFileSizeMB":100,"imguiMaxLogCount":200,"logDirectory":"","editorFileName":"yoki_editor.log","playerFileName":"yoki_player.log"},"capabilities":{"settingsApply":true,"filePreview":true,"fileWriter":false,"playerImGui":false,"encryption":false},"files":{"directory":"","editor":{"kind":"editor","path":"","fileName":"yoki_editor.log","exists":false,"sizeBytes":0,"modifiedUtc":""},"player":{"kind":"player","path":"","fileName":"yoki_player.log","exists":false,"sizeBytes":0,"modifiedUtc":""}},"history":{"entries":[],"count":0,"totalCount":0,"droppedCount":0,"truncated":false}}
        """;

    /// <summary>验证 clear_history 使用空对象并直接解析完整新 state。</summary>
    [Fact]
    public async Task ClearHistoryReturnsAtomicState()
    {
        var recorder = RecordingClient.Create(CreateProjectRoot());
        var service = new WorkbenchDashboardService(recorder.Client);

        var state = await service.ClearLogKitHistoryAsync("unity-editor", CancellationToken.None);

        Assert.Equal("clear_history", recorder.LastAction);
        Assert.Equal("{}", recorder.LastPayloadJson);
        Assert.Equal("command", state.Source);
        Assert.Equal(0, state.History.TotalCount);
        Assert.Equal(2, state.DiagnosticVersion);
    }

    /// <summary>验证 state 命令等待期间宿主换代时拒绝旧会话回包。</summary>
    [Fact]
    public async Task ClearHistoryRejectsChangedHostIdentity()
    {
        var recorder = RecordingClient.Create(CreateProjectRoot());
        recorder.CommandSent = () => recorder.RegistryGeneration++;
        var service = new WorkbenchDashboardService(recorder.Client);

        var exception = await Assert.ThrowsAsync<YokiFrameProtocolException>(() =>
            service.ClearLogKitHistoryAsync("unity-editor", CancellationToken.None));

        Assert.Equal("LogKitCommandIdentityChanged", exception.Error.Code);
        Assert.Equal(new[] { "command.json", "response.json" }, exception.Error.EvidencePaths);
    }

    /// <summary>验证文件读取 payload 由 Application 构造，并保留文件错误和传输。</summary>
    [Fact]
    public async Task ReadFileBuildsKindPayloadAndPreservesErrorMessage()
    {
        var recorder = RecordingClient.Create(CreateProjectRoot());
        recorder.FilePreviewJson = "{\"kind\":\"editor\",\"path\":\"C:/x.log\",\"fileName\":\"x.log\",\"exists\":false,\"sizeBytes\":0,\"modifiedUtc\":\"\",\"lineCount\":0,\"truncated\":false,\"content\":\"\",\"errorMessage\":\"not found\"}";
        var service = new WorkbenchDashboardService(recorder.Client);

        var preview = await service.ReadLogKitFileAsync(
            "unity-editor", "EDITOR", CancellationToken.None);

        Assert.Equal("read_log_file", recorder.LastAction);
        using var payload = JsonDocument.Parse(recorder.LastPayloadJson);
        Assert.Equal("editor", payload.RootElement.GetProperty("kind").GetString());
        Assert.Equal("not found", preview.ErrorMessage);
        Assert.Equal("file-bridge", preview.Transport);
    }

    /// <summary>验证文件预览不能用回包 kind 偷换用户选择的来源。</summary>
    [Fact]
    public async Task ReadFileRejectsMismatchedResponseKind()
    {
        var recorder = RecordingClient.Create(CreateProjectRoot());
        recorder.FilePreviewJson = "{\"kind\":\"player\",\"exists\":false,\"sizeBytes\":0,\"lineCount\":0,\"truncated\":false,\"content\":\"\",\"errorMessage\":\"\"}";
        var service = new WorkbenchDashboardService(recorder.Client);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ReadLogKitFileAsync(
            "unity-editor", "editor", CancellationToken.None));
    }

    /// <summary>验证文件预览等待期间宿主换代时不会展示旧会话文件内容。</summary>
    [Fact]
    public async Task ReadFileRejectsChangedHostIdentity()
    {
        var recorder = RecordingClient.Create(CreateProjectRoot());
        recorder.FilePreviewJson = "{\"kind\":\"editor\",\"exists\":true,\"sizeBytes\":4,\"lineCount\":1,\"truncated\":false,\"content\":\"old\",\"errorMessage\":\"\"}";
        recorder.CommandSent = () => recorder.RegistrySessionId = "restarted-session";
        var service = new WorkbenchDashboardService(recorder.Client);

        var exception = await Assert.ThrowsAsync<YokiFrameProtocolException>(() =>
            service.ReadLogKitFileAsync("unity-editor", "editor", CancellationToken.None));

        Assert.Equal("LogKitCommandIdentityChanged", exception.Error.Code);
        Assert.Equal(new[] { "command.json", "response.json" }, exception.Error.EvidencePaths);
    }

    /// <summary>验证保存命令使用顶层完整 settings，且分别报告项目保存和 Runtime 应用。</summary>
    [Fact]
    public async Task SaveUsesFlatCompleteSettingsPayload()
    {
        var projectRoot = CreateProjectRoot();
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
        var recorder = RecordingClient.Create(projectRoot);
        var service = new WorkbenchDashboardService(recorder.Client);
        var loaded = service.LoadLogKitProjectSettings("unity-editor");

        var result = await service.SaveLogKitSettingsAsync(
            "unity-editor",
            loaded.Settings with { MinimumLevel = "Error", MaxQueueSize = 256 },
            loaded.Fingerprint,
            CancellationToken.None);

        Assert.True(result.ProjectSaved);
        Assert.True(result.RuntimeApplied);
        Assert.NotNull(result.AppliedState);
        Assert.Equal("set_settings", recorder.LastAction);
        using var payload = JsonDocument.Parse(recorder.LastPayloadJson);
        Assert.False(payload.RootElement.TryGetProperty("settings", out _));
        Assert.Equal("Error", payload.RootElement.GetProperty("minimumLevel").GetString());
        Assert.Equal(256, payload.RootElement.GetProperty("maxQueueSize").GetInt32());
    }

    /// <summary>验证旧会话设置回包不会把 Runtime 应用状态错误标记为成功。</summary>
    [Fact]
    public async Task SaveDoesNotReportRuntimeAppliedAfterHostIdentityChanges()
    {
        var projectRoot = CreateProjectRoot();
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
        var recorder = RecordingClient.Create(projectRoot);
        recorder.CommandSent = () => recorder.RegistryGeneration++;
        var service = new WorkbenchDashboardService(recorder.Client);
        var loaded = service.LoadLogKitProjectSettings("unity-editor");

        var result = await service.SaveLogKitSettingsAsync(
            "unity-editor",
            loaded.Settings with { MinimumLevel = "Error" },
            loaded.Fingerprint,
            CancellationToken.None);

        Assert.True(result.ProjectSaved);
        Assert.False(result.RuntimeApplied);
        Assert.Null(result.AppliedState);
        Assert.Contains("host session or generation", result.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>验证其它项目的 registry 不能驱动当前项目设置写入。</summary>
    [Fact]
    public async Task SaveRejectsEngineFromAnotherProject()
    {
        var projectRoot = CreateProjectRoot();
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
        var recorder = RecordingClient.Create(projectRoot);
        recorder.RegistryProjectPath = CreateProjectRoot();
        var service = new WorkbenchDashboardService(recorder.Client);

        var result = await service.SaveLogKitSettingsAsync(
            "unity-editor",
            WorkbenchLogKitSettings.CreateDefault(),
            "missing",
            CancellationToken.None);

        Assert.True(result.ProjectSaved);
        Assert.False(result.RuntimeApplied);
        Assert.Equal(0, recorder.CommandCount);
        Assert.Contains("another project", result.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>验证 Unity Host 断线时仍保存项目配置，只把当前会话应用报告为失败。</summary>
    [Fact]
    public async Task SavePersistsUnityProjectWhileRuntimeIsOffline()
    {
        var projectRoot = CreateProjectRoot();
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
        var service = new WorkbenchDashboardService(projectRoot);
        var loaded = service.LoadLogKitProjectSettings("unity-editor");

        var result = await service.SaveLogKitSettingsAsync(
            "unity-editor",
            loaded.Settings with { MinimumLevel = "Warning" },
            loaded.Fingerprint,
            CancellationToken.None);

        Assert.True(result.ProjectSaved);
        Assert.False(result.RuntimeApplied);
        Assert.Contains("Runtime was not applied", result.ErrorMessage, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            projectRoot,
            "Assets",
            "Settings",
            "Resources",
            "YokiFrame",
            "runtime-settings.json")));
    }

    /// <summary>创建唯一项目根。</summary>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-logkit-command-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>记录 Dashboard 对统一 Client 的调用。</summary>
    public class RecordingClient : DispatchProxy
    {
        private IYokiFrameClient mClient = null!;
        private YokiFramePaths mPaths = null!;

        /// <summary>获取代理 Client。</summary>
        public IYokiFrameClient Client => mClient;
        /// <summary>获取可覆盖的 registry 项目路径。</summary>
        public string RegistryProjectPath { get; set; } = string.Empty;
        /// <summary>获取或设置当前 registry 会话。</summary>
        public string RegistrySessionId { get; set; } = "command-session";
        /// <summary>获取或设置当前 registry 代际。</summary>
        public long RegistryGeneration { get; set; } = 5L;
        /// <summary>获取或设置命令返回前执行的场景动作。</summary>
        public Action? CommandSent { get; set; }
        /// <summary>获取最近 action。</summary>
        public string LastAction { get; private set; } = string.Empty;
        /// <summary>获取最近 payload。</summary>
        public string LastPayloadJson { get; private set; } = string.Empty;
        /// <summary>获取命令次数。</summary>
        public int CommandCount { get; private set; }
        /// <summary>获取或设置文件预览响应。</summary>
        public string FilePreviewJson { get; set; } = string.Empty;

        /// <summary>创建绑定项目根的记录代理。</summary>
        public static RecordingClient Create(string projectRoot)
        {
            var client = DispatchProxy.Create<IYokiFrameClient, RecordingClient>();
            var recorder = (RecordingClient)(object)client;
            recorder.mClient = client;
            recorder.mPaths = new YokiFramePaths(projectRoot);
            recorder.RegistryProjectPath = projectRoot;
            return recorder;
        }

        /// <summary>映射 Dashboard 所需最小 Client 成员。</summary>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments)
        {
            return targetMethod?.Name switch
            {
                "get_Paths" => mPaths,
                nameof(IYokiFrameClient.ReadEngineEntries) => CreateRegistryEntries(),
                "CanSendFastChannelReadOnlyCommand" => false,
                nameof(IYokiFrameClient.SendCommandAsync) => SendCommand(arguments),
                _ => throw new NotSupportedException("LogKit test client does not support " + targetMethod?.Name)
            };
        }

        /// <summary>创建当前测试 registry。</summary>
        private IReadOnlyList<EngineRegistryEntry> CreateRegistryEntries()
        {
            return new[]
            {
                new EngineRegistryEntry
                {
                    ProtocolVersion = 2,
                    EngineId = "unity-editor",
                    Engine = "Unity",
                    ProjectPath = RegistryProjectPath,
                    SessionId = RegistrySessionId,
                    Generation = RegistryGeneration,
                    Mode = "PlayMode"
                }
            };
        }

        /// <summary>记录命令并返回与 action 匹配的 terminal response。</summary>
        private Task<CommandSendResult> SendCommand(object?[]? arguments)
        {
            var engineId = Assert.IsType<string>(arguments![0]);
            LastAction = Assert.IsType<string>(arguments[2]);
            LastPayloadJson = Assert.IsType<string>(arguments[3]);
            CommandCount++;
            CommandSent?.Invoke();
            var resultJson = LastAction == "read_log_file" ? FilePreviewJson : STATE_JSON;
            var response = new CommandResponse
            {
                ProtocolVersion = 2,
                RequestId = "log-request",
                EngineId = engineId,
                Status = "Success",
                ResultJson = resultJson,
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            var envelope = new CommandEnvelope
            {
                ProtocolVersion = 2,
                RequestId = "log-request",
                EngineId = engineId,
                Source = "workbench",
                Kit = "LogKit",
                Action = LastAction,
                PayloadJson = LastPayloadJson,
                TimeoutMs = 10000
            };
            return Task.FromResult(new CommandSendResult(
                envelope, "command.json", "response.json", response));
        }
    }
}
