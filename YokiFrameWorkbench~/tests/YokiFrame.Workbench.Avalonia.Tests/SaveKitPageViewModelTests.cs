using YokiFrame.Tooling.Application.Services.SaveKit;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 SaveKit 页面快捷打开目录的行为。</summary>
public sealed class SaveKitPageViewModelTests
{
    /// <summary>可解析目录尚不存在时，快捷入口会创建目录并把同一路径交给宿主。</summary>
    [Fact]
    public async Task OpenDirectoryCreatesAndOpensConfiguredPath()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-savekit-page-" + Guid.NewGuid().ToString("N"));
        string? openedPath = null;
        SaveKitPageViewModel? viewModel = null;
        try
        {
            Directory.CreateDirectory(root);
            SaveKitWorkbenchSettingsService service = new(root);
            viewModel = new SaveKitPageViewModel(
                service,
                null,
                path =>
                {
                    openedPath = path;
                    return Task.CompletedTask;
                });

            viewModel.SetEngine("unity-editor");
            for (int attempt = 0; attempt < 20 && !viewModel.IsSupported; attempt++)
            {
                await Task.Delay(25);
            }
            viewModel.StoragePath = "ShortcutSaves";

            Assert.True(viewModel.OpenDirectoryCommand.CanExecute(null));
            await viewModel.OpenDirectoryCommand.ExecuteAsync();

            Assert.NotNull(openedPath);
            Assert.True(Directory.Exists(openedPath));
            Assert.Equal(Path.GetFullPath(Path.Combine(root, "ShortcutSaves")), openedPath);
        }
        finally
        {
            viewModel?.Dispose();
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Console.Error.WriteLine("无法清理 SaveKit 页面测试目录: " + exception.Message);
            }
        }
    }

    /// <summary>刷新时用宿主返回的用户目录扫描 slots，而不是停在运行时变量。</summary>
    [Fact]
    public async Task RefreshScansFilesUnderResolvedRuntimeRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-savekit-scan-" + Guid.NewGuid().ToString("N"));
        string runtimeRoot = Path.Combine(Path.GetTempPath(), "yokiframe-savekit-scan-runtime-" + Guid.NewGuid().ToString("N"));
        SaveKitPageViewModel? viewModel = null;
        try
        {
            Directory.CreateDirectory(root);
            string saveRoot = Path.Combine(runtimeRoot, "YokiFrame", "Saves", "slots");
            Directory.CreateDirectory(saveRoot);
            File.WriteAllBytes(Path.Combine(saveRoot, "save_0.yoki"), new byte[3]);
            viewModel = new SaveKitPageViewModel(
                new SaveKitWorkbenchSettingsService(root),
                null,
                _ => Task.CompletedTask,
                (_, _) => Task.FromResult<string?>(runtimeRoot));
            viewModel.SetEngine("unity-editor");
            for (int attempt = 0; attempt < 40 && viewModel.SlotCount == 0; attempt++)
            {
                await Task.Delay(25);
            }

            Assert.Equal(1, viewModel.SlotCount);
            Assert.Equal("0", viewModel.Files[0].Name);
            Assert.Equal(Path.Combine(runtimeRoot, "YokiFrame", "Saves"), viewModel.ResolvedStoragePath);
        }
        finally
        {
            viewModel?.Dispose();
            DeleteDirectory(root);
            DeleteDirectory(runtimeRoot);
        }
    }

    /// <summary>Runtime 用户目录未在 Workbench 本地解析时，使用宿主环境返回的真实根目录。</summary>
    [Fact]
    public async Task OpenDirectoryUsesResolvedRuntimeRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-savekit-runtime-" + Guid.NewGuid().ToString("N"));
        string? openedPath = null;
        SaveKitPageViewModel? viewModel = null;
        try
        {
            Directory.CreateDirectory(root);
            viewModel = new SaveKitPageViewModel(
                new SaveKitWorkbenchSettingsService(root),
                null,
                path =>
                {
                    openedPath = path;
                    return Task.CompletedTask;
                },
                (_, _) => Task.FromResult<string?>(root));
            viewModel.SetEngine("unity-editor");
            for (int attempt = 0; attempt < 20 && !viewModel.IsSupported; attempt++)
            {
                await Task.Delay(25);
            }

            viewModel.StoragePath = "${persistentDataPath}/YokiFrame/Saves";
            await viewModel.OpenDirectoryCommand.ExecuteAsync();

            Assert.Equal(Path.Combine(root, "YokiFrame", "Saves"), openedPath);
            Assert.True(Directory.Exists(openedPath));
        }
        finally
        {
            viewModel?.Dispose();
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Console.Error.WriteLine("无法清理 SaveKit Runtime 测试目录: " + exception.Message);
            }
        }
    }

    /// <summary>删除测试目录，并记录不会影响断言结果的文件系统清理失败。</summary>
    private static void DeleteDirectory(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Console.Error.WriteLine("无法清理 SaveKit 页面测试目录: " + exception.Message);
        }
    }
}
