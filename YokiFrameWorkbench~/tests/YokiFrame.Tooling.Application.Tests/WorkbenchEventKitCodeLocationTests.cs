using System.Text.Json;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 EventKit、PoolKit 与 ResKit 源码定位的可靠通道路由、payload 和路径保护。</summary>
public sealed class WorkbenchEventKitCodeLocationTests
{
    /// <summary>验证源码定位只发送一次显式 FileBridge UserAction，并保留相对路径和一基行号。</summary>
    [Fact]
    public async Task OpenCodeLocationUsesOneFileBridgeUserAction()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        string sourcePath = CreateSourceFile(recorder.Client.Paths.ProjectRoot, "Assets/Combat/Emitter.cs");
        try
        {
            var service = new WorkbenchDashboardService(recorder.Client);
            var location = new WorkbenchEventKitCodeLocation("Assets/Combat/Emitter.cs", 42);

            await service.OpenEventKitCodeLocationAsync("unity-editor", location, CancellationToken.None);

            Assert.Equal(0, recorder.FastChannelCallCount);
            Assert.Equal(1, recorder.FileBridgeCallCount);
            Assert.Equal("System", recorder.LastFileBridgeKit);
            Assert.Equal("open_code_location", recorder.LastFileBridgeAction);
            using JsonDocument payload = JsonDocument.Parse(recorder.LastFileBridgePayloadJson);
            Assert.Equal("Assets/Combat/Emitter.cs", payload.RootElement.GetProperty("filePath").GetString());
            Assert.Equal(42, payload.RootElement.GetProperty("line").GetInt32());
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    /// <summary>验证绝对路径、Assets 逃逸和非 C# 文件都会在进入 transport 前被拒绝。</summary>
    [Theory]
    [InlineData("C:/outside/Test.cs")]
    [InlineData("../outside/Test.cs")]
    [InlineData("Assets/Combat/Test.txt")]
    public async Task InvalidCodeLocationIsRejectedBeforeTransport(string filePath)
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        var service = new WorkbenchDashboardService(recorder.Client);
        var location = new WorkbenchEventKitCodeLocation(filePath, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.OpenEventKitCodeLocationAsync("unity-editor", location, CancellationToken.None));

        Assert.Equal(0, recorder.FastChannelCallCount);
        Assert.Equal(0, recorder.FileBridgeCallCount);
    }

    /// <summary>验证宿主 terminal error 会投影为调用失败，不能把已到达响应误报为已打开。</summary>
    [Fact]
    public async Task HostTerminalErrorIsReportedToCaller()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        recorder.FileBridgeResponseStatus = "Error";
        recorder.FileBridgeErrorMessage = "code editor unavailable";
        string sourcePath = CreateSourceFile(recorder.Client.Paths.ProjectRoot, "Assets/Combat/Receiver.cs");
        try
        {
            var service = new WorkbenchDashboardService(recorder.Client);
            var location = new WorkbenchEventKitCodeLocation("Assets/Combat/Receiver.cs", 8);

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.OpenEventKitCodeLocationAsync("unity-editor", location, CancellationToken.None));

            Assert.Equal("code editor unavailable", error.Message);
            Assert.Equal(1, recorder.FileBridgeCallCount);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    /// <summary>验证 ResKit 调试符号产生的绝对 Assets 路径会规范化后走同一 UserAction。</summary>
    [Fact]
    public async Task ResKitAbsoluteSourcePathIsNormalizedInsideProjectAssets()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        string sourcePath = CreateSourceFile(recorder.Client.Paths.ProjectRoot, "Assets/Audio/Loader.cs");
        try
        {
            var service = new WorkbenchDashboardService(recorder.Client);

            await service.OpenResKitCodeLocationAsync(
                "unity-editor", sourcePath, 27, CancellationToken.None);

            Assert.Equal(0, recorder.FastChannelCallCount);
            Assert.Equal(1, recorder.FileBridgeCallCount);
            using JsonDocument payload = JsonDocument.Parse(recorder.LastFileBridgePayloadJson);
            Assert.Equal("Assets/Audio/Loader.cs", payload.RootElement.GetProperty("filePath").GetString());
            Assert.Equal(27, payload.RootElement.GetProperty("line").GetInt32());
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    /// <summary>验证 PoolKit 堆栈产生的绝对 Assets 路径复用同一受保护 UserAction。</summary>
    [Fact]
    public async Task PoolKitAbsoluteSourcePathIsNormalizedInsideProjectAssets()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        string sourcePath = CreateSourceFile(recorder.Client.Paths.ProjectRoot, "Assets/Runtime/PoolCaller.cs");
        try
        {
            var service = new WorkbenchDashboardService(recorder.Client);

            await service.OpenPoolKitCodeLocationAsync(
                "unity-editor", sourcePath, 36, CancellationToken.None);

            Assert.Equal(0, recorder.FastChannelCallCount);
            Assert.Equal(1, recorder.FileBridgeCallCount);
            using JsonDocument payload = JsonDocument.Parse(recorder.LastFileBridgePayloadJson);
            Assert.Equal("Assets/Runtime/PoolCaller.cs", payload.RootElement.GetProperty("filePath").GetString());
            Assert.Equal(36, payload.RootElement.GetProperty("line").GetInt32());
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    /// <summary>验证 ResKit 来源越过项目 Assets 时在进入 transport 前被拒绝。</summary>
    [Fact]
    public async Task ResKitSourceOutsideAssetsIsRejectedBeforeTransport()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        var service = new WorkbenchDashboardService(recorder.Client);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenResKitCodeLocationAsync(
            "unity-editor", Path.Combine(Path.GetTempPath(), "outside.cs"), 1, CancellationToken.None));

        Assert.Equal(0, recorder.FastChannelCallCount);
        Assert.Equal(0, recorder.FileBridgeCallCount);
    }

    /// <summary>在代理的隔离项目根创建一个最小 C# 文件。</summary>
    private static string CreateSourceFile(string projectRoot, string relativePath)
    {
        string fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "namespace Tests; internal sealed class Marker { }");
        return fullPath;
    }
}
