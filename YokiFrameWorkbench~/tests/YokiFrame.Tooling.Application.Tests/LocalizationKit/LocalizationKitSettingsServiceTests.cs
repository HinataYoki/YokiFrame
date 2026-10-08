using System.Text.Json;
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

    /// <summary>Godot 项目的工作目录草稿写入 project.godot 编辑区，不创建 Unity 目录。</summary>
    [Fact]
    public void Save_Godot草稿_写入编辑区且不创建Unity目录()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-localization-godot-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5\n");
            LocalizationKitSettingsService service = new();

            service.Save(root, new LocalizationKitWorkbenchSettings { LubanWorkDir = "Localization" });

            Assert.Equal("Localization", service.Load(root).LubanWorkDir);
            Dictionary<string, Dictionary<string, string>> project = ToolingFileObservation.ReadGodot(Path.Combine(root, "project.godot"));
            string encoded = project["[yokiframe/editor]"]["localizationkit/document"];
            using JsonDocument wrapped = JsonDocument.Parse(encoded);
            using JsonDocument document = JsonDocument.Parse(wrapped.RootElement.GetString() ?? string.Empty);
            Assert.Equal("Localization", document.RootElement.GetProperty("LubanWorkDir").GetString());
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
    public void Save_空草稿_不改写project字节()
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
            ToolingFileObservation.AssertUtf8(path, "config_version=5\n");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
