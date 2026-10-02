using YokiFrame.Tooling.Application.Models.LocalizationKit;
using YokiFrame.Tooling.Application.Services.LocalizationKit;

namespace YokiFrame.Tooling.Application.Tests.LocalizationKit;

/// <summary>验证 LocalizationKit Workbench 的 Luban 工作目录只写入 Editor-only 项目设置。</summary>
public sealed class LocalizationKitSettingsServiceTests
{
    /// <summary>显式工作目录应完整往返，且不能落入 Runtime Settings。</summary>
    [Fact]
    public void SavesAndLoadsLubanWorkDirectoryOutsideRuntimeSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-localization-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            LocalizationKitSettingsService service = new();
            service.Save(root, new LocalizationKitWorkbenchSettings
            {
                LubanWorkDir = "Luban/CustomTemplate"
            });

            LocalizationKitWorkbenchSettings loaded = service.Load(root);

            Assert.Equal("Luban/CustomTemplate", loaded.LubanWorkDir);
            Assert.True(File.Exists(Path.Combine(
                root,
                "ProjectSettings",
                "Packages",
                "com.hinatayoki.yokiframe",
                "localizationkit-settings.json")));
            Assert.False(File.Exists(Path.Combine(
                root,
                "Assets",
                "Settings",
                "Resources",
                "YokiFrame",
                "runtime-settings.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    /// <summary>Godot 项目的工作目录草稿写入 .yokiframe，不创建 Unity ProjectSettings。</summary>
    [Fact]
    public void GodotProjectKeepsWorkbenchDraftOutsideUnityDirectories()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-localization-godot-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5\n");
            LocalizationKitSettingsService service = new();

            service.Save(root, new LocalizationKitWorkbenchSettings { LubanWorkDir = "Localization" });

            Assert.Equal("Localization", service.Load(root).LubanWorkDir);
            string project = File.ReadAllText(Path.Combine(root, "project.godot"));
            Assert.Contains("[yokiframe/editor]", project, StringComparison.Ordinal);
            Assert.Contains("localizationkit/document=", project, StringComparison.Ordinal);
            Assert.Contains("Localization", project, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(root, "ProjectSettings")));
            Assert.False(Directory.Exists(Path.Combine(root, "Assets")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>验证未选择工作目录时，关闭保存不会改写 Godot project.godot。</summary>
    [Fact]
    public void EmptyGodotDraftDoesNotRewriteProjectFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-localization-empty-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "project.godot");
            File.WriteAllText(path, "config_version=5\n");
            DateTime timestamp = new(2002, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, timestamp);

            new LocalizationKitSettingsService().Save(root, new LocalizationKitWorkbenchSettings());

            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
            Assert.Equal("config_version=5\n", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
