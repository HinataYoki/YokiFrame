using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.TableKit;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 TableKit Workbench 草稿的持久化和恢复行为。</summary>
public sealed class TableKitPersistenceTests
{
    /// <summary>验证关闭自定义路径后恢复草稿时忽略旧编辑器数据值并重新按数据输出推断。</summary>
    [Fact]
    public void LoadIgnoresStaleEditorDataPathWhenCustomizationIsDisabled()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-inferred-editor-data-" + Guid.NewGuid().ToString("N"));
        try
        {
            TableKitSettingsService settingsService = new();
            settingsService.Save(root, new TableKitOptions
            {
                ProjectRoot = root,
                LubanConfigPath = "Luban/MiniTemplate/luban.conf",
                OutputDataDir = "Assets/Generated/Tables/Data",
                CustomEditorDataPath = false,
                EditorDataPath = "Assets/Resources/Art/Table"
            });

            TableKitPageViewModel restored = new(root, new TableKitApplicationService());

            Assert.Equal("Assets/Generated/Tables/Data", restored.EditorDataPath);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>显式保存后重新创建页面能够恢复全部 TableKit 草稿字段。</summary>
    [Fact]
    public void SaveCommandRestoresCompleteConfigurationInNewViewModel()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-persistence-" + Guid.NewGuid().ToString("N"));
        try
        {
            TableKitPageViewModel source = new(root, new TableKitApplicationService())
            {
                ConfigPath = "Luban/Custom/luban.conf",
                LubanWorkDir = "Luban/Custom",
                LubanExecutablePath = "Tools/Luban/Luban.dll",
                TargetName = "game-client",
                CodeTarget = "cs-dotnet-json",
                DataTarget = "json",
                OutputDataDir = "Assets/Generated/Tables/Data",
                OutputCodeDir = "Assets/Generated/Tables/Code",
                IsAddressable = true,
                RuntimePathPattern = "external://Tables/{0}",
                CustomEditorDataPath = true,
                EditorDataPath = "Assets/Generated/Tables/Editor",
                UseAsyncLoading = true,
                UseRawResourceLoading = false,
                GenerateExternalTypeUtil = true,
                UseAssemblyDefinition = true,
                AssemblyName = "Game.TableKit"
            };
            source.AddExtraOutputCommand.Execute(null);
            TableKitExtraOutputViewModel sourceExtra = Assert.Single(source.ExtraOutputTargets);
            sourceExtra.TargetName = "server-release";
            sourceExtra.CodeTarget = "java-bin";
            sourceExtra.DataTarget = "bin";
            sourceExtra.OutputDataDir = "Build/Tables/Data";
            sourceExtra.OutputCodeDir = "Build/Tables/Code";
            source.SaveCommand.Execute(null);
            TableKitPageViewModel restored = new(root, new TableKitApplicationService());
            Assert.Equal("Luban/Custom/luban.conf", restored.ConfigPath);
            Assert.Equal("Luban/Custom", restored.LubanWorkDir);
            Assert.Equal("Tools/Luban/Luban.dll", restored.LubanExecutablePath);
            Assert.Equal("game-client", restored.TargetName);
            Assert.Equal("cs-dotnet-json", restored.CodeTarget);
            Assert.Equal("json", restored.DataTarget);
            Assert.Equal("Assets/Generated/Tables/Data", restored.OutputDataDir);
            Assert.Equal("Assets/Generated/Tables/Code", restored.OutputCodeDir);
            Assert.True(restored.IsAddressable);
            Assert.Equal("external://Tables/{0}", restored.RuntimePathPattern);
            Assert.True(restored.CustomEditorDataPath);
            Assert.Equal("Assets/Generated/Tables/Editor", restored.EditorDataPath);
            Assert.True(restored.UseAsyncLoading);
            Assert.False(restored.UseRawResourceLoading);
            Assert.True(restored.GenerateExternalTypeUtil);
            Assert.True(restored.UseAssemblyDefinition);
            Assert.Equal("Game.TableKit", restored.AssemblyName);
            TableKitExtraOutputViewModel restoredExtra = Assert.Single(restored.ExtraOutputTargets);
            Assert.Equal("server-release", restoredExtra.TargetName);
            Assert.Equal("java-bin", restoredExtra.CodeTarget);
            Assert.Equal("bin", restoredExtra.DataTarget);
            Assert.Equal("Build/Tables/Data", restoredExtra.OutputDataDir);
            Assert.Equal("Build/Tables/Code", restoredExtra.OutputCodeDir);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
