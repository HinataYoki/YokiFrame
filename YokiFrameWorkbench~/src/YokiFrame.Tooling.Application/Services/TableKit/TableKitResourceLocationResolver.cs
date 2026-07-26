using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Tooling.Application.Services.TableKit;

/// <summary>解析 TableKit 的可寻址开关和运行时路径模板。</summary>
public sealed class TableKitResourceLocationResolver
{
    private const string ADDRESSABLE_PATH_PATTERN = "{0}";

    /// <summary>解析最终写入生成契约的资源路径模板。</summary>
    /// <param name="options">包含可寻址开关、项目根和输出目录的选项。</param>
    /// <returns>可寻址标记和可供 Loader 使用的路径模板。</returns>
    public TableKitRuntimeLocation Resolve(TableKitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.IsAddressable)
        {
            return CreateLocation(true, ADDRESSABLE_PATH_PATTERN);
        }

        if (!string.IsNullOrWhiteSpace(options.RuntimePathPattern))
        {
            return CreateLocation(false, options.RuntimePathPattern);
        }

        return InferLocation(options.ProjectRoot, options.OutputDataDir);
    }

    /// <summary>根据 Unity、Godot 或未知宿主推导运行时路径模板。</summary>
    /// <param name="projectRoot">当前宿主项目根。</param>
    /// <param name="outputDataDir">Luban 数据输出目录。</param>
    /// <returns>包含 `{0}` 表名占位符的路径模板。</returns>
    private static TableKitRuntimeLocation InferLocation(string projectRoot, string outputDataDir)
    {
        string root = Path.GetFullPath(projectRoot);
        string output = ResolveProjectPath(root, outputDataDir);
        bool isUnity = File.Exists(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"));
        bool isGodot = File.Exists(Path.Combine(root, "project.godot"));
        if (isUnity == isGodot)
        {
            throw new InvalidDataException("TableKit 自动路径无法识别唯一宿主，请开启资源可寻址或填写运行时地址模板。");
        }

        return isUnity ? InferUnityLocation(root, output) : InferGodotLocation(root, output);
    }

    /// <summary>从 Unity Resources 或 StreamingAssets 输出目录推导路径模板。</summary>
    /// <param name="projectRoot">Unity 项目根目录。</param>
    /// <param name="outputDataDirectory">数据输出绝对目录。</param>
    /// <returns>Unity 运行时路径模板。</returns>
    private static TableKitRuntimeLocation InferUnityLocation(string projectRoot, string outputDataDirectory)
    {
        string assetsRoot = Path.Combine(projectRoot, "Assets");
        string relative = GetContainedRelativePath(assetsRoot, outputDataDirectory, "Unity 数据输出目录必须位于 Assets 下。");
        string[] segments = SplitPath(relative);
        int resourcesIndex = Array.FindLastIndex(segments, static value => value.Equals("Resources", StringComparison.OrdinalIgnoreCase));
        if (resourcesIndex >= 0)
        {
            return CreateLocation(false, AppendPlaceholder(JoinSegments(segments, resourcesIndex + 1)));
        }

        if (segments.Length > 0 && segments[0].Equals("StreamingAssets", StringComparison.OrdinalIgnoreCase))
        {
            string path = "streaming-assets://" + JoinSegments(segments, 1);
            return CreateLocation(false, AppendPlaceholder(path));
        }

        throw new InvalidDataException("Unity 自动路径只支持 Resources/StreamingAssets，请开启资源可寻址或编辑运行时地址模板。");
    }

    /// <summary>从 Godot 项目内输出目录推导 res:// 路径模板。</summary>
    /// <param name="projectRoot">Godot 项目根目录。</param>
    /// <param name="outputDataDirectory">数据输出绝对目录。</param>
    /// <returns>Godot res:// 路径模板。</returns>
    private static TableKitRuntimeLocation InferGodotLocation(string projectRoot, string outputDataDirectory)
    {
        string relative = GetContainedRelativePath(
            projectRoot,
            outputDataDirectory,
            "Godot 自动路径只支持项目内 res:// 目录，请开启资源可寻址或编辑运行时地址模板。");
        string path = relative == "." ? "res://" : "res://" + NormalizeSeparators(relative);
        return CreateLocation(false, AppendPlaceholder(path));
    }

    /// <summary>校验并规范化用户填写的运行时路径模板或资源根。</summary>
    /// <param name="isAddressable">是否直接按 Luban 表名寻址。</param>
    /// <param name="value">运行时路径模板或资源根。</param>
    /// <returns>规范化路径定位。</returns>
    private static TableKitRuntimeLocation CreateLocation(bool isAddressable, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("TableKit 运行时地址模板不能为空。");
        }
        if (value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
        {
            throw new InvalidDataException("TableKit 运行时地址模板不能包含换行。");
        }

        return new TableKitRuntimeLocation
        {
            IsAddressable = isAddressable,
            PathPattern = NormalizeSeparators(AppendPlaceholder(value.Trim()))
        };
    }

    /// <summary>把项目相对输出解析为绝对路径，保留显式绝对目录。</summary>
    /// <param name="projectRoot">规范化项目根。</param>
    /// <param name="path">绝对或项目相对路径。</param>
    /// <returns>规范化绝对路径。</returns>
    private static string ResolveProjectPath(string projectRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("TableKit 数据输出目录不能为空。");
        return Path.GetFullPath(Path.IsPathFullyQualified(path) ? path : Path.Combine(projectRoot, path));
    }

    /// <summary>取得被指定根包含的相对路径，越界时返回领域错误。</summary>
    /// <param name="root">允许的目录根。</param>
    /// <param name="path">待检查绝对路径。</param>
    /// <param name="errorMessage">越界错误。</param>
    /// <returns>根目录相对路径。</returns>
    private static string GetContainedRelativePath(string root, string path, string errorMessage)
    {
        string relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        if (relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relative))
        {
            throw new InvalidDataException(errorMessage);
        }
        return relative;
    }

    /// <summary>拆分宿主文件系统相对路径。</summary>
    /// <param name="path">待拆分路径。</param>
    /// <returns>移除空段后的路径段。</returns>
    private static string[] SplitPath(string path)
    {
        return path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>从指定索引连接路径段。</summary>
    /// <param name="segments">路径段。</param>
    /// <param name="startIndex">首个保留段。</param>
    /// <returns>使用正斜杠连接的路径。</returns>
    private static string JoinSegments(IReadOnlyList<string> segments, int startIndex)
    {
        return startIndex >= segments.Count ? string.Empty : string.Join('/', segments.Skip(startIndex));
    }

    /// <summary>为数据根补充表名占位符，已有占位符时保持原值。</summary>
    /// <param name="value">资源根或路径模板。</param>
    /// <returns>包含 `{0}` 的路径模板。</returns>
    private static string AppendPlaceholder(string value)
    {
        string normalized = NormalizeSeparators(value);
        if (normalized.Contains("{0}", StringComparison.Ordinal)) return normalized;
        if (normalized.EndsWith("://", StringComparison.Ordinal)) return normalized + "{0}";
        normalized = normalized.TrimEnd('/');
        return string.IsNullOrEmpty(normalized) ? "{0}" : normalized + "/{0}";
    }

    /// <summary>统一跨宿主定位值的目录分隔符。</summary>
    /// <param name="value">原始定位值。</param>
    /// <returns>使用正斜杠的定位值。</returns>
    private static string NormalizeSeparators(string value)
    {
        return value.Replace('\\', '/');
    }
}
