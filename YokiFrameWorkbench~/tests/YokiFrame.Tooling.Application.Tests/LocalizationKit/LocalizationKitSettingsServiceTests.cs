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
}
