using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>项目内绝对路径写入草稿时必须转换为基于项目根的可搬运相对路径。</summary>
    [Fact]
    public void SavesProjectPathsAsPortableRelativeValues()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-portable-" + Guid.NewGuid().ToString("N"));
        try
        {
            TableKitOptions options = CreateOptions(root) with
            {
                LubanWorkDir = Path.Combine(root, "Luban", "MiniTemplate"),
                LubanExecutablePath = Path.Combine(root, "Luban", "Tools", "Luban", "Luban.dll"),
                OutputDataDir = Path.Combine(root, "Assets", "Resources", "Art", "Table"),
                OutputCodeDir = Path.Combine(root, "Assets", "Scripts", "TableKit"),
                EditorDataPath = Path.Combine(root, "Assets", "Resources", "Art", "Table")
            };

            new TableKitSettingsService().Save(root, options);

            JsonObject draft = ReadDraft(root);
            Assert.Equal(".", draft["ProjectRoot"]!.GetValue<string>());
            Assert.Equal("Luban/luban.conf", draft["LubanConfigPath"]!.GetValue<string>());
            Assert.Equal("Luban/MiniTemplate", draft["LubanWorkDir"]!.GetValue<string>());
            Assert.Equal("Luban/Tools/Luban/Luban.dll", draft["LubanExecutablePath"]!.GetValue<string>());
            Assert.Equal("Assets/Resources/Art/Table", draft["OutputDataDir"]!.GetValue<string>());
            Assert.Equal("Assets/Scripts/TableKit", draft["OutputCodeDir"]!.GetValue<string>());
            Assert.Equal("Assets/Resources/Art/Table", draft["EditorDataPath"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>旧草稿中位于旧项目根下的绝对路径必须重定位到当前项目并在保存时迁移。</summary>
    [Fact]
    public void LoadsAndMigratesLegacyAbsoluteProjectPaths()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-current-" + Guid.NewGuid().ToString("N"));
        string legacyRoot = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-legacy-" + Guid.NewGuid().ToString("N"));
        try
        {
            TableKitOptions defaults = CreateOptions(root);
            TableKitOptions legacy = CreateOptions(legacyRoot) with
            {
                OutputDataDir = Path.Combine(legacyRoot, "Assets", "Resources", "Legacy", "Tables"),
                OutputCodeDir = Path.Combine(legacyRoot, "Assets", "Scripts", "LegacyTableKit")
            };
            string settingsPath = GetDraftPath(root);
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(legacy));
            TableKitSettingsService service = new();

            TableKitOptions loaded = service.Load(root, defaults);
            service.Save(root, loaded);

            Assert.Equal(Path.Combine(root, "Luban", "luban.conf"), loaded.LubanConfigPath);
            Assert.Equal("Assets/Resources/Legacy/Tables", loaded.OutputDataDir);
            Assert.Equal("Assets/Scripts/LegacyTableKit", loaded.OutputCodeDir);
            JsonObject migrated = ReadDraft(root);
            Assert.Equal(".", migrated["ProjectRoot"]!.GetValue<string>());
            Assert.Equal("Luban/luban.conf", migrated["LubanConfigPath"]!.GetValue<string>());
            Assert.False(Path.IsPathFullyQualified(migrated["OutputDataDir"]!.GetValue<string>()));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(legacyRoot)) Directory.Delete(legacyRoot, true);
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
            Assert.DoesNotContain("UseAsyncLoading", settingsJson, StringComparison.Ordinal);
            Assert.DoesNotContain("RuntimeLocationMode", settingsJson, StringComparison.Ordinal);
            Assert.DoesNotContain("ExplicitRuntimePath", settingsJson, StringComparison.Ordinal);
            JsonObject runtimeSettings = JsonNode.Parse(File.ReadAllText(Path.Combine(
                root,
                "Assets",
                "Settings",
                "Resources",
                "YokiFrame",
                "runtime-settings.json")))!.AsObject();
            JsonObject[] tableKitSettings = runtimeSettings["settings"]!.AsArray()
                .Select(static item => item!.AsObject())
                .Where(static item => item["kit"]!.GetValue<string>() == "TableKit")
                .ToArray();
            Assert.Equal(2, tableKitSettings.Length);
            Assert.Contains(tableKitSettings, static item =>
                item["key"]!.GetValue<string>() == "runtimePathPattern"
                && item["value"]!.GetValue<string>() == "{0}");
            Assert.Contains(tableKitSettings, static item =>
                item["key"]!.GetValue<string>() == "useRawResourceLoading"
                && item["value"]!.GetValue<string>() == "true");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>旧草稿中的异步开关应被兼容读取，并在下一次保存时从文档中移除。</summary>
    [Fact]
    public void LegacyAsyncSwitchIsIgnoredAndRemovedOnSave()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            TableKitOptions defaults = CreateOptions(root);
            string settingsPath = Path.Combine(
                root,
                "ProjectSettings",
                "Packages",
                "com.hinatayoki.yokiframe",
                "tablekit-settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, """
                {
                  "ProjectRoot": "legacy",
                  "LubanConfigPath": "Luban/luban.conf",
                  "UseAsyncLoading": true,
                  "UseRawResourceLoading": false
                }
                """);
            TableKitSettingsService service = new();

            TableKitOptions loaded = service.Load(root, defaults);
            service.Save(root, loaded);

            Assert.False(loaded.UseRawResourceLoading);
            Assert.DoesNotContain("UseAsyncLoading", File.ReadAllText(settingsPath), StringComparison.Ordinal);
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
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
        File.WriteAllText(
            Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"),
            "m_EditorVersion: 2022.3.0f1");
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

    /// <summary>读取测试项目当前 TableKit 草稿根对象。</summary>
    /// <param name="root">测试项目根。</param>
    /// <returns>已解析的草稿 JSON。</returns>
    private static JsonObject ReadDraft(string root)
    {
        return JsonNode.Parse(File.ReadAllText(GetDraftPath(root)))!.AsObject();
    }

    /// <summary>取得测试项目 TableKit 草稿绝对路径。</summary>
    /// <param name="root">测试项目根。</param>
    /// <returns>项目设置目录中的草稿路径。</returns>
    private static string GetDraftPath(string root)
    {
        return Path.Combine(
            root,
            "ProjectSettings",
            "Packages",
            "com.hinatayoki.yokiframe",
            "tablekit-settings.json");
    }
}
