using YokiFrame.Tooling.Application.Services.SaveKit;

namespace YokiFrame.Tooling.Application.Tests.SaveKit;

/// <summary>覆盖 SaveKit Workbench 配置合并、并发冲突和文件元信息扫描。</summary>
public sealed class SaveKitWorkbenchSettingsServiceTests
{
    /// <summary>Unity 保存只替换 SaveKit 条目，并扫描 slots/global 文件。</summary>
    [Fact]
    public async Task SavesUnitySettingsAndScansSaveFiles()
    {
        string root = CreateRoot();
        try
        {
            string config = Path.Combine(root, "Assets/Settings/Resources/YokiFrame/runtime-settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(config)!);
            File.WriteAllText(config, "{\"formatVersion\":1,\"settings\":[{\"kit\":\"LogKit\",\"key\":\"enabled\",\"value\":\"true\"},{\"kit\":\"SaveKit\",\"key\":\"fileExtension\",\"value\":\"old\"}]}");
            string saveRoot = Path.Combine(root, "Saves");
            Directory.CreateDirectory(Path.Combine(saveRoot, "slots"));
            Directory.CreateDirectory(Path.Combine(saveRoot, "global"));
            File.WriteAllBytes(Path.Combine(saveRoot, "slots/save_3.bin"), new byte[7]);
            File.WriteAllBytes(Path.Combine(saveRoot, "global/settings.bin"), new byte[11]);
            var service = new SaveKitWorkbenchSettingsService(root);
            var loaded = service.Load("unity-editor");
            var result = await service.SaveAsync("unity-editor", "Saves", "bin", loaded.Fingerprint, CancellationToken.None);
            Assert.True(result.Saved);
            Assert.Equal(".bin", result.Settings.FileExtension);
            Assert.Equal(1, result.Settings.SlotCount);
            Assert.Equal(1, result.Settings.GlobalCount);
            Assert.Contains("LogKit", File.ReadAllText(config));
        }
        finally { DeleteRoot(root); }
    }

    /// <summary>配置文件指纹变化时拒绝覆盖并报告冲突。</summary>
    [Fact]
    public async Task RejectsStaleFingerprint()
    {
        string root = CreateRoot();
        try
        {
            var service = new SaveKitWorkbenchSettingsService(root);
            var loaded = service.Load("unity-editor");
            string config = Path.Combine(root, "Assets/Settings/Resources/YokiFrame/runtime-settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(config)!);
            File.WriteAllText(config, "changed");
            var result = await service.SaveAsync("unity-editor", "Saves", ".yoki", loaded.Fingerprint, CancellationToken.None);
            Assert.True(result.Conflict);
            Assert.False(result.Saved);
        }
        finally { DeleteRoot(root); }
    }

    /// <summary>Workbench 保存配置时拒绝会破坏 Runtime 文件定位和扫描语义的扩展名。</summary>
    [Theory]
    [InlineData(".")]
    [InlineData(".save*")]
    [InlineData("../save")]
    public async Task RejectsUnsafeFileExtensions(string extension)
    {
        string root = CreateRoot();
        try
        {
            var service = new SaveKitWorkbenchSettingsService(root);
            var loaded = service.Load("unity-editor");

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SaveAsync("unity-editor", "Saves", extension, loaded.Fingerprint, CancellationToken.None));
        }
        finally { DeleteRoot(root); }
    }

    /// <summary>Godot 保存仅维护 yokiframe/runtime section 并保留其它项目设置。</summary>
    [Fact]
    public async Task PatchesGodotRuntimeSection()
    {
        string root = CreateRoot();
        try
        {
            string path = Path.Combine(root, "project.godot");
            File.WriteAllText(path, "config_version=5\n\n[display]\nwindow/size=1280\n");
            var service = new SaveKitWorkbenchSettingsService(root);
            var loaded = service.Load("godot-editor");
            var result = await service.SaveAsync("godot-editor", "Saves", "save", loaded.Fingerprint, CancellationToken.None);
            string text = File.ReadAllText(path);
            Assert.True(result.Saved);
            Assert.Contains("[display]", text);
            Assert.Contains("[yokiframe/runtime]", text);
            Assert.Contains("save_kit/file_extension=\".save\"", text);
        }
        finally { DeleteRoot(root); }
    }

    /// <summary>创建隔离测试目录。</summary>
    private static string CreateRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-savekit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>删除测试目录，并记录不会影响断言结果的文件系统清理失败。</summary>
    private static void DeleteRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Console.Error.WriteLine("无法清理 SaveKit 测试目录: " + exception.Message);
        }
    }
}
