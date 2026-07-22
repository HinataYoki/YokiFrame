using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;

namespace YokiFrame.Client.Tests.FileBridge;

/// <summary>
/// 覆盖统一 Client 对 engine registry 文件的容错读取。
/// </summary>
public sealed class YokiFrameClientRegistryTests
{
    /// <summary>
    /// 验证外部 registry 显式写入 null 集合时会在协议边界归一为空集合，消费者可安全枚举。
    /// </summary>
    [Fact]
    public void EngineRegistryNormalizesNullCollections()
    {
        var entry = EngineRegistryEntry.FromJson(
            "{\"engineId\":\"unity-editor\",\"capabilities\":null,\"fastChannels\":null}");

        Assert.Empty(entry.Capabilities);
        Assert.Empty(entry.FastChannels);
        Assert.Empty(entry.ExtensionData);
    }

    /// <summary>
    /// 验证 Client 只读取构造时绑定项目的 registry，不会发现另一项目的同名 engine。
    /// </summary>
    [Fact]
    public void EngineListDoesNotCrossProjectBoundary()
    {
        var firstProjectRoot = CreateProjectRoot();
        var secondProjectRoot = CreateProjectRoot();
        try
        {
            var secondEngineRoot = Path.Combine(
                secondProjectRoot,
                ".yokiframe",
                "engines",
                "unity-editor");
            Directory.CreateDirectory(secondEngineRoot);
            File.WriteAllText(
                Path.Combine(secondEngineRoot, "engine.json"),
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"engine\":\"Unity\"}");

            using YokiFrameClient firstClient = new(firstProjectRoot);
            using YokiFrameClient secondClient = new(secondProjectRoot);

            Assert.Empty(firstClient.ReadEngineEntries());
            Assert.Equal("unity-editor", Assert.Single(secondClient.ReadEngineEntries()).EngineId);
        }
        finally
        {
            DeleteProjectRoot(firstProjectRoot);
            DeleteProjectRoot(secondProjectRoot);
        }
    }

    /// <summary>
    /// 验证读取 engine registry 时能容忍 FileBridge 写入侧短暂占用文件。
    /// </summary>
    [Fact]
    public async Task EngineListRetriesTransientFileLock()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor");
            Directory.CreateDirectory(engineRoot);
            var registryPath = Path.Combine(engineRoot, "engine.json");
            await File.WriteAllTextAsync(
                registryPath,
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"engine\":\"Unity\"}");

            // 独占打开模拟原子写入窗口；先等读任务开始，再释放锁，避免竞态假失败。
            FileStream lockStream = new(registryPath, FileMode.Open, FileAccess.Read, FileShare.None);
            using ManualResetEventSlim readerStarted = new(false);
            Task<IReadOnlyList<YokiFrame.Protocol.FileBridge.EngineRegistryEntry>> readTask = Task.Run(() =>
            {
                readerStarted.Set();
                using YokiFrameClient client = new(projectRoot);
                return client.ReadEngineEntries();
            });

            Assert.True(readerStarted.Wait(TimeSpan.FromSeconds(2)), "reader did not start");
            await Task.Delay(100);
            await lockStream.DisposeAsync();

            IReadOnlyList<YokiFrame.Protocol.FileBridge.EngineRegistryEntry> entries =
                await readTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Single(entries);
            Assert.Equal("unity-editor", entries[0].EngineId);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证一个损坏 registry 会携带精确路径，同时保留其它已成功解析的 engine。
    /// </summary>
    [Fact]
    public void EngineListPreservesHealthyEntriesWhenAnotherRegistryIsInvalid()
    {
        var projectRoot = CreateProjectRoot();
        var healthyRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor");
        var brokenRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "broken");
        Directory.CreateDirectory(healthyRoot);
        Directory.CreateDirectory(brokenRoot);
        File.WriteAllText(
            Path.Combine(healthyRoot, "engine.json"),
            "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"engine\":\"Unity\"}");
        var brokenPath = Path.Combine(brokenRoot, "engine.json");
        File.WriteAllText(brokenPath, "{ invalid-json");

        var exception = Assert.Throws<EngineRegistryReadException>(
            () => new YokiFrameClient(projectRoot).ReadEngineEntries());

        Assert.Equal("unity-editor", Assert.Single(exception.ValidEntries).EngineId);
        Assert.Equal(brokenPath, Assert.Single(exception.InvalidPaths));
    }

    /// <summary>
    /// 为 registry 读取测试创建唯一项目根目录。
    /// </summary>
    /// <returns>测试项目根路径。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-engine-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// 删除测试创建的项目目录，缺失时保持幂等。
    /// </summary>
    /// <param name="projectRoot">待删除项目根。</param>
    private static void DeleteProjectRoot(string projectRoot)
    {
        if (Directory.Exists(projectRoot))
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }
}
