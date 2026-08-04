namespace YokiFrame.Tooling.Application.CommandLine;

/// <summary>
/// 保存 Workbench、Installer 和 CLI 共用的命令行词法结果。
/// </summary>
public sealed class ToolCommandLineOptions
{
    private readonly Dictionary<string, string> mOptions;
    private readonly IReadOnlyCollection<string> mDuplicateOptionNames;

    /// <summary>
    /// 创建一个已经完成词法解析的选项集合。
    /// </summary>
    /// <param name="verbs">命令动词片段。</param>
    /// <param name="options">不含双横线的选项和值。</param>
    /// <param name="duplicateOptionNames">重复出现的选项名。</param>
    private ToolCommandLineOptions(
        IReadOnlyList<string> verbs,
        Dictionary<string, string> options,
        IReadOnlyCollection<string> duplicateOptionNames)
    {
        Verbs = verbs;
        mOptions = options;
        mDuplicateOptionNames = duplicateOptionNames;
    }

    /// <summary>获取命令动词片段。</summary>
    public IReadOnlyList<string> Verbs { get; }

    /// <summary>获取显式出现过的选项名。</summary>
    public IReadOnlyCollection<string> OptionNames => mOptions.Keys;

    /// <summary>获取重复出现的选项名。</summary>
    public IReadOnlyCollection<string> DuplicateOptionNames => mDuplicateOptionNames;

    /// <summary>
    /// 解析 `--name value`、`--name=value` 和裸布尔开关，保留重复选项诊断。
    /// </summary>
    /// <param name="args">原始命令行参数。</param>
    /// <returns>共享词法解析结果。</returns>
    public static ToolCommandLineOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        List<string> verbs = new();
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> duplicates = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                verbs.Add(argument);
                continue;
            }

            index = ParseOption(args, index, options, duplicates);
        }

        return new ToolCommandLineOptions(verbs, options, duplicates);
    }

    /// <summary>判断调用方是否显式提供了选项。</summary>
    /// <param name="name">不含双横线的选项名。</param>
    /// <returns>存在时返回 true。</returns>
    public bool HasOption(string name)
    {
        return mOptions.ContainsKey(name);
    }

    /// <summary>读取字符串选项，缺失时返回默认值。</summary>
    /// <param name="name">不含双横线的选项名。</param>
    /// <param name="defaultValue">缺失时的默认值。</param>
    /// <returns>选项值或默认值。</returns>
    public string GetOption(string name, string defaultValue)
    {
        return mOptions.TryGetValue(name, out var value) ? value : defaultValue;
    }

    /// <summary>判断动词片段是否完全匹配。</summary>
    /// <param name="verbs">期望的动词片段。</param>
    /// <returns>完全匹配时返回 true。</returns>
    public bool IsCommand(params string[] verbs)
    {
        return Verbs.Count == verbs.Length
            && !verbs.Where((verb, index) =>
                !string.Equals(Verbs[index], verb, StringComparison.OrdinalIgnoreCase)).Any();
    }

    /// <summary>
    /// 解析单个选项；值缺失时将其视为裸布尔开关 true。
    /// </summary>
    /// <param name="args">原始参数。</param>
    /// <param name="index">当前选项索引。</param>
    /// <param name="options">待写入选项字典。</param>
    /// <param name="duplicates">重复选项集合。</param>
    /// <returns>消费值参数后的索引。</returns>
    private static int ParseOption(
        string[] args,
        int index,
        Dictionary<string, string> options,
        ISet<string> duplicates)
    {
        var argument = args[index][2..];
        var equalsIndex = argument.IndexOf('=');
        var name = equalsIndex >= 0 ? argument[..equalsIndex] : argument;
        if (options.ContainsKey(name))
        {
            duplicates.Add(name);
        }

        if (equalsIndex >= 0)
        {
            options[name] = argument[(equalsIndex + 1)..];
            return index;
        }

        if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            options[name] = args[index + 1];
            return index + 1;
        }

        options[name] = "true";
        return index;
    }
}
