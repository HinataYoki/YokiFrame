using YokiFrame.Tooling.Application.Models.UIKit;
using YokiFrame.Tooling.Application.Services.UIKit;

namespace YokiFrame.Tooling.Application.Tests.UIKit;

/// <summary>覆盖 UIKit Editor Tools 项目配置的默认值、受控写入和回读。</summary>
public sealed class UIKitEditorSettingsServiceTests
{
    /// <summary>缺少持久配置时返回空结果，让 Unity Provider 继续提供默认值。</summary>
    [Fact]
    public void MissingSettingsReturnNoOverride()
    {
        string root = CreateRoot();
        try
        {
            UIKitEditorSettingsService service = new(root);

            Assert.Null(service.Load());
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    /// <summary>保存后由新服务实例回读五个字段，并保留其它 owner 与 UIKit 未知键。</summary>
    [Fact]
    public async Task SavePersistsEditorToolsSettingsWithoutReplacingUnownedValues()
    {
        string root = CreateRoot();
        try
        {
            UIKitEditorSettingsService service = new(root);
            string path = service.SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                "{\"formatVersion\":1,\"settings\":["
                + "{\"kit\":\"LogKit\",\"key\":\"enabled\",\"value\":\"true\"},"
                + "{\"kit\":\"UIKit\",\"key\":\"editor.futureKey\",\"value\":\"keep\"}]}");
            WorkbenchUIKitPanelGenerationRequest settings = new()
            {
                PrefabFolder = "Assets/Game/UI/Prefabs",
                ScriptFolder = "Assets/Game/UI/Scripts",
                ScriptNamespace = "Game.UI",
                AssemblyName = "Game.UI",
                CodeTemplate = "TeamTemplate-1",
            };

            await service.SaveAsync(settings, CancellationToken.None);

            WorkbenchUIKitPanelGenerationRequest loaded = Assert.IsType<WorkbenchUIKitPanelGenerationRequest>(
                new UIKitEditorSettingsService(root).Load());
            Assert.Equal(settings.PrefabFolder, loaded.PrefabFolder);
            Assert.Equal(settings.ScriptFolder, loaded.ScriptFolder);
            Assert.Equal(settings.ScriptNamespace, loaded.ScriptNamespace);
            Assert.Equal(settings.AssemblyName, loaded.AssemblyName);
            Assert.Equal(settings.CodeTemplate, loaded.CodeTemplate);
            string saved = File.ReadAllText(path);
            Assert.Contains("\"kit\": \"LogKit\"", saved, StringComparison.Ordinal);
            Assert.Contains("\"key\": \"editor.futureKey\"", saved, StringComparison.Ordinal);
            Assert.Contains("\"value\": \"keep\"", saved, StringComparison.Ordinal);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    /// <summary>验证模板名必须是安全 ID，路径分隔符不会进入项目配置。</summary>
    [Theory]
    [InlineData("bad/name")]
    [InlineData("../escape")]
    [InlineData("模板")]
    public async Task UnsafeTemplateNamesAreRejected(string templateName)
    {
        string root = CreateRoot();
        try
        {
            UIKitEditorSettingsService service = new(root);
            WorkbenchUIKitPanelGenerationRequest settings = new()
            {
                CodeTemplate = templateName,
            };

            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(
                settings,
                CancellationToken.None));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    /// <summary>创建隔离的 Unity 项目根。</summary>
    private static string CreateRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-uikit-editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>清理隔离目录，避免失败测试污染后续运行。</summary>
    private static void DeleteRoot(string root)
    {
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        catch
        {
            // 临时目录清理失败不覆盖测试的业务断言。
        }
    }
}
