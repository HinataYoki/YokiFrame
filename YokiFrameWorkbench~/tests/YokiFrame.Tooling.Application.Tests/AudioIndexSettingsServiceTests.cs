using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Services.AudioKit;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 AudioKit 索引设置的默认值、项目隔离和原子持久化。</summary>
public sealed class AudioIndexSettingsServiceTests
{
    /// <summary>验证缺失配置时返回新的项目默认值。</summary>
    [Fact]
    public void MissingSettingsReturnAudioArtFolderAndGameAudioNamespace()
    {
        using TestProject project = new();
        AudioIndexSettings settings = new AudioIndexSettingsService(project.Root).Load();

        Assert.Equal("Assets/Art/Audio", settings.ScanFolder);
        Assert.Equal("GameAudio", settings.NamespaceName);
    }

    /// <summary>验证保存后重新创建服务仍能恢复全部字段。</summary>
    [Fact]
    public async Task SavedSettingsSurviveServiceRecreation()
    {
        using TestProject project = new();
        AudioIndexSettings expected = new(
            "Assets/Art/Audio/Desktop", "Assets/Game/AudioIds.cs",
            "Assets/Game/audio-manifest.json", "Project.Audio", "SoundIds", 1200);

        await new AudioIndexSettingsService(project.Root).SaveAsync(expected, CancellationToken.None);
        AudioIndexSettings actual = new AudioIndexSettingsService(project.Root).Load();

        Assert.Equal(expected, actual);
    }

    /// <summary>验证两个项目的同名设置文件不会互相污染。</summary>
    [Fact]
    public async Task SettingsAreIsolatedPerProjectRoot()
    {
        using TestProject first = new();
        using TestProject second = new();
        AudioIndexSettings firstSettings = AudioIndexSettings.CreateDefault() with { NamespaceName = "FirstAudio" };
        AudioIndexSettings secondSettings = AudioIndexSettings.CreateDefault() with { NamespaceName = "SecondAudio" };

        await new AudioIndexSettingsService(first.Root).SaveAsync(firstSettings, CancellationToken.None);
        await new AudioIndexSettingsService(second.Root).SaveAsync(secondSettings, CancellationToken.None);

        Assert.Equal("FirstAudio", new AudioIndexSettingsService(first.Root).Load().NamespaceName);
        Assert.Equal("SecondAudio", new AudioIndexSettingsService(second.Root).Load().NamespaceName);
    }

    /// <summary>验证配置写入 YokiFrame 统一 Editor Settings 且不残留临时文件。</summary>
    [Fact]
    public async Task SaveUsesProjectSettingsAndCleansTemporaryFiles()
    {
        using TestProject project = new();
        AudioIndexSettingsService service = new(project.Root);

        await service.SaveAsync(AudioIndexSettings.CreateDefault(), CancellationToken.None);

        Assert.Equal(
            Path.Combine(project.Root, "ProjectSettings", "Packages", "com.hinatayoki.yokiframe", "editor-settings.json"),
            service.SettingsPath);
        Assert.True(File.Exists(service.SettingsPath));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(service.SettingsPath)!, "*.tmp"));
    }

    /// <summary>验证损坏和异常大的配置不会静默回退为默认值。</summary>
    [Fact]
    public async Task InvalidSettingsProduceExplicitFailure()
    {
        using TestProject project = new();
        AudioIndexSettingsService service = new(project.Root);
        Directory.CreateDirectory(Path.GetDirectoryName(service.SettingsPath)!);
        await File.WriteAllTextAsync(service.SettingsPath, "{ invalid-json");
        Assert.Throws<InvalidDataException>(() => service.Load());

        await File.WriteAllBytesAsync(service.SettingsPath, new byte[1024 * 1024 + 1]);
        Assert.Throws<InvalidDataException>(() => service.Load());
    }

    /// <summary>验证保存 AudioKit 时保留统一文件中的其它 Kit 配置。</summary>
    [Fact]
    public async Task SavePreservesOtherKitEntries()
    {
        using TestProject project = new();
        AudioIndexSettingsService service = new(project.Root);
        Directory.CreateDirectory(Path.GetDirectoryName(service.SettingsPath)!);
        await File.WriteAllTextAsync(service.SettingsPath,
            "{\"formatVersion\":1,\"settings\":[{\"kit\":\"LogKit\",\"key\":\"minimumLevel\",\"value\":\"Debug\"}]}" );

        await service.SaveAsync(AudioIndexSettings.CreateDefault() with { NamespaceName = "PreservedAudio" }, CancellationToken.None);

        string json = await File.ReadAllTextAsync(service.SettingsPath);
        Assert.Contains("LogKit", json, StringComparison.Ordinal);
        Assert.Contains("PreservedAudio", json, StringComparison.Ordinal);
    }

    /// <summary>验证历史 AudioKit 独立文件会自动迁移到统一 Editor Settings。</summary>
    [Fact]
    public async Task LegacySettingsAreMigratedAndRemoved()
    {
        using TestProject project = new();
        AudioIndexSettingsService service = new(project.Root);
        Directory.CreateDirectory(Path.GetDirectoryName(service.LegacySettingsPath)!);
        await File.WriteAllTextAsync(service.LegacySettingsPath,
            "{\"formatVersion\":1,\"scanFolder\":\"Assets/Art/Audio/Desktop\",\"outputPath\":\"Assets/Generated.cs\",\"manifestPath\":\"Assets/audio.json\",\"namespaceName\":\"LegacyAudio\",\"className\":\"Ids\",\"startId\":2000}");

        AudioIndexSettings settings = service.Load();

        Assert.Equal("LegacyAudio", settings.NamespaceName);
        Assert.True(File.Exists(service.SettingsPath));
        Assert.False(File.Exists(service.LegacySettingsPath));
    }

    /// <summary>创建和清理一个隔离测试项目根。</summary>
    private sealed class TestProject : IDisposable
    {
        /// <summary>初始化带 Assets 目录的临时项目。</summary>
        internal TestProject()
        {
            Root = Path.Combine(Path.GetTempPath(), "yokiframe-audio-settings", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "Assets"));
        }

        /// <summary>获取测试项目绝对根路径。</summary>
        internal string Root { get; }

        /// <summary>删除测试项目目录。</summary>
        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
