using YokiFrame;

namespace YokiFrame.Tooling.Application.Environment;

/// <summary>
/// 解析 Workbench 访问 FileBridge 时使用的项目根目录。
/// </summary>
public static class WorkbenchProjectRootResolver
{
    /// <summary>
    /// 从命令行或当前目录解析项目根目录；显式 `--project` 优先。
    /// </summary>
    /// <param name="args">Workbench 启动参数。</param>
    /// <param name="currentDirectory">当前工作目录。</param>
    /// <returns>项目根目录完整路径。</returns>
    public static string Resolve(string[] args, string currentDirectory)
    {
        var explicitRoot = ReadOption(args, "project");
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return Path.GetFullPath(explicitRoot);
        }

        return ResolveFromDirectory(currentDirectory);
    }

    /// <summary>
    /// 从参数数组中读取 `--name value` 或 `--name=value` 形式的选项。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="name">选项名，不包含前缀。</param>
    /// <returns>选项值；不存在时返回空字符串。</returns>
    private static string ReadOption(string[] args, string name)
    {
        var prefix = "--" + name + "=";
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return args[index][prefix.Length..];
            }

            if (string.Equals(args[index], "--" + name, StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                return args[index + 1];
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// 从指定目录向上查找包含 `.yokiframe` 的项目根。
    /// </summary>
    /// <param name="currentDirectory">起始目录。</param>
    /// <returns>项目根目录；找不到时返回起始目录完整路径。</returns>
    private static string ResolveFromDirectory(string currentDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(currentDirectory));
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(currentDirectory);
    }
}
