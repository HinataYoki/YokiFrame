using System.Security.Cryptography;
using System.Text;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 将包内源码投影、生成 UID 和薄启动入口组合成一个可整体替换的 Godot add-on 投影。
/// </summary>
internal sealed class GodotAddonProjectionBuilder
{
    private const string PACKAGE_PREFIX = "package/YokiFrame/";

    /// <summary>
    /// 在事务生成目录物化薄入口，并把所有内容重映射到 `addons/yokiframe` 的单一目录投影。
    /// </summary>
    /// <param name="packageProjection">已经包含包内 UID sidecar 的最终源码投影。</param>
    /// <param name="plan">已完成只读验证的 Godot 安装计划。</param>
    /// <param name="generatedRoot">同项目卷内的生成入口源目录。</param>
    /// <returns>以 add-on 根为相对路径起点的稳定投影。</returns>
    public PackageProjection Build(
        PackageProjection packageProjection,
        GodotInstallPlan plan,
        string generatedRoot)
    {
        ArgumentNullException.ThrowIfNull(packageProjection);
        ArgumentNullException.ThrowIfNull(plan);
        var fullGeneratedRoot = InstallerPathGuard.RequireFullPath(generatedRoot, nameof(generatedRoot));
        List<PackageProjectionFile> files = new(packageProjection.Files.Count + 5);
        foreach (var file in packageProjection.Files)
        {
            files.Add(new PackageProjectionFile(
                file.SourcePath,
                PACKAGE_PREFIX + file.RelativePath,
                file.Sha256,
                file.Length));
        }

        AddGeneratedFile(files, fullGeneratedRoot, "plugin.cfg", plan.PluginConfigContent);
        AddGeneratedFile(files, fullGeneratedRoot, "YokiFrameGodotEditorPlugin.cs", plan.PluginScriptContent);
        AddGeneratedFile(files, fullGeneratedRoot, "YokiFrameGodotEditorPlugin.cs.uid", plan.PluginScriptUidContent);
        AddGeneratedFile(files, fullGeneratedRoot, "YokiFrameGodotBootstrap.cs", plan.RuntimeBootstrapContent);
        AddGeneratedFile(files, fullGeneratedRoot, "YokiFrameGodotBootstrap.cs.uid", plan.RuntimeBootstrapUidContent);
        files.Sort(static (left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));
        return new PackageProjection(packageProjection.SourcePackageRoot, packageProjection.RuntimeProfile, files);
    }

    /// <summary>
    /// 写入一个 Installer 生成的 add-on 文件，并立即创建对应的长度和 SHA-256 摘要。
    /// </summary>
    /// <param name="files">最终投影文件收集。</param>
    /// <param name="generatedRoot">事务生成目录。</param>
    /// <param name="relativePath">相对 add-on 根的稳定路径。</param>
    /// <param name="content">完整 UTF-8 文本内容。</param>
    private static void AddGeneratedFile(
        ICollection<PackageProjectionFile> files,
        string generatedRoot,
        string relativePath,
        string content)
    {
        var sourcePath = InstallerPathGuard.CombineInside(
            generatedRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        using (FileStream stream = new(sourcePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        using (StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write(content);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        using var readStream = File.OpenRead(sourcePath);
        var hash = SHA256.HashData(readStream);
        files.Add(new PackageProjectionFile(
            sourcePath,
            relativePath,
            Convert.ToHexString(hash).ToLowerInvariant(),
            readStream.Length));
    }
}
