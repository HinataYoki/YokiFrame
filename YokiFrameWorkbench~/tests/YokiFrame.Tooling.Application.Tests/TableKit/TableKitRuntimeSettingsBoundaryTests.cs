using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.TableKit;

namespace YokiFrame.Tooling.Application.Tests.TableKit;

/// <summary>验证 TableKit Workbench 草稿与 Runtime resourcePath 契约保持分域。</summary>
public sealed class TableKitRuntimeSettingsBoundaryTests
{
    /// <summary>保存 TableKit 草稿不会创建或修改 Runtime Settings 文件。</summary>
    [Fact]
    public void DraftSaveDoesNotCreateRuntimeSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-runtime-boundary-" + Guid.NewGuid().ToString("N"));
        try
        {
            TableKitOptions options = new()
            {
                ProjectRoot = root,
                LubanConfigPath = Path.Combine(root, "Luban", "luban.conf")
            };

            new TableKitSettingsService().Save(root, options);

            Assert.False(File.Exists(Path.Combine(root, "Assets/Settings/Resources/YokiFrame/runtime-settings.json")));
            Assert.True(File.Exists(Path.Combine(
                root,
                "ProjectSettings/Packages/com.hinatayoki.yokiframe/tablekit-settings.json")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
