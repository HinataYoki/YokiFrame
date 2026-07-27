using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests;

/// <summary>
/// 锁定 Godot 本地安装使用的受控包投影规则。
/// </summary>
public sealed class GodotPackageProjectionTests
{
    /// <summary>
    /// 验证 Godot 投影只保留可交付源码，并排除工具源码、测试、包内 Runtime、缓存与废弃 Kit。
    /// </summary>
    [Fact]
    public void BuildKeepsDeliverableFilesAndFiltersExcludedContent()
    {
        var sourceRoot = CreateSourcePackageRoot();

        var projection = new GodotPackageProjectionBuilder().Build(sourceRoot, "win-x64");
        var actualPaths = projection.Files
            .Select(static file => GodotPackageProjectionTests.NormalizeRelativePath(file.RelativePath))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expectedPaths = new[]
        {
            "Core/Adapters/Godot/Editor/YokiFrame.Godot.Editor.csproj",
            "Core/Adapters/Godot/Runtime/Directory.Build.props",
            "Core/Adapters/Godot/Runtime/GodotEngineLogger.cs",
            "Core/Adapters/Godot/Runtime/YokiFrame.Godot.Runtime.csproj",
            "Core/Editor/YokiFrame.Editor.csproj",
            "Core/Runtime/YokiFrame.cs",
            "Core/Runtime/YokiFrame.csproj",
            "Documentation~/Api/00-GettingStarted/FrameworkOverview.md",
            "Documentation~/Guides/AI-Install.md",
            "Tools/ActionKit/Editor/YokiFrame.ActionKit.Editor.csproj",
            "Tools/ActionKit/Runtime/ActionKit.cs",
            "Tools/ActionKit/Runtime/YokiFrame.ActionKit.csproj",
            "Tools/AudioKit/Adapters/Godot/Runtime/YokiFrame.AudioKit.Godot.csproj",
            "Tools/AudioKit/Editor/YokiFrame.AudioKit.Editor.csproj",
            "Tools/AudioKit/Runtime/AudioKit.cs",
            "Tools/AudioKit/Runtime/YokiFrame.AudioKit.csproj",
            "Tools/SaveKit/Adapters/Godot/Runtime/YokiFrame.SaveKit.Godot.csproj",
            "Tools/SaveKit/Editor/YokiFrame.SaveKit.Editor.csproj",
            "Tools/SaveKit/Runtime/SaveKit.cs",
            "Tools/SaveKit/Runtime/YokiFrame.SaveKit.csproj"
        };

        Assert.Equal(expectedPaths, actualPaths);
    }

    /// <summary>
    /// 创建同时包含应保留内容与全部已确认排除类型的源包 fixture。
    /// </summary>
    /// <returns>测试专用源包根目录。</returns>
    private static string CreateSourcePackageRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-godot-projection-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Documentation~"));
        WriteFixtureFiles(root, new[]
        {
            "Documentation~/Api/00-GettingStarted/FrameworkOverview.md",
            "Documentation~/Guides/AI-Install.md",
            "Core/Runtime/YokiFrame.cs",
            "Core/Runtime/YokiFrame.csproj",
            "Core/Editor/YokiFrame.Editor.csproj",
            "Core/Adapters/Godot/Editor/YokiFrame.Godot.Editor.csproj",
            "Core/Adapters/Godot/Runtime/Directory.Build.props",
            "Core/Adapters/Godot/Runtime/GodotEngineLogger.cs",
            "Core/Adapters/Godot/Runtime/YokiFrame.Godot.Runtime.csproj",
            "Tools/ActionKit/Editor/YokiFrame.ActionKit.Editor.csproj",
            "Tools/ActionKit/Runtime/ActionKit.cs",
            "Tools/ActionKit/Runtime/YokiFrame.ActionKit.csproj",
            "Tools/AudioKit/Adapters/Godot/Runtime/YokiFrame.AudioKit.Godot.csproj",
            "Tools/AudioKit/Editor/YokiFrame.AudioKit.Editor.csproj",
            "Tools/AudioKit/Runtime/AudioKit.cs",
            "Tools/AudioKit/Runtime/YokiFrame.AudioKit.csproj",
            "Tools/SaveKit/Adapters/Godot/Runtime/YokiFrame.SaveKit.Godot.csproj",
            "Tools/SaveKit/Editor/YokiFrame.SaveKit.Editor.csproj",
            "Tools/SaveKit/Runtime/SaveKit.cs",
            "Tools/SaveKit/Runtime/YokiFrame.SaveKit.csproj",
            "WorkbenchRuntime~/win-x64/YokiFrame.Workbench.Avalonia.exe"
        });
        WriteFixtureFiles(root, new[]
        {
            "Documentation~/Architecture_Guardrails.md",
            "Documentation~/README.md",
            "YokiFrameWorkbench~/src/InstallerTool.cs",
            "Core/Tests/CoreTests.cs",
            "Tools/ActionKit/Tests/ActionKitTests.cs",
            ".git/config",
            "Core/Runtime/YokiFrame.cs.meta",
            "Core/Runtime/YokiFrame.cs.uid",
            "Core/Runtime/bin/Release/YokiFrame.dll",
            "Core/Runtime/obj/project.assets.json",
            "Core/Runtime/.artifacts-validation/Release/YokiFrame.dll",
            "WorkbenchRuntime~/.artifacts/publish.tmp",
            "WorkbenchRuntime~/linux-x64/yoki",
            "WorkbenchRuntime~/osx-arm64/YokiFrame.Workbench.Avalonia",
            "WorkbenchRuntime~/win-x64-aot/YokiFrame.Workbench.Avalonia.exe",
            "Tools/BuffKit/Runtime/BuffKit.cs",
            "Tools/InputKit/Runtime/InputKit.cs",
            "Tools/AudioKit/Adapters/Unity/Runtime/UnityAudioKitBackend.cs",
            "Tools/UIKit/Adapters/Unity/Runtime/UIKit.cs",
            "Tools/UIKit/Adapters/Unity/Editor/UIKitInteractionProvider.cs",
            "Tools/UIKit/Integrations/Unity/DOTween/Runtime/DOTweenAnimations.cs"
        });
        return root;
    }

    /// <summary>
    /// 在源包 fixture 中创建指定相对路径的文件，并自动补齐父目录。
    /// </summary>
    /// <param name="root">fixture 根目录。</param>
    /// <param name="relativePaths">需要创建的相对文件路径。</param>
    private static void WriteFixtureFiles(string root, IEnumerable<string> relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, relativePath);
        }
    }

    /// <summary>
    /// 把平台相关目录分隔符统一为测试断言使用的正斜杠。
    /// </summary>
    /// <param name="relativePath">投影返回的相对路径。</param>
    /// <returns>使用正斜杠的相对路径。</returns>
    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }
}
