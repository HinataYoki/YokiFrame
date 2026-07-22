using System.Security.Cryptography;
using System.Text.Json;
using YokiFrame.Packaging.Models;
using YokiFrame.Packaging.Services;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖运行副本 manifest 生成与写入。
/// </summary>
public sealed class RuntimeManifestBuilderTests
{
    /// <summary>
    /// 验证 manifest 会记录平台入口、文件数量、总大小和文件哈希。
    /// </summary>
    [Fact]
    public void BuildRecordsPlatformFilesAndHashes()
    {
        var runtimeRoot = CreateRuntimeRoot();
        var exePath = Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe");
        var dllPath = Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Tooling.Application.dll");
        WriteFile(exePath, "exe");
        WriteFile(dllPath, "application");

        var manifest = new RuntimeManifestBuilder().Build(runtimeRoot, "Workbench", "win-x64", "YokiFrame.Workbench.Avalonia.exe");

        Assert.Equal(1, manifest.ManifestVersion);
        Assert.Equal("Workbench", manifest.Product);
        var platform = Assert.Single(manifest.Platforms);
        Assert.Equal("win-x64/YokiFrame.Workbench.Avalonia.exe", platform.Entrypoint);
        Assert.Equal(2, platform.FileCount);
        Assert.Equal(14, platform.TotalBytes);
        Assert.Contains(platform.Files, file => file.RelativePath == "win-x64/YokiFrame.Tooling.Application.dll" && file.Sha256 == Sha256(dllPath));
    }

    /// <summary>
    /// 验证运行时生成的 `.yokiframe` 状态不会进入发布 manifest 或 Git URL 载荷。
    /// </summary>
    [Fact]
    public void BuildExcludesRuntimeStateDirectory()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe"), "gui");
        WriteFile(
            Path.Combine(runtimeRoot, "win-x64", ".yokiframe", "workbench", "startup.jsonl"),
            "trace");

        var manifest = new RuntimeManifestBuilder().Build(
            runtimeRoot,
            "YokiFrameTool",
            "win-x64",
            "YokiFrame.Workbench.Avalonia.exe");

        var platform = Assert.Single(manifest.Platforms);
        Assert.Equal(1, platform.FileCount);
        Assert.DoesNotContain(platform.Files, file => file.RelativePath.Contains("/.yokiframe/", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证可分发 manifest 使用相对 runtime root，不泄露构建机器的绝对路径。
    /// </summary>
    [Fact]
    public void BuildUsesPortableRuntimeRoot()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe"), "gui");

        var manifest = new RuntimeManifestBuilder().Build(
            runtimeRoot,
            "YokiFrameTool",
            "win-x64",
            "YokiFrame.Workbench.Avalonia.exe");

        Assert.Equal(".", manifest.RuntimeRoot);
    }

    /// <summary>
    /// 验证共享运行时布局会同时记录 GUI 入口和轻量 CLI 入口，避免 CLI 被单独打成一整份运行时。
    /// </summary>
    [Fact]
    public void BuildRecordsSharedGuiAndCliEntries()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe"), "gui");
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "yoki.exe"), "cli");
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Tooling.Application.dll"), "application");

        var manifest = new RuntimeManifestBuilder().Build(
            runtimeRoot,
            "YokiFrameTool",
            "win-x64",
            "YokiFrame.Workbench.Avalonia.exe",
            "yoki.exe");

        Assert.Equal(2, manifest.LayoutVersion);
        var platform = Assert.Single(manifest.Platforms);
        Assert.Equal("win-x64", platform.RuntimeIdentifier);
        Assert.True(platform.SharedRuntime);
        Assert.Equal("win-x64/YokiFrame.Workbench.Avalonia.exe", platform.GuiEntry);
        Assert.Equal("win-x64/yoki.exe", platform.CliEntry);
        Assert.Equal(platform.GuiEntry, platform.Entrypoint);
        Assert.Contains(platform.Files, file => file.RelativePath == "win-x64/yoki.exe");
    }

    /// <summary>
    /// 验证 Native AOT 可同时交付独立 GUI 和 CLI；两者不共享运行时但 manifest 必须保留 CLI 入口。
    /// </summary>
    [Fact]
    public void BuildRecordsNativeAotGuiAndCliEntriesWithoutSharedRuntime()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64-aot", "YokiFrame.Workbench.Avalonia.exe"), "gui");
        WriteFile(Path.Combine(runtimeRoot, "win-x64-aot", "yoki.exe"), "cli");

        var manifest = new RuntimeManifestBuilder().Build(
            runtimeRoot,
            "YokiFrameTool",
            "win-x64-aot",
            "YokiFrame.Workbench.Avalonia.exe",
            "yoki.exe",
            sharedRuntime: false);

        Assert.Equal(2, manifest.LayoutVersion);
        var platform = Assert.Single(manifest.Platforms);
        Assert.False(platform.SharedRuntime);
        Assert.Equal("win-x64-aot/yoki.exe", platform.CliEntry);
        Assert.Contains(platform.Files, file => file.RelativePath == "win-x64-aot/yoki.exe");
    }

    /// <summary>
    /// 验证追加新平台 manifest 时会保留已有平台，并按平台名排序，避免后续发布 macOS 或 Linux 时覆盖 Windows 记录。
    /// </summary>
    [Fact]
    public void BuildWithExistingManifestKeepsOtherPlatforms()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe"), "gui-win");
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "yoki.exe"), "cli-win");
        WriteFile(Path.Combine(runtimeRoot, "osx-arm64", "YokiFrame.Workbench.Avalonia.app", "Contents", "MacOS", "YokiFrame.Workbench.Avalonia"), "gui-osx");
        WriteFile(Path.Combine(runtimeRoot, "osx-arm64", "yoki"), "cli-osx");

        var builder = new RuntimeManifestBuilder();
        var windowsManifest = builder.Build(
            runtimeRoot,
            "YokiFrameTool",
            "win-x64",
            "YokiFrame.Workbench.Avalonia.exe",
            "yoki.exe");

        var mergedManifest = builder.Build(
            runtimeRoot,
            "YokiFrameTool",
            windowsManifest,
            "osx-arm64",
            Path.Combine("YokiFrame.Workbench.Avalonia.app", "Contents", "MacOS", "YokiFrame.Workbench.Avalonia"),
            "yoki");

        Assert.Equal(2, mergedManifest.LayoutVersion);
        Assert.Equal(new[] { "osx-arm64", "win-x64" }, mergedManifest.Platforms.Select(platform => platform.Platform).ToArray());
        Assert.Contains(mergedManifest.Platforms, platform => platform.Platform == "win-x64" && platform.CliEntry == "win-x64/yoki.exe");
        Assert.Contains(mergedManifest.Platforms, platform => platform.Platform == "osx-arm64" && platform.GuiEntry == "osx-arm64/YokiFrame.Workbench.Avalonia.app/Contents/MacOS/YokiFrame.Workbench.Avalonia");
    }

    /// <summary>
    /// 验证合并 manifest 时会清除目录已经不存在的旧 profile，避免发布清单保留幽灵平台。
    /// </summary>
    [Fact]
    public void BuildWithExistingManifestDropsProfileWhoseDirectoryIsMissing()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe"), "gui-win");
        WriteFile(Path.Combine(runtimeRoot, "osx-arm64", "Workbench.app", "Contents", "MacOS", "Workbench"), "gui-osx");
        var builder = new RuntimeManifestBuilder();
        var windowsManifest = builder.Build(runtimeRoot, "YokiFrameTool", "win-x64", "YokiFrame.Workbench.Avalonia.exe");
        var mergedManifest = builder.Build(
            runtimeRoot,
            "YokiFrameTool",
            windowsManifest,
            "osx-arm64",
            "Workbench.app/Contents/MacOS/Workbench",
            string.Empty);
        Directory.Delete(Path.Combine(runtimeRoot, "osx-arm64"), recursive: true);

        var rebuiltManifest = builder.Build(
            runtimeRoot,
            "YokiFrameTool",
            mergedManifest,
            "win-x64",
            "YokiFrame.Workbench.Avalonia.exe",
            string.Empty);

        Assert.Equal(new[] { "win-x64" }, rebuiltManifest.Platforms.Select(static platform => platform.Platform));
    }

    /// <summary>
    /// 验证合并 manifest 时会清除入口已经不存在的旧 profile，即使旧平台目录仍然存在。
    /// </summary>
    [Fact]
    public void BuildWithExistingManifestDropsProfileWhoseEntrypointIsMissing()
    {
        var runtimeRoot = CreateRuntimeRoot();
        var windowsEntry = Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe");
        var macEntry = Path.Combine(runtimeRoot, "osx-arm64", "Workbench.app", "Contents", "MacOS", "Workbench");
        WriteFile(windowsEntry, "gui-win");
        WriteFile(macEntry, "gui-osx");
        var builder = new RuntimeManifestBuilder();
        var windowsManifest = builder.Build(runtimeRoot, "YokiFrameTool", "win-x64", "YokiFrame.Workbench.Avalonia.exe");
        var mergedManifest = builder.Build(
            runtimeRoot,
            "YokiFrameTool",
            windowsManifest,
            "osx-arm64",
            "Workbench.app/Contents/MacOS/Workbench",
            string.Empty);
        File.Delete(macEntry);

        var rebuiltManifest = builder.Build(
            runtimeRoot,
            "YokiFrameTool",
            mergedManifest,
            "win-x64",
            "YokiFrame.Workbench.Avalonia.exe",
            string.Empty);

        Assert.Equal(new[] { "win-x64" }, rebuiltManifest.Platforms.Select(static platform => platform.Platform));
    }

    /// <summary>
    /// 验证重复发布同一平台时会替换该平台记录，而不是生成重复 platform 条目。
    /// </summary>
    [Fact]
    public void BuildWithExistingManifestReplacesMatchingPlatform()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe"), "old-gui");
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "yoki.exe"), "old-cli");

        var builder = new RuntimeManifestBuilder();
        var firstManifest = builder.Build(
            runtimeRoot,
            "YokiFrameTool",
            "win-x64",
            "YokiFrame.Workbench.Avalonia.exe",
            "yoki.exe");

        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia2.exe"), "new-gui");
        var replacedManifest = builder.Build(
            runtimeRoot,
            "YokiFrameTool",
            firstManifest,
            "win-x64",
            "YokiFrame.Workbench.Avalonia2.exe",
            "yoki.exe");

        var platform = Assert.Single(replacedManifest.Platforms);
        Assert.Equal("win-x64/YokiFrame.Workbench.Avalonia2.exe", platform.GuiEntry);
    }

    /// <summary>
    /// 验证 manifest 不记录调试符号文件，避免发布副本带入大型 PDB。
    /// </summary>
    [Fact]
    public void BuildSkipsDebugSymbolFiles()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe"), "exe");
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Tooling.Application.dll"), "application");
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "libSkiaSharp.pdb"), "symbols");

        var manifest = new RuntimeManifestBuilder().Build(runtimeRoot, "Workbench", "win-x64", "YokiFrame.Workbench.Avalonia.exe");

        var platform = Assert.Single(manifest.Platforms);
        Assert.Equal(2, platform.FileCount);
        Assert.Equal(14, platform.TotalBytes);
        Assert.DoesNotContain(platform.Files, file => file.RelativePath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 验证缺少平台入口文件时会拒绝生成 manifest。
    /// </summary>
    [Fact]
    public void BuildRejectsMissingEntrypoint()
    {
        var runtimeRoot = CreateRuntimeRoot();
        Directory.CreateDirectory(Path.Combine(runtimeRoot, "win-x64"));

        Assert.Throws<FileNotFoundException>(() => new RuntimeManifestBuilder().Build(runtimeRoot, "Installer", "win-x64", "missing.exe"));
    }

    /// <summary>
    /// 验证共享运行时布局缺少 CLI 入口时拒绝生成 manifest，防止发布目录退回到 GUI-only。
    /// </summary>
    [Fact]
    public void BuildRejectsMissingCliEntryForSharedRuntime()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe"), "gui");

        Assert.Throws<FileNotFoundException>(() => new RuntimeManifestBuilder().Build(
            runtimeRoot,
            "YokiFrameTool",
            "win-x64",
            "YokiFrame.Workbench.Avalonia.exe",
            "yoki.exe"));
    }

    /// <summary>
    /// 验证平台标识不能通过相对路径或绝对路径跳出 WorkbenchRuntime 根。
    /// </summary>
    /// <param name="useAbsolutePath">是否使用绝对平台目录模拟越界。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildRejectsPlatformOutsideRuntimeRoot(bool useAbsolutePath)
    {
        var runtimeRoot = CreateRuntimeRoot();
        var outsideRoot = Path.Combine(Path.GetDirectoryName(runtimeRoot)!, "outside-" + Guid.NewGuid().ToString("N"));
        WriteFile(Path.Combine(outsideRoot, "Workbench.exe"), "outside");
        var platform = useAbsolutePath ? outsideRoot : Path.GetRelativePath(runtimeRoot, outsideRoot);

        Assert.Throws<ArgumentException>(() => new RuntimeManifestBuilder().Build(
            runtimeRoot,
            "YokiFrameTool",
            platform,
            "Workbench.exe"));
    }

    /// <summary>
    /// 验证 GUI 入口必须保留在当前平台目录内，不能读取相邻目录或绝对文件。
    /// </summary>
    /// <param name="useAbsolutePath">是否使用绝对入口路径模拟越界。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildRejectsGuiEntryOutsidePlatformRoot(bool useAbsolutePath)
    {
        var runtimeRoot = CreateRuntimeRoot();
        var platformRoot = Path.Combine(runtimeRoot, "win-x64");
        var outsidePath = Path.Combine(runtimeRoot, "outside.exe");
        Directory.CreateDirectory(platformRoot);
        WriteFile(outsidePath, "outside");
        var guiEntry = useAbsolutePath ? outsidePath : "../outside.exe";

        Assert.Throws<ArgumentException>(() => new RuntimeManifestBuilder().Build(
            runtimeRoot,
            "YokiFrameTool",
            "win-x64",
            guiEntry));
    }

    /// <summary>
    /// 验证共享 CLI 入口与 GUI 一样受平台目录 containment 约束。
    /// </summary>
    [Fact]
    public void BuildRejectsCliEntryOutsidePlatformRoot()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "Workbench.exe"), "gui");
        WriteFile(Path.Combine(runtimeRoot, "outside.exe"), "outside");

        Assert.Throws<ArgumentException>(() => new RuntimeManifestBuilder().Build(
            runtimeRoot,
            "YokiFrameTool",
            "win-x64",
            "Workbench.exe",
            "../outside.exe"));
    }

    /// <summary>
    /// 验证非 Windows 平台 containment 区分目录大小写，不能借助同名不同大小写的相邻目录越界。
    /// </summary>
    [Fact]
    public void BuildUsesCaseSensitiveContainmentOutsideWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var parentRoot = Path.Combine(Path.GetTempPath(), "yokiframe-packaging-tests", Guid.NewGuid().ToString("N"));
        var runtimeRoot = Path.Combine(parentRoot, "Runtime");
        var outsideRoot = Path.Combine(parentRoot, "runtime");
        Directory.CreateDirectory(Path.Combine(runtimeRoot, "linux-x64"));
        WriteFile(Path.Combine(outsideRoot, "outside"), "outside");

        Assert.Throws<ArgumentException>(() => new RuntimeManifestBuilder().Build(
            runtimeRoot,
            "YokiFrameTool",
            "linux-x64",
            "../../runtime/outside"));
    }

    /// <summary>
    /// 验证 manifest writer 会写出可反序列化的 JSON。
    /// </summary>
    [Fact]
    public void WriterCreatesReadableManifestJson()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe"), "tool");
        var manifest = new RuntimeManifestBuilder().Build(runtimeRoot, "YokiFrameTool", "win-x64", "YokiFrame.Workbench.Avalonia.exe");
        var outputPath = Path.Combine(runtimeRoot, "tool-manifest.json");

        new RuntimeManifestWriter().Write(manifest, outputPath);

        var written = JsonSerializer.Deserialize<RuntimeManifest>(File.ReadAllText(outputPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(written);
        Assert.Equal("YokiFrameTool", written.Product);
    }

    /// <summary>
    /// 验证 writer 通过替换目标文件完成提交，已打开的旧文件句柄不能观察到原地截断后的新内容。
    /// </summary>
    [Fact]
    public void WriterAtomicallyReplacesExistingManifest()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe"), "tool");
        var manifest = new RuntimeManifestBuilder().Build(runtimeRoot, "YokiFrameTool", "win-x64", "YokiFrame.Workbench.Avalonia.exe");
        var outputPath = Path.Combine(runtimeRoot, "tool-manifest.json");
        const string originalContent = "original-manifest";
        File.WriteAllText(outputPath, originalContent);

        using var originalHandle = new FileStream(
            outputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        new RuntimeManifestWriter().Write(manifest, outputPath);

        using var reader = new StreamReader(originalHandle, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true);
        Assert.Equal(originalContent, reader.ReadToEnd());
        Assert.NotEqual(originalContent, File.ReadAllText(outputPath));
    }

    /// <summary>
    /// 验证 writer 成功提交后不会在 manifest 同目录遗留临时文件。
    /// </summary>
    [Fact]
    public void WriterLeavesNoTemporaryManifestFiles()
    {
        var runtimeRoot = CreateRuntimeRoot();
        WriteFile(Path.Combine(runtimeRoot, "win-x64", "YokiFrame.Workbench.Avalonia.exe"), "tool");
        var manifest = new RuntimeManifestBuilder().Build(runtimeRoot, "YokiFrameTool", "win-x64", "YokiFrame.Workbench.Avalonia.exe");
        var outputPath = Path.Combine(runtimeRoot, "tool-manifest.json");

        new RuntimeManifestWriter().Write(manifest, outputPath);

        Assert.Empty(Directory.EnumerateFiles(runtimeRoot, "tool-manifest.json.*.tmp", SearchOption.TopDirectoryOnly));
    }

    /// <summary>
    /// 创建测试专用 runtime root。
    /// </summary>
    /// <returns>runtime root。</returns>
    private static string CreateRuntimeRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-packaging-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// 写入测试文件。
    /// </summary>
    /// <param name="path">文件路径。</param>
    /// <param name="text">文件内容。</param>
    private static void WriteFile(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>
    /// 计算测试文件的 SHA256。
    /// </summary>
    /// <param name="path">文件路径。</param>
    /// <returns>SHA256 文本。</returns>
    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
