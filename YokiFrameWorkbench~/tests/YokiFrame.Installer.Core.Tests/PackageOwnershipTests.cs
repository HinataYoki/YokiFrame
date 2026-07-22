using System.Text.Json;
using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests;

/// <summary>
/// 锁定 Installer 对受管包 owner manifest 与用户修改冲突的判定契约。
/// </summary>
public sealed class PackageOwnershipTests
{
    /// <summary>
    /// 验证目标包目录不存在时明确返回未安装状态，避免把空目标误判为 legacy 或冲突。
    /// </summary>
    [Fact]
    public void InspectReturnsNotInstalledWhenPackageRootDoesNotExist()
    {
        var packageRoot = Path.Combine(CreateTempRoot("not-installed"), "YokiFrame");

        var inspection = new PackageOwnershipInspector().Inspect(packageRoot);

        Assert.Equal(PackageOwnershipState.NotInstalled, inspection.State);
        Assert.Empty(inspection.ConflictPaths);
    }

    /// <summary>
    /// 验证已有包但没有 owner manifest 时标记为未受管 legacy，供后续接管流程单独处理。
    /// </summary>
    [Fact]
    public void InspectReturnsUnmanagedLegacyWhenManifestIsMissing()
    {
        var packageRoot = CreateTempRoot("unmanaged-legacy");
        WriteFile(packageRoot, "Core/Runtime/YokiFrame.cs", "legacy");

        var inspection = new PackageOwnershipInspector().Inspect(packageRoot);

        Assert.Equal(PackageOwnershipState.UnmanagedLegacy, inspection.State);
        Assert.Empty(inspection.ConflictPaths);
    }

    /// <summary>
    /// 验证目标文件与 owner manifest 的长度和 SHA-256 全部匹配时返回干净状态。
    /// </summary>
    [Fact]
    public void InspectReturnsCleanWhenManagedFilesMatchManifest()
    {
        var projection = CreateProjection();
        var packageRoot = CreateTempRoot("clean");
        CopyProjectionFiles(projection, packageRoot);
        WriteOwnerManifest(projection, packageRoot);

        var inspection = new PackageOwnershipInspector().Inspect(packageRoot);

        Assert.Equal(PackageOwnershipState.Clean, inspection.State);
        Assert.Empty(inspection.ConflictPaths);
    }

    /// <summary>
    /// 验证旧版 Workbench 误写入 Runtime 目录的 WebView2 缓存可安全升级，其他额外文件仍必须保留冲突保护。
    /// </summary>
    [Fact]
    public void InspectIgnoresOnlyLegacyWorkbenchWebView2Cache()
    {
        var projection = CreateProjection();
        var packageRoot = CreateTempRoot("legacy-webview2-cache");
        CopyProjectionFiles(projection, packageRoot);
        WriteOwnerManifest(projection, packageRoot);
        WriteFile(
            packageRoot,
            "WorkbenchRuntime~/win-x64/YokiFrame.Workbench.Avalonia.exe.WebView2/EBWebView/Default/Preferences",
            "cache");
        WriteFile(packageRoot, "WorkbenchRuntime~/win-x64/Other.exe.WebView2/Preferences", "manual");

        var inspection = new PackageOwnershipInspector().Inspect(packageRoot);

        Assert.Equal(PackageOwnershipState.Modified, inspection.State);
        Assert.Equal(
            new[] { "WorkbenchRuntime~/win-x64/Other.exe.WebView2/Preferences" },
            inspection.ConflictPaths);
    }

    /// <summary>
    /// 验证 Ctrl+E bootstrap 写入的 Workbench `.artifacts*` 缓存不会阻止受管包更新，
    /// 同一 Workbench 目录及其他路径的手工文件仍必须报告冲突。
    /// </summary>
    [Fact]
    public void InspectIgnoresOnlyWorkbenchGeneratedArtifactDirectories()
    {
        var projection = CreateProjection();
        var packageRoot = CreateTempRoot("workbench-build-artifacts");
        CopyProjectionFiles(projection, packageRoot);
        WriteOwnerManifest(projection, packageRoot);
        WriteFile(packageRoot, "YokiFrameWorkbench~/.artifacts/bin/YokiFrame.Cli/yoki.exe", "generated");
        WriteFile(packageRoot, "YokiFrameWorkbench~/.artifacts-installer-ui/obj/cache.bin", "generated");
        WriteFile(packageRoot, "YokiFrameWorkbench~/Manual.txt", "manual");
        WriteFile(packageRoot, "Unexpected/.artifacts/Manual.txt", "manual");

        var inspection = new PackageOwnershipInspector().Inspect(packageRoot);

        Assert.Equal(PackageOwnershipState.Modified, inspection.State);
        Assert.Equal(
            new[]
            {
                "Unexpected/.artifacts/Manual.txt",
                "YokiFrameWorkbench~/Manual.txt"
            },
            inspection.ConflictPaths);
    }

    /// <summary>
    /// 验证缺失、同长度内容篡改和额外文件都会标记为修改，并返回稳定排序的相对冲突路径。
    /// </summary>
    [Fact]
    public void InspectReturnsModifiedWithStablePathsForMissingChangedAndExtraFiles()
    {
        var projection = CreateProjection();
        var packageRoot = CreateTempRoot("modified");
        CopyProjectionFiles(projection, packageRoot);
        WriteOwnerManifest(projection, packageRoot);
        DeleteFile(packageRoot, "Core/Runtime/YokiFrame.cs");
        WriteFile(packageRoot, "Tools/ActionKit/Runtime/ActionKit.cs", "tampered");
        WriteFile(packageRoot, "Unexpected/Manual.txt", "manual");

        var inspector = new PackageOwnershipInspector();
        var firstInspection = inspector.Inspect(packageRoot);
        var secondInspection = inspector.Inspect(packageRoot);
        var expectedPaths = new[]
        {
            "Core/Runtime/YokiFrame.cs",
            "Tools/ActionKit/Runtime/ActionKit.cs",
            "Unexpected/Manual.txt"
        };

        Assert.Equal(PackageOwnershipState.Modified, firstInspection.State);
        Assert.Equal(expectedPaths, firstInspection.ConflictPaths);
        Assert.Equal(firstInspection.ConflictPaths, secondInspection.ConflictPaths);
    }

    /// <summary>
    /// 验证 owner manifest 只保存可搬运相对事实，读取后重写不会产生 JSON 漂移。
    /// </summary>
    [Fact]
    public void ManifestRoundTripIsPortableAndIdempotent()
    {
        var projection = CreateProjection();
        var packageRoot = CreateTempRoot("manifest-round-trip");
        PackageOwnerManifestStore store = new();
        var manifest = store.Create(projection);
        store.Write(packageRoot, manifest);
        var manifestPath = store.GetManifestPath(packageRoot);
        var firstJson = File.ReadAllText(manifestPath);

        AssertManifestContainsNoAbsolutePaths(firstJson);

        var loadedManifest = store.Read(packageRoot);
        store.Write(packageRoot, loadedManifest);
        var secondJson = File.ReadAllText(manifestPath);

        Assert.Equal(firstJson, secondJson);
    }

    /// <summary>
    /// 创建包含缺失、内容变化与额外文件测试基线的确定性 Godot 包投影。
    /// </summary>
    /// <returns>带稳定 SHA-256 的投影。</returns>
    private static PackageProjection CreateProjection()
    {
        var sourceRoot = CreateTempRoot("source");
        WriteFile(sourceRoot, "Core/Runtime/YokiFrame.cs", "original");
        WriteFile(sourceRoot, "Tools/ActionKit/Runtime/ActionKit.cs", "original");
        return new GodotPackageProjectionBuilder().Build(sourceRoot, "win-x64");
    }

    /// <summary>
    /// 按投影相对路径复制真实文件，构造 Installer 将要检查的目标包目录。
    /// </summary>
    /// <param name="projection">源包投影。</param>
    /// <param name="packageRoot">目标包根目录。</param>
    private static void CopyProjectionFiles(PackageProjection projection, string packageRoot)
    {
        foreach (var file in projection.Files)
        {
            var targetPath = GetFullPath(packageRoot, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file.SourcePath, targetPath);
        }
    }

    /// <summary>
    /// 从投影创建 owner manifest 并写入目标包，模拟一次已成功提交的受管安装。
    /// </summary>
    /// <param name="projection">已提交安装使用的文件投影。</param>
    /// <param name="packageRoot">目标包根目录。</param>
    private static void WriteOwnerManifest(PackageProjection projection, string packageRoot)
    {
        PackageOwnerManifestStore store = new();
        store.Write(packageRoot, store.Create(projection));
    }

    /// <summary>
    /// 解析 manifest JSON 的全部字符串值，确保没有开发机源路径或其它绝对路径泄漏。
    /// </summary>
    /// <param name="json">待检查的 manifest JSON。</param>
    private static void AssertManifestContainsNoAbsolutePaths(string json)
    {
        using var document = JsonDocument.Parse(json);
        List<string> stringValues = new();
        CollectStringValues(document.RootElement, stringValues);
        Assert.DoesNotContain(stringValues, static value => Path.IsPathFullyQualified(value));
    }

    /// <summary>
    /// 递归收集 JSON 字符串值，使可搬运性断言不依赖具体属性名或格式化方式。
    /// </summary>
    /// <param name="element">当前 JSON 节点。</param>
    /// <param name="values">字符串值收集目标。</param>
    private static void CollectStringValues(JsonElement element, ICollection<string> values)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            values.Add(element.GetString()!);
            return;
        }

        var children = element.ValueKind switch
        {
            JsonValueKind.Array => element.EnumerateArray(),
            JsonValueKind.Object => element.EnumerateObject().Select(static property => property.Value),
            _ => Enumerable.Empty<JsonElement>()
        };
        foreach (var child in children)
        {
            CollectStringValues(child, values);
        }
    }

    /// <summary>
    /// 在测试目录写入相对文件并自动创建父目录。
    /// </summary>
    /// <param name="root">测试根目录。</param>
    /// <param name="relativePath">使用正斜杠的相对路径。</param>
    /// <param name="content">文件内容。</param>
    private static void WriteFile(string root, string relativePath, string content)
    {
        var fullPath = GetFullPath(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    /// <summary>
    /// 删除指定 fixture 文件，用于模拟受管文件缺失。
    /// </summary>
    /// <param name="root">测试根目录。</param>
    /// <param name="relativePath">使用正斜杠的相对路径。</param>
    private static void DeleteFile(string root, string relativePath)
    {
        File.Delete(GetFullPath(root, relativePath));
    }

    /// <summary>
    /// 把清单相对路径转换为当前平台的完整测试路径。
    /// </summary>
    /// <param name="root">测试根目录。</param>
    /// <param name="relativePath">使用正斜杠的相对路径。</param>
    /// <returns>当前平台完整路径。</returns>
    private static string GetFullPath(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// 创建隔离的 Installer 测试目录，避免测试读写真实项目或 firstdemo。
    /// </summary>
    /// <param name="prefix">便于诊断的目录前缀。</param>
    /// <returns>新建的临时目录。</returns>
    private static string CreateTempRoot(string prefix)
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-owner-tests", prefix + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
