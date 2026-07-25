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
    public void UnitySaveWritesRuntimeSettingsAndExcludesEditorFields()
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

            string runtimeJson = File.ReadAllText(runtimePath);
            Assert.Contains("\"runtimePathPattern\"", runtimeJson, StringComparison.Ordinal);
            Assert.Contains("\"Art/Config/{0}\"", runtimeJson, StringComparison.Ordinal);
            Assert.Contains("\"useRawResourceLoading\"", runtimeJson, StringComparison.Ordinal);
            Assert.Contains("\"OtherKit\"", runtimeJson, StringComparison.Ordinal);
            Assert.DoesNotContain("resourceRoot", runtimeJson, StringComparison.Ordinal);
            Assert.DoesNotContain("dataExtension", runtimeJson, StringComparison.Ordinal);
            Assert.DoesNotContain("useAsyncLoading", runtimeJson, StringComparison.Ordinal);
            Assert.DoesNotContain("EditorOnly", runtimeJson, StringComparison.Ordinal);
            Assert.DoesNotContain("LubanConfigPath", runtimeJson, StringComparison.Ordinal);
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
    public void GodotSaveWritesRuntimeSettingsSection()
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

            string projectSettings = File.ReadAllText(projectPath);
            Assert.Contains("[application]", projectSettings, StringComparison.Ordinal);
            Assert.Contains("[yokiframe/runtime]", projectSettings, StringComparison.Ordinal);
            Assert.Contains("table_kit/runtime_path_pattern=\"res://Data/Tables/{0}\"", projectSettings, StringComparison.Ordinal);
            Assert.Contains("table_kit/use_raw_resource_loading=\"true\"", projectSettings, StringComparison.Ordinal);
            Assert.DoesNotContain("table_kit/use_async_loading", projectSettings, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
