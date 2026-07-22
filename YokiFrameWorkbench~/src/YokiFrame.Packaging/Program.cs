using YokiFrame.Packaging.Services;

namespace YokiFrame.Packaging;

/// <summary>
/// YokiFrame Packaging CLI 入口。
/// </summary>
public static class Program
{
    /// <summary>
    /// 执行 packaging 命令。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>进程退出码。</returns>
    public static int Main(string[] args)
    {
        try
        {
            if (IsCommand(args, "manifest", "write"))
            {
                return WriteManifest(args);
            }

            if (IsCommand(args, "runtime", "publish-current"))
            {
                return PublishCurrentRuntime(args);
            }

            if (IsCommand(args, "runtime", "bootstrap"))
            {
                return BootstrapRuntime(args);
            }

            if (IsCommand(args, "runtime", "publish"))
            {
                return PublishRuntimeProfile(args);
            }

            if (IsCommand(args, "runtime", "release-prepare"))
            {
                return PrepareRuntimeRelease(args);
            }

            if (IsCommand(args, "runtime", "release-verify"))
            {
                return VerifyRuntimeRelease(args);
            }

            throw new ArgumentException(CreateUsageText());
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// 写入或合并指定平台 runtime manifest。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>成功退出码 0。</returns>
    private static int WriteManifest(string[] args)
    {
        var runtimeRoot = RequireOption(args, "runtime-root");
        var product = RequireOption(args, "product");
        var platform = RequireOption(args, "platform");
        var guiEntry = GetOption(args, "gui-entry") ?? RequireOption(args, "entrypoint");
        var cliEntry = GetOption(args, "cli-entry") ?? string.Empty;
        var output = RuntimePathGuard.RequireManifestPath(runtimeRoot, RequireOption(args, "output"));
        var existingManifest = new RuntimeManifestReader().ReadIfExists(output);
        var manifest = new RuntimeManifestBuilder().Build(
            runtimeRoot,
            product,
            existingManifest,
            platform,
            guiEntry,
            cliEntry);
        new RuntimeManifestWriter().Write(manifest, output);
        Console.WriteLine("YokiFrame manifest written to " + Path.GetFullPath(output));
        return 0;
    }

    /// <summary>
    /// 在当前宿主构建 Workbench、Installer 模式与 `yoki` 共享运行时。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>成功退出码 0。</returns>
    private static int PublishCurrentRuntime(string[] args)
    {
        var packageRoot = RequireOption(args, "package-root");
        var projectRoot = RequireOption(args, "project-root");
        var configuration = GetOption(args, "configuration") ?? "Release";
        var profile = RuntimePublishProfileResolver.ResolveCurrent();
        var result = new RuntimeCacheService().Publish(
            packageRoot,
            projectRoot,
            configuration,
            profile.RuntimeIdentifier,
            startupOptimized: false);
        WriteBootstrapResult(result);
        return 0;
    }

    /// <summary>
    /// 按源码指纹确保当前项目缓存可用；缓存完整时不重复执行 Native AOT 或 managed publish。
    /// </summary>
    /// <param name="args">Packaging CLI 参数。</param>
    /// <returns>成功退出码 0。</returns>
    private static int BootstrapRuntime(string[] args)
    {
        var packageRoot = RequireOption(args, "package-root");
        var projectRoot = RequireOption(args, "project-root");
        var configuration = GetOption(args, "configuration") ?? "Release";
        var result = new RuntimeCacheService().Bootstrap(packageRoot, projectRoot, configuration);
        WriteBootstrapResult(result);
        if (HasFlag(args, "open-installer"))
        {
            new RuntimeInstallerLauncher().Launch(result, packageRoot, projectRoot);
            Console.WriteLine("YokiFrame Installer started from the current Runtime cache.");
        }

        return 0;
    }

    /// <summary>
    /// 发布 allowlist 中的指定 profile，供维护脚本和 CI 复用同一 C# 提交实现。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>成功退出码 0。</returns>
    private static int PublishRuntimeProfile(string[] args)
    {
        var packageRoot = RequireOption(args, "package-root");
        var projectRoot = RequireOption(args, "project-root");
        var configuration = GetOption(args, "configuration") ?? "Release";
        var runtimeIdentifier = RequireOption(args, "profile");
        var startupOptimized = HasFlag(args, "startup-optimized");
        var result = new RuntimeCacheService().Publish(
            packageRoot,
            projectRoot,
            configuration,
            runtimeIdentifier,
            startupOptimized);
        WriteBootstrapResult(result);
        return 0;
    }

    /// <summary>
    /// 执行 Git URL 源码包预检；该命令不再生成包内 Runtime 文件。
    /// </summary>
    /// <param name="args">Packaging CLI 参数。</param>
    /// <returns>成功退出码 0。</returns>
    private static int PrepareRuntimeRelease(string[] args)
    {
        var packageRoot = RequireOption(args, "package-root");
        new RuntimeReleaseService().Prepare(packageRoot);
        Console.WriteLine("YokiFrame source release is free of package-local Runtime payloads.");
        return 0;
    }

    /// <summary>
    /// 校验 Git index 中即将进入 Git URL 包的内容不包含可再生产物。
    /// </summary>
    /// <param name="args">Packaging CLI 参数。</param>
    /// <returns>成功退出码 0。</returns>
    private static int VerifyRuntimeRelease(string[] args)
    {
        var packageRoot = RequireOption(args, "package-root");
        new RuntimeReleaseService().Verify(packageRoot);
        Console.WriteLine("YokiFrame source release verified for Git URL.");
        return 0;
    }

    /// <summary>
    /// 输出项目缓存的源码指纹、GUI、可选 CLI 与 manifest 结果。
    /// </summary>
    /// <param name="result">项目缓存 bootstrap 或发布结果。</param>
    private static void WriteBootstrapResult(YokiFrame.Packaging.Models.RuntimeCacheBootstrapResult result)
    {
        var publishResult = result.PublishResult;
        Console.WriteLine("YokiFrame Runtime cache " + (result.Rebuilt ? "published" : "reused") + " for " + publishResult.RuntimeIdentifier + ".");
        Console.WriteLine("Source fingerprint: " + result.SourceFingerprint);
        Console.WriteLine("Runtime root: " + result.RuntimeRoot);
        Console.WriteLine("GUI: " + publishResult.GuiPath);
        if (!string.IsNullOrWhiteSpace(publishResult.CliPath))
        {
            Console.WriteLine("CLI: " + publishResult.CliPath);
        }

        Console.WriteLine("Manifest: " + publishResult.ManifestPath);
    }

    /// <summary>
    /// 判断参数是否匹配两段式命令名。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="group">命令组。</param>
    /// <param name="action">命令动作。</param>
    /// <returns>命令匹配时返回 true。</returns>
    private static bool IsCommand(string[] args, string group, string action)
    {
        return args.Length >= 2
            && string.Equals(args[0], group, StringComparison.OrdinalIgnoreCase)
            && string.Equals(args[1], action, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 创建 Packaging CLI 支持命令的简洁用法文本。
    /// </summary>
    /// <returns>命令用法。</returns>
    private static string CreateUsageText()
    {
        return "Use one of:"
            + Environment.NewLine
            + "  manifest write --runtime-root <path> --product <name> --platform <id> --gui-entry <file> --cli-entry <file> --output <manifest.json>"
            + Environment.NewLine
            + "  runtime bootstrap --package-root <YokiFrame> --project-root <project> [--configuration Release] [--open-installer]"
            + Environment.NewLine
            + "  runtime publish-current --package-root <YokiFrame> --project-root <project> [--configuration Release]"
            + Environment.NewLine
            + "  runtime publish --package-root <YokiFrame> --project-root <project> --profile <id> [--configuration Release] [--startup-optimized]"
            + Environment.NewLine
            + "  runtime release-prepare --package-root <YokiFrame>"
            + Environment.NewLine
            + "  runtime release-verify --package-root <YokiFrame>";
    }

    /// <summary>
    /// 读取必填命令选项。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="name">选项名。</param>
    /// <returns>选项值。</returns>
    private static string RequireOption(string[] args, string name)
    {
        return GetOption(args, name) ?? throw new ArgumentException("Missing required option --" + name + ".");
    }

    /// <summary>
    /// 读取可选命令选项。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="name">选项名。</param>
    /// <returns>选项值；缺失时返回 null。</returns>
    private static string? GetOption(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--" + name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// 判断命令行是否包含不带值的布尔开关。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="name">开关名。</param>
    /// <returns>存在 `--name` 时返回 true。</returns>
    private static bool HasFlag(string[] args, string name)
    {
        return args.Any(argument => string.Equals(argument, "--" + name, StringComparison.OrdinalIgnoreCase));
    }
}
