using System.Text.Json;
using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.TableKit;

namespace YokiFrame.Tooling.Application.Tests.TableKit;

/// <summary>验证 TableKit Workbench 草稿与 Runtime Settings 保持正确分域。</summary>
public sealed class TableKitRuntimeSettingsBoundaryTests
{
    /// <summary>宿主和资源定位尚未确定时只保存草稿，不生成猜测的 Runtime Settings。</summary>
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

    /// <summary>Unity 保存只投影两个真实运行时设置，并清理历史死键。</summary>
    [Fact]
    public void Save_Unity运行时字段_写入设置并排除编辑器路径()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-runtime-unity-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Assets", "Settings", "Resources", "YokiFrame"));
            Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
            File.WriteAllText(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 2022.3.0f1");
            string runtimePath = Path.Combine(root, "Assets", "Settings", "Resources", "YokiFrame", "runtime-settings.json");
            File.WriteAllText(runtimePath, """
                {
                  "formatVersion": 1,
                  "settings": [
                    { "kit": "TableKit", "key": "resourceRoot", "value": "legacy" },
                    { "kit": "TableKit", "key": "dataExtension", "value": "bytes" },
                    { "kit": "TableKit", "key": "useAsyncLoading", "value": "true" },
                    { "kit": "OtherKit", "key": "enabled", "value": "true" }
                  ]
                }
                """);
            TableKitOptions options = new()
            {
                ProjectRoot = root,
                LubanConfigPath = Path.Combine(root, "Luban", "luban.conf"),
                IsAddressable = false,
                RuntimePathPattern = "Art/Config/{0}",
                UseRawResourceLoading = false,
                CustomEditorDataPath = true,
                EditorDataPath = "Assets/EditorOnly/Tables"
            };

            new TableKitSettingsService().Save(root, options);

            AssertUnityRuntimeProjection(runtimePath);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>只修改 Workbench 草稿字段时不重复替换内容相同的 Unity Runtime Settings。</summary>
    [Fact]
    public void DraftOnlyChangeDoesNotRewriteUnchangedRuntimeSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-runtime-stable-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Assets"));
            Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
            File.WriteAllText(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 2022.3.0f1");
            TableKitSettingsService service = new();
            TableKitOptions options = new()
            {
                ProjectRoot = root,
                LubanConfigPath = Path.Combine(root, "Luban", "luban.conf"),
                IsAddressable = true,
                UseRawResourceLoading = false
            };
            service.Save(root, options);
            string runtimePath = Path.Combine(root, "Assets", "Settings", "Resources", "YokiFrame", "runtime-settings.json");
            DateTime stableTimestamp = new(2002, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(runtimePath, stableTimestamp);

            service.Save(root, options with
            {
                CodeTarget = "cs-dotnet-json",
                DataTarget = "json",
                EditorDataPath = "Assets/EditorOnly/Tables"
            });

            Assert.Equal(stableTimestamp, File.GetLastWriteTimeUtc(runtimePath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>Godot 保存使用 ProjectSettings 路径，并保持其它 project.godot section 不变。</summary>
    [Fact]
    public void Save_Godot运行时区_写入表路径并保留应用区()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-runtime-godot-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string projectPath = Path.Combine(root, "project.godot");
            File.WriteAllText(
                projectPath,
                "[application]\nconfig/name=\"Game\"\n\n[yokiframe/runtime]\ntable_kit/use_async_loading=\"true\"\n");
            TableKitOptions options = new()
            {
                ProjectRoot = root,
                LubanConfigPath = Path.Combine(root, "Luban", "luban.conf"),
                OutputDataDir = "Data/Tables",
                UseRawResourceLoading = true
            };

            new TableKitSettingsService().Save(root, options);

            Dictionary<string, Dictionary<string, string>> projectSettings = ToolingFileObservation.ReadGodot(projectPath);
            Assert.Equal("\"Game\"", projectSettings["[application]"]["config/name"]);
            Assert.Equal("\"res://Data/Tables/{0}\"", projectSettings["[yokiframe/runtime]"]["table_kit/runtime_path_pattern"]);
            Assert.Equal("\"true\"", projectSettings["[yokiframe/runtime]"]["table_kit/use_raw_resource_loading"]);
            Assert.False(projectSettings["[yokiframe/runtime]"].ContainsKey("table_kit/use_async_loading"));
            Assert.True(projectSettings.Values.SelectMany(static section => section.Keys).Contains("tablekit/document"));
            Assert.All(
                projectSettings.Values.SelectMany(static section => section),
                static setting => Assert.DoesNotContain(
                    ".yokiframe",
                    setting.Value.Split('/', '\\', '"')));
            Assert.False(Directory.Exists(Path.Combine(root, "ProjectSettings")));
            Assert.False(Directory.Exists(Path.Combine(root, "Assets")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>验证 Godot 改写过的相同 TableKit 草稿不会再次替换 project.godot。</summary>
    [Fact]
    public void Save_草稿未变_不改写project字节()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-unchanged-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string projectPath = Path.Combine(root, "project.godot");
            File.WriteAllText(projectPath, "config_version=5\n");
            TableKitSettingsService service = new();
            TableKitOptions options = new()
            {
                ProjectRoot = root,
                LubanConfigPath = "Luban/MiniTemplate/luban.conf",
                OutputDataDir = "Assets/Resources/Art/Table/",
                UseRawResourceLoading = true
            };
            service.Save(root, options);
            // 只模拟 Godot 重写 JSON 内部引号，保留 project.godot 字符串自己的外层引号。
            string escaped = ToolingFileObservation.ReadUtf8(projectPath).Replace("\\\"", "\\u0022", StringComparison.Ordinal);
            File.WriteAllText(projectPath, escaped);
            DateTime timestamp = new(2002, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(projectPath, timestamp);

            service.Save(root, service.Load(root, options));

            ToolingFileObservation.AssertUtf8(projectPath, escaped);
            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(projectPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>断言 Unity Runtime Settings 只保留现行 TableKit 字段和其它 Kit。</summary>
    /// <param name="runtimePath">runtime-settings.json 路径。</param>
    private static void AssertUnityRuntimeProjection(string runtimePath)
    {
        using JsonDocument runtimeJson = ToolingFileObservation.OpenJson(runtimePath);
        JsonElement[] settings = runtimeJson.RootElement.GetProperty("settings").EnumerateArray().ToArray();
        Assert.Contains(settings, static entry =>
            entry.GetProperty("kit").GetString() == "TableKit"
            && entry.GetProperty("key").GetString() == "runtimePathPattern"
            && entry.GetProperty("value").GetString() == "Art/Config/{0}");
        Assert.Contains(settings, static entry =>
            entry.GetProperty("kit").GetString() == "TableKit"
            && entry.GetProperty("key").GetString() == "useRawResourceLoading"
            && entry.GetProperty("value").GetString() == "false");
        Assert.Contains(settings, static entry =>
            entry.GetProperty("kit").GetString() == "OtherKit"
            && entry.GetProperty("key").GetString() == "enabled"
            && entry.GetProperty("value").GetString() == "true");
        string[] keys = settings.Select(static entry => entry.GetProperty("key").GetString() ?? string.Empty).ToArray();
        Assert.DoesNotContain("resourceRoot", keys);
        Assert.DoesNotContain("dataExtension", keys);
        Assert.DoesNotContain("useAsyncLoading", keys);
        Assert.DoesNotContain("LubanConfigPath", keys);
        Assert.All(settings, static entry => Assert.DoesNotContain(
            "EditorOnly",
            (entry.GetProperty("value").GetString() ?? string.Empty).Split('/')));
    }
}
