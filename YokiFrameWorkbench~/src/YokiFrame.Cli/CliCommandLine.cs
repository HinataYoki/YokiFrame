using YokiFrame.Protocol.Results;

namespace YokiFrame.Cli;

/// <summary>
/// 提供极小命令行解析器，避免 Phase 1 为 CLI 引入额外包依赖。
/// </summary>
internal sealed class CliCommandLine
{
    private readonly Dictionary<string, string> mOptions;

    /// <summary>
    /// 根据原始参数创建解析结果。
    /// </summary>
    /// <param name="verbs">命令动词片段。</param>
    /// <param name="options">命名参数表。</param>
    private CliCommandLine(IReadOnlyList<string> verbs, Dictionary<string, string> options)
    {
        Verbs = verbs;
        mOptions = options;
    }

    /// <summary>
    /// 获取命令动词片段。
    /// </summary>
    public IReadOnlyList<string> Verbs { get; }

    /// <summary>
    /// 获取本次命令出现过的选项名称，供命令边界执行严格 schema 校验。
    /// </summary>
    public IReadOnlyCollection<string> OptionNames => mOptions.Keys;

    /// <summary>
    /// 判断调用方是否显式提供了指定选项。
    /// </summary>
    /// <param name="name">不含双横线的选项名。</param>
    /// <returns>显式提供时返回 true。</returns>
    public bool HasOption(string name)
    {
        return mOptions.ContainsKey(name);
    }

    /// <summary>
    /// 解析命令行参数；支持 `--name value` 与 `--name=value` 两种写法。
    /// </summary>
    /// <param name="args">原始命令行参数。</param>
    /// <returns>解析后的命令行对象。</returns>
    public static CliCommandLine Parse(string[] args)
    {
        List<string> verbs = new();
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                index = ParseOption(args, index, options);
            }
            else
            {
                verbs.Add(argument);
            }
        }

        return new CliCommandLine(verbs, options);
    }

    /// <summary>
    /// 读取字符串参数，缺失时返回默认值。
    /// </summary>
    /// <param name="name">参数名。</param>
    /// <param name="defaultValue">默认值。</param>
    /// <returns>参数值或默认值。</returns>
    public string GetOption(string name, string defaultValue)
    {
        return mOptions.TryGetValue(name, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// 读取整数参数，无法解析时返回默认值。
    /// </summary>
    /// <param name="name">参数名。</param>
    /// <param name="defaultValue">默认值。</param>
    /// <returns>整数参数值或默认值。</returns>
    public int GetIntOption(string name, int defaultValue)
    {
        return int.TryParse(GetOption(name, string.Empty), out var value) ? value : defaultValue;
    }

    /// <summary>
    /// 读取布尔参数；裸开关等价于 true，显式值只接受 true 或 false。
    /// </summary>
    /// <param name="name">参数名。</param>
    /// <param name="defaultValue">参数缺失时使用的默认值。</param>
    /// <returns>解析后的布尔值。</returns>
    public bool GetBoolOption(string name, bool defaultValue)
    {
        if (!mOptions.TryGetValue(name, out var text))
        {
            return defaultValue;
        }

        if (bool.TryParse(text, out var value))
        {
            return value;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "InvalidOptionValue",
            $"Option --{name} must be true or false.",
            $"Use --{name}, --{name}=true or --{name}=false.",
            Array.Empty<string>()));
    }

    /// <summary>
    /// 尝试读取长整型参数；缺失或无法解析时返回 false。
    /// </summary>
    /// <param name="name">参数名。</param>
    /// <param name="value">解析出的长整型值。</param>
    /// <returns>参数存在且可解析时返回 true。</returns>
    public bool TryGetLongOption(string name, out long value)
    {
        return long.TryParse(GetOption(name, string.Empty), out value);
    }

    /// <summary>
    /// 判断命令动词是否与指定片段完全一致。
    /// </summary>
    /// <param name="verbs">期望动词片段。</param>
    /// <returns>完全一致时返回 true。</returns>
    public bool IsCommand(params string[] verbs)
    {
        return Verbs.Count == verbs.Length && !verbs.Where((verb, index) =>
            !string.Equals(Verbs[index], verb, StringComparison.OrdinalIgnoreCase)).Any();
    }

    /// <summary>
    /// 解析单个命名参数。
    /// </summary>
    /// <param name="args">原始命令行参数。</param>
    /// <param name="index">当前参数索引。</param>
    /// <param name="options">待写入的参数表。</param>
    /// <returns>解析后新的参数索引。</returns>
    private static int ParseOption(string[] args, int index, Dictionary<string, string> options)
    {
        var argument = args[index][2..];
        var equalsIndex = argument.IndexOf('=');
        if (equalsIndex >= 0)
        {
            options[argument[..equalsIndex]] = argument[(equalsIndex + 1)..];
            return index;
        }

        if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            options[argument] = args[index + 1];
            return index + 1;
        }

        options[argument] = "true";
        return index;
    }
}
