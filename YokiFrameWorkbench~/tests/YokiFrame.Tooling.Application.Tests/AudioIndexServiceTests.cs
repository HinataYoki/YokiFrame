using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Services.AudioKit;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 AudioKit 稳定 manifest、冲突检测、路径保护和原子输出。</summary>
public sealed class AudioIndexServiceTests
{
    /// <summary>验证新增排序靠前文件不会改变已有音频 ID。</summary>
    [Fact]
    public void GeneratePreservesIdsWhenNewFileSortsBeforeExistingEntries()
    {
        string projectRoot = CreateProjectRoot();
        WriteAudio(projectRoot, "Assets/Audio/Music/Menu.ogg");
        WriteAudio(projectRoot, "Assets/Audio/Sfx/Click.wav");
        AudioIndexRequest request = CreateRequest(projectRoot);
        AudioIndexService service = new();

        AudioIndexResult first = service.Generate(request);
        WriteAudio(projectRoot, "Assets/Audio/Ambience/Rain.ogg");
        AudioIndexResult second = service.Generate(request);

        Assert.Equal(1001, first.Entries.Single(entry => entry.Path.EndsWith("Menu.ogg")).Id);
        Assert.Equal(1001, second.Entries.Single(entry => entry.Path.EndsWith("Menu.ogg")).Id);
        Assert.Equal(1002, second.Entries.Single(entry => entry.Path.EndsWith("Click.wav")).Id);
        Assert.Equal(1003, second.Entries.Single(entry => entry.Path.EndsWith("Rain.ogg")).Id);
        Assert.Contains("public const int MUSIC_MENU = 1001;", File.ReadAllText(second.GeneratedFile));
        Assert.Empty(Directory.EnumerateFiles(
            Path.GetDirectoryName(second.GeneratedFile)!, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    /// <summary>验证同分类同名不同扩展名会产生明确常量冲突。</summary>
    [Fact]
    public void ScanRejectsConstantNameCollisionWithBothPaths()
    {
        string projectRoot = CreateProjectRoot();
        WriteAudio(projectRoot, "Assets/Audio/Music/Menu.ogg");
        WriteAudio(projectRoot, "Assets/Audio/Music/Menu.wav");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => new AudioIndexService().Scan(CreateRequest(projectRoot)));

        Assert.Contains("MUSIC_MENU", error.Message, StringComparison.Ordinal);
        Assert.Contains("Menu.ogg", error.Message, StringComparison.Ordinal);
        Assert.Contains("Menu.wav", error.Message, StringComparison.Ordinal);
    }

    /// <summary>验证输出与 manifest 都不能逃逸项目根。</summary>
    [Theory]
    [InlineData("../AudioIds.cs", "Assets/Settings/YokiFrame/audio-index.json")]
    [InlineData("Assets/Scripts/Generated/AudioIds.cs", "../audio-index.json")]
    public void GenerateRejectsPathsOutsideProjectRoot(string outputPath, string manifestPath)
    {
        string projectRoot = CreateProjectRoot();
        WriteAudio(projectRoot, "Assets/Audio/Sfx/Click.wav");
        AudioIndexRequest request = CreateRequest(projectRoot) with
        {
            OutputPath = outputPath,
            ManifestPath = manifestPath
        };

        Assert.Throws<InvalidDataException>(() => new AudioIndexService().Generate(request));
    }

    /// <summary>验证 manifest 无法提交时恢复刚刚写入的 C# 映射，不留下半完成索引。</summary>
    [Fact]
    public void GenerateRollsBackSourceWhenManifestCommitFails()
    {
        string projectRoot = CreateProjectRoot();
        WriteAudio(projectRoot, "Assets/Audio/Sfx/Click.wav");
        AudioIndexRequest request = CreateRequest(projectRoot);
        Directory.CreateDirectory(Path.Combine(
            projectRoot,
            "Assets",
            "Settings",
            "YokiFrame",
            "audio-index.json"));

        Exception exception = Assert.ThrowsAny<Exception>(() => new AudioIndexService().Generate(request));
        Assert.True(exception is IOException or UnauthorizedAccessException);

        Assert.False(File.Exists(Path.Combine(
            projectRoot,
            "Assets",
            "Scripts",
            "Generated",
            "AudioIds.cs")));
    }

    /// <summary>创建包含空音频根的唯一项目。</summary>
    private static string CreateProjectRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-audio-index", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets", "Audio"));
        return root;
    }

    /// <summary>写入测试音频占位文件。</summary>
    private static void WriteAudio(string projectRoot, string relativePath)
    {
        string path = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
    }

    /// <summary>创建默认生成请求。</summary>
    private static AudioIndexRequest CreateRequest(string projectRoot)
    {
        return new AudioIndexRequest(
            projectRoot,
            "Assets/Audio",
            "Assets/Scripts/Generated/AudioIds.cs",
            "Assets/Settings/YokiFrame/audio-index.json",
            "Game.Audio",
            "AudioIds",
            1001);
    }
}
