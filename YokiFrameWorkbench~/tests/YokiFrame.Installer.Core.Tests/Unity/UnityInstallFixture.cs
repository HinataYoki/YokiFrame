using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokiFrame.Installer.Core.Tests.Unity;

/// <summary>
/// 提供完全位于系统临时目录的 Unity 2022.3 项目、源包和 manifest fixture。
/// </summary>
internal sealed class UnityInstallFixture : IDisposable
{
    private const string PACKAGE_ID = "com.hinatayoki.yokiframe";

    /// <summary>
    /// 创建带真实 Unity 最小结构、结构化 manifest 和可筛选源包内容的测试目录。
    /// </summary>
    /// <param name="editorVersion">写入 ProjectVersion.txt 的 Unity 版本。</param>
    private UnityInstallFixture(string editorVersion)
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-unity-install-tests",
            Guid.NewGuid().ToString("N"));
        SourcePackageRoot = Path.Combine(Root, "source", "YokiFrame");
        ProjectRoot = Path.Combine(Root, "project");
        PackagesRoot = Path.Combine(ProjectRoot, "Packages");
        EmbeddedPackageRoot = Path.Combine(PackagesRoot, PACKAGE_ID);
        ManifestPath = Path.Combine(PackagesRoot, "manifest.json");

        Directory.CreateDirectory(Path.Combine(ProjectRoot, "Assets"));
        Directory.CreateDirectory(PackagesRoot);
        Directory.CreateDirectory(Path.Combine(ProjectRoot, "ProjectSettings"));
        File.WriteAllText(
            Path.Combine(ProjectRoot, "ProjectSettings", "ProjectVersion.txt"),
            "m_EditorVersion: " + editorVersion + Environment.NewLine);
        WriteManifest(CreateDefaultManifest());
        CreateSourcePackage();
    }

    /// <summary>
    /// 获取 fixture 总根目录。
    /// </summary>
    internal string Root { get; }

    /// <summary>
    /// 获取模拟 YokiFrame 源包根目录。
    /// </summary>
    internal string SourcePackageRoot { get; }

    /// <summary>
    /// 获取模拟 Unity 项目根目录。
    /// </summary>
    internal string ProjectRoot { get; }

    /// <summary>
    /// 获取 Unity Packages 目录。
    /// </summary>
    internal string PackagesRoot { get; }

    /// <summary>
    /// 获取 YokiFrame embedded 包目标目录。
    /// </summary>
    internal string EmbeddedPackageRoot { get; }

    /// <summary>
    /// 获取 Unity Packages/manifest.json 路径。
    /// </summary>
    internal string ManifestPath { get; }

    /// <summary>
    /// 创建使用指定 Unity 版本的隔离 fixture。
    /// </summary>
    /// <param name="editorVersion">Unity Editor 版本，默认使用最低支持版本。</param>
    /// <returns>已创建的 fixture。</returns>
    internal static UnityInstallFixture Create(string editorVersion = "2022.3.0f1")
    {
        return new UnityInstallFixture(editorVersion);
    }

    /// <summary>
    /// 用结构化 JSON 覆盖 Unity manifest，保持测试输入合法且可精确控制。
    /// </summary>
    /// <param name="manifest">待写入的 manifest 根对象。</param>
    internal void WriteManifest(JsonObject manifest)
    {
        File.WriteAllText(ManifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }

    /// <summary>
    /// 直接写入 manifest 原文，用于验证无效 JSON 的零写入失败路径。
    /// </summary>
    /// <param name="content">待写入的原始文本。</param>
    internal void WriteManifestText(string content)
    {
        File.WriteAllText(ManifestPath, content);
    }

    /// <summary>
    /// 在 manifest dependencies 中结构化设置 YokiFrame Git 依赖。
    /// </summary>
    /// <param name="gitUrl">待设置的 Git URL。</param>
    internal void SetYokiFrameGitDependency(string gitUrl)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(ManifestPath))?.AsObject()
            ?? throw new InvalidDataException("Fixture manifest is empty.");
        var dependencies = manifest["dependencies"]?.AsObject()
            ?? throw new InvalidDataException("Fixture manifest dependencies are missing.");
        dependencies[PACKAGE_ID] = gitUrl;
        WriteManifest(manifest);
    }

    /// <summary>
    /// 读取并返回 manifest 根对象，供依赖保留和其它根属性断言复用。
    /// </summary>
    /// <returns>已解析的 manifest 根对象。</returns>
    internal JsonObject ReadManifest()
    {
        return JsonNode.Parse(File.ReadAllText(ManifestPath))?.AsObject()
            ?? throw new InvalidDataException("Fixture manifest is empty.");
    }

    /// <summary>
    /// 获取 embedded 包内目标文件的完整路径。
    /// </summary>
    /// <param name="relativePath">使用正斜杠的包相对路径。</param>
    /// <returns>当前平台完整路径。</returns>
    internal string GetEmbeddedPath(string relativePath)
    {
        return Path.Combine(EmbeddedPackageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// 修改 embedded 包内文件，用于构造受管文件冲突。
    /// </summary>
    /// <param name="relativePath">使用正斜杠的包相对路径。</param>
    /// <param name="content">待写入内容。</param>
    internal void WriteEmbeddedFile(string relativePath, string content)
    {
        var path = GetEmbeddedPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// 验证 manifest 原子写入没有遗留同目录临时文件。
    /// </summary>
    internal void AssertNoManifestTemporaryFiles()
    {
        var matches = Directory.EnumerateFiles(PackagesRoot, "manifest.json*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "manifest.json" }, matches);
    }

    /// <summary>
    /// 删除 fixture 产生的临时目录，避免安装投影长期占用磁盘。
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary>
    /// 创建包含其它 Unity 依赖和非 dependencies 根属性的默认 manifest。
    /// </summary>
    /// <returns>默认 manifest 根对象。</returns>
    private static JsonObject CreateDefaultManifest()
    {
        return new JsonObject
        {
            ["dependencies"] = new JsonObject
            {
                ["com.unity.textmeshpro"] = "3.0.6",
                ["com.unity.modules.jsonserialize"] = "1.0.0"
            },
            ["enableLockFile"] = true,
            ["resolutionStrategy"] = "highestMinor"
        };
    }

    /// <summary>
    /// 创建同时包含应交付文件、Unity meta、排除目录、废弃 Kit 和多平台 Runtime 的源包。
    /// </summary>
    private void CreateSourcePackage()
    {
        WriteSourceFiles(new[]
        {
            "package.json",
            "Documentation~/Api/00-GettingStarted/FrameworkOverview.md",
            "Documentation~/Guides/AI-Install.md",
            "Core/Runtime/Alpha.cs",
            "Core/Runtime/Alpha.cs.meta",
            "Core/Runtime/YokiFrame.Runtime.asmdef",
            "Core/Runtime/YokiFrame.Runtime.asmdef.meta",
            "Tools/ActionKit/Runtime/ActionKit.cs",
            "WorkbenchRuntime~/build-current-platform.cmd",
            "WorkbenchRuntime~/win-x64/YokiFrame.Workbench.Avalonia.exe"
        });
        WriteSourceFiles(new[]
        {
            ".yokiframe-owner.json",
            ".git/config",
            "Documentation~/Architecture_Guardrails.md",
            "Documentation~/README.md",
            "Documentation~/Api/00-GettingStarted/FrameworkOverview.md.meta",
            "Core/Tests/AlphaTests.cs",
            "Core/Tests.meta",
            "Core/Runtime/bin/Release/YokiFrame.dll",
            "Core/Runtime/obj/project.assets.json",
            "Tools/BuffKit/Runtime/BuffKit.cs",
            "Tools/InputKit/Runtime/InputKit.cs",
            "WorkbenchRuntime~/linux-x64/yoki",
            "YokiFrameWorkbench~/src/YokiFrame.Installer.Core/Installer.cs",
            "YokiFrameWorkbench~/.artifacts-installer-ui/Installer.dll",
            "YokiFrameWorkbench~/src/YokiFrame.Installer.Core/.artifacts-validation/Installer.dll"
        });
    }

    /// <summary>
    /// 在源包中写入一组相对文件，并自动创建父目录。
    /// </summary>
    /// <param name="relativePaths">使用正斜杠的源包相对路径。</param>
    private void WriteSourceFiles(IEnumerable<string> relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            var path = Path.Combine(SourcePackageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, relativePath);
        }
    }
}
