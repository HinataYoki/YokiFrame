using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.TableKit;

namespace YokiFrame.Tooling.Application.Tests.TableKit;

/// <summary>验证 TableKit 项目设置对当前配置模型的完整持久化。</summary>
public sealed class TableKitSettingsServiceTests
{
    /// <summary>额外输出的代码 target 与代码目录可以完整往返。</summary>
    [Fact]
    public void SavesAndLoadsExtraCodeOutput()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            TableKitSettingsService service = new();
            TableKitOptions options = CreateOptions(root);

            service.Save(root, options);
            TableKitOptions loaded = service.Load(root, options);

            TableKitExtraOutput extra = Assert.Single(loaded.ExtraOutputTargets);
            Assert.Equal("server", extra.TargetName);
            Assert.Equal("java-json", extra.CodeTarget);
            Assert.Equal("Temp/LubanExtra/server/code", extra.OutputCodeDir);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>用户配置的程序集名称通过项目草稿原样往返。</summary>
    [Fact]
    public void SavesConfiguredAssemblyName()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            TableKitSettingsService service = new();
            TableKitOptions options = CreateOptions(root) with { AssemblyName = "Game.TableKit" };
            service.Save(root, options);

            TableKitOptions loaded = service.Load(root, CreateOptions(root));

            Assert.Equal("Game.TableKit", loaded.AssemblyName);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>可寻址开关和路径模板可以通过项目草稿完整往返。</summary>
    [Fact]
    public void SavesAndLoadsRuntimeLocationSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            TableKitSettingsService service = new();
            TableKitOptions options = CreateOptions(root) with
            {
                IsAddressable = false,
                RuntimePathPattern = "user://tables/{0}"
            };

            service.Save(root, options);
            TableKitOptions loaded = service.Load(root, CreateOptions(root));

            Assert.False(loaded.IsAddressable);
            Assert.Equal("user://tables/{0}", loaded.RuntimePathPattern);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>保存结果只包含当前资源定位字段，不写入已删除的三态契约。</summary>
    [Fact]
    public void WritesOnlyCurrentRuntimeLocationFields()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            TableKitSettingsService service = new();
            service.Save(root, CreateOptions(root) with
            {
                IsAddressable = true,
                RuntimePathPattern = string.Empty
            });
            string settingsJson = File.ReadAllText(Path.Combine(
                root,
                "ProjectSettings",
                "Packages",
                "com.hinatayoki.yokiframe",
                "tablekit-settings.json"));
            Assert.Contains("\"IsAddressable\": true", settingsJson, StringComparison.Ordinal);
            Assert.Contains("\"RuntimePathPattern\": \"\"", settingsJson, StringComparison.Ordinal);
            Assert.DoesNotContain("RuntimeLocationMode", settingsJson, StringComparison.Ordinal);
            Assert.DoesNotContain("ExplicitRuntimePath", settingsJson, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>创建包含完整额外输出的测试设置。</summary>
    /// <param name="root">测试项目根。</param>
    /// <returns>可持久化设置。</returns>
    private static TableKitOptions CreateOptions(string root)
    {
        return new TableKitOptions
        {
            ProjectRoot = root,
            LubanConfigPath = Path.Combine(root, "Luban", "luban.conf"),
            ExtraOutputTargets = new[]
            {
                new TableKitExtraOutput
                {
                    TargetName = "server",
                    CodeTarget = "java-json",
                    DataTarget = "json",
                    OutputDataDir = "Temp/LubanExtra/server/data",
                    OutputCodeDir = "Temp/LubanExtra/server/code"
                }
            }
        };
    }
}
