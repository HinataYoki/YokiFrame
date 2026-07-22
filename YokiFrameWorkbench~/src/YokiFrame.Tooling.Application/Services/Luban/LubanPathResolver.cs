using YokiFrame.Tooling.Application.Models.Luban;

namespace YokiFrame.Tooling.Application.Services.Luban;

/// <summary>统一解析 Luban 调用参数中的项目相对路径，避免 CLI、Workbench 与 Kit 服务各自依赖当前进程目录。</summary>
internal static class LubanPathResolver
{
    /// <summary>把绝对路径或项目根相对路径转换为规范绝对路径。</summary>
    /// <param name="options">包含项目根的 Luban 工具参数。</param>
    /// <param name="path">待解析的路径。</param>
    /// <param name="description">用于异常提示的路径语义。</param>
    /// <returns>规范化后的绝对路径。</returns>
    public static string ResolveProjectPath(LubanToolOptions options, string path, string description)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ProjectRoot))
        {
            throw new ArgumentException("Luban 项目根不能为空。", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(description + " 路径不能为空。", nameof(path));
        }

        string projectRoot = Path.GetFullPath(options.ProjectRoot);
        return Path.GetFullPath(Path.IsPathFullyQualified(path) ? path : Path.Combine(projectRoot, path));
    }

    /// <summary>解析 luban.conf 路径，空配置由调用方按自身结果模型处理。</summary>
    /// <param name="options">当前 Luban 工具参数。</param>
    /// <returns>规范化后的配置绝对路径；未配置时返回空文本。</returns>
    public static string ResolveConfigPath(LubanToolOptions options)
    {
        return string.IsNullOrWhiteSpace(options.LubanConfigPath)
            ? string.Empty
            : ResolveProjectPath(options, options.LubanConfigPath, "luban.conf");
    }

    /// <summary>解析 Luban 进程工作目录；相对目录始终以项目根为基准，空值回落配置文件目录。</summary>
    /// <param name="options">当前 Luban 工具参数。</param>
    /// <param name="configPath">已经规范化的 luban.conf 路径。</param>
    /// <returns>用于启动外部进程的工作目录。</returns>
    public static string ResolveWorkDirectory(LubanToolOptions options, string configPath)
    {
        return string.IsNullOrWhiteSpace(options.LubanWorkDir)
            ? Path.GetDirectoryName(configPath)!
            : ResolveProjectPath(options, options.LubanWorkDir, "Luban 工作目录");
    }
}
