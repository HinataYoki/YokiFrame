using System.Text.Json;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Tooling.Application.Services.LogKit;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 LogKit 项目设置的隔离、冲突检测和原子保存。</summary>
public sealed class LogKitRuntimeSettingsServiceTests
{
    /// <summary>验证保存替换 LogKit 条目但保留其它 Kit，并清理临时文件。</summary>
    [Fact]
    public async Task SavePreservesOtherKitsAndCommitsAtomically()
    {
        var projectRoot = CreateProjectRoot();
        var service = new LogKitRuntimeSettingsService(projectRoot);
        WriteSettings(service.SettingsPath, """
            {"formatVersion":1,"settings":[{"kit":"EventKit","key":"enabled","value":"true"},{"kit":"LogKit","key":"minimumLevel","value":"Debug"}]}
            """);
        var loaded = service.LoadUnitySettings("unity-editor");
        var settings = loaded.Settings with
        {
            MinimumLevel = "warning",
            MaxQueueSize = 512,
            SaveLogInEditor = true,
            EditorFileName = "editor-only.log"
        };

        var result = await service.SaveUnitySettingsAsync(
            "unity-editor", settings, loaded.Fingerprint, CancellationToken.None);

        Assert.True(result.ProjectSaved);
        Assert.False(result.RuntimeApplied);
        Assert.Equal("Warning", result.ProjectSettings.Settings.MinimumLevel);
        Assert.Equal(512, result.ProjectSettings.Settings.MaxQueueSize);
        using var document = JsonDocument.Parse(File.ReadAllText(service.SettingsPath));
        var entries = document.RootElement.GetProperty("settings").EnumerateArray().ToArray();
        Assert.Contains(entries, static entry => entry.GetProperty("kit").GetString() == "EventKit");
        Assert.DoesNotContain(entries, static entry =>
            entry.GetProperty("key").GetString() is "saveLogInEditor" or "editorFileName");
        using var editorDocument = JsonDocument.Parse(File.ReadAllText(service.EditorSettingsPath));
        var editorEntries = editorDocument.RootElement.GetProperty("settings").EnumerateArray().ToArray();
        Assert.Contains(editorEntries, static entry =>
            entry.GetProperty("key").GetString() == "saveLogInEditor"
            && entry.GetProperty("value").GetString() == "true");
        Assert.Contains(editorEntries, static entry =>
            entry.GetProperty("key").GetString() == "editorFileName"
            && entry.GetProperty("value").GetString() == "editor-only.log");
        Assert.Empty(Directory.EnumerateFiles(
            Path.GetDirectoryName(service.SettingsPath)!, "runtime-settings.json.tmp-*"));
    }

    /// <summary>验证页面指纹过期时拒绝覆盖外部修改。</summary>
    [Fact]
    public async Task SaveRejectsFingerprintConflictWithoutOverwritingExternalChange()
    {
        var projectRoot = CreateProjectRoot();
        var service = new LogKitRuntimeSettingsService(projectRoot);
        WriteSettings(service.SettingsPath, "{\"formatVersion\":1,\"settings\":[]}");
        var loaded = service.LoadUnitySettings("unity-editor");
        const string external = "{\"formatVersion\":1,\"settings\":[{\"kit\":\"EventKit\",\"key\":\"owner\",\"value\":\"external\"}]}";
        File.WriteAllText(service.SettingsPath, external);

        var result = await service.SaveUnitySettingsAsync(
            "unity-editor",
            loaded.Settings with { MaxQueueSize = 123 },
            loaded.Fingerprint,
            CancellationToken.None);

        Assert.False(result.ProjectSaved);
        Assert.True(result.ConflictDetected);
        Assert.Equal(external, File.ReadAllText(service.SettingsPath));
    }

    /// <summary>验证缺失文件使用稳定指纹并可创建完整配置。</summary>
    [Fact]
    public async Task SaveCreatesMissingSettingsFileFromCoreDefaults()
    {
        var service = new LogKitRuntimeSettingsService(CreateProjectRoot());
        var loaded = service.LoadUnitySettings("unity-editor");

        var result = await service.SaveUnitySettingsAsync(
            "unity-editor", loaded.Settings, loaded.Fingerprint, CancellationToken.None);

        Assert.False(loaded.Exists);
        Assert.Equal("missing", loaded.Fingerprint);
        Assert.True(result.ProjectSaved);
        Assert.True(File.Exists(service.SettingsPath));
        Assert.True(File.Exists(service.EditorSettingsPath));
        Assert.True(result.ProjectSettings.Settings.EnableEncryption);
    }

    /// <summary>验证重新加载会合并 Editor 文件，同时拒绝 Editor 字段回流 Resources。</summary>
    [Fact]
    public async Task LoadMergesIsolatedEditorSettingsWithoutPollutingRuntimeJson()
    {
        var service = new LogKitRuntimeSettingsService(CreateProjectRoot());
        var loaded = service.LoadUnitySettings("unity-editor");
        var expected = loaded.Settings with
        {
            SaveLogInEditor = true,
            EditorFileName = "persisted-editor.log"
        };

        await service.SaveUnitySettingsAsync(
            "unity-editor", expected, loaded.Fingerprint, CancellationToken.None);
        var reloaded = service.LoadUnitySettings("unity-editor");

        Assert.True(reloaded.Settings.SaveLogInEditor);
        Assert.Equal("persisted-editor.log", reloaded.Settings.EditorFileName);
        string runtimeJson = File.ReadAllText(service.SettingsPath);
        Assert.DoesNotContain("saveLogInEditor", runtimeJson, StringComparison.Ordinal);
        Assert.DoesNotContain("editorFileName", runtimeJson, StringComparison.Ordinal);
    }

    /// <summary>验证 Workbench 与 Core 对日志目录使用同一 4096 字符边界，不会提前拒绝 Runtime 可接受的设置。</summary>
    [Fact]
    public async Task SaveAcceptsDirectoryLengthAllowedByRuntimeContract()
    {
        var service = new LogKitRuntimeSettingsService(CreateProjectRoot());
        var loaded = service.LoadUnitySettings("unity-editor");
        var directory = new string('a', 2048);

        var result = await service.SaveUnitySettingsAsync(
            "unity-editor",
            loaded.Settings with { LogDirectory = directory },
            loaded.Fingerprint,
            CancellationToken.None);

        Assert.True(result.ProjectSaved);
        Assert.Equal(directory, result.ProjectSettings.Settings.LogDirectory);
    }

    /// <summary>验证路径策略拒绝绝对路径和越出当前项目的相对路径。</summary>
    [Fact]
    public void ResolveContainedPathRejectsPathEscape()
    {
        var root = CreateProjectRoot();

        Assert.Throws<ArgumentException>(() =>
            LogKitRuntimeSettingsService.ResolveContainedPath(root, "../outside.json"));
        Assert.Throws<ArgumentException>(() =>
            LogKitRuntimeSettingsService.ResolveContainedPath(root, Path.GetFullPath("outside.json")));
    }

    /// <summary>创建唯一测试项目根。</summary>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-logkit-settings-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>创建目录并写入设置原文。</summary>
    private static void WriteSettings(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }
}
