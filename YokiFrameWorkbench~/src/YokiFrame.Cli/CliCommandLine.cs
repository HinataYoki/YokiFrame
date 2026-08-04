using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.CommandLine;

namespace YokiFrame.Cli;

/// <summary>
/// 提供极小命令行解析器，避免 Phase 1 为 CLI 引入额外包依赖。
/// </summary>
internal sealed class CliCommandLine
{
    private readonly ToolCommandLineOptions mOptions;

    /// <summary>
    /// 根据原始参数创建解析结果。
    /// </summary>
    /// <param name="options">共享词法解析结果。</param>
    private CliCommandLine(ToolCommandLineOptions options)
    {
        mOptions = options;
    }

    /// <summary>
    /// 获取命令动词片段。
    /// </summary>
    public IReadOnlyList<string> Verbs => mOptions.Verbs;

    /// <summary>
    /// 获取本次命令出现过的选项名称，供命令边界执行严格 schema 校验。
    /// </summary>
    public IReadOnlyCollection<string> OptionNames => mOptions.OptionNames;

    /// <summary>
    /// 获取重复出现的选项名称；重复值不会静默覆盖先前输入。
    /// </summary>
    public IReadOnlyCollection<string> DuplicateOptionNames => mOptions.DuplicateOptionNames;

    /// <summary>
    /// 判断调用方是否显式提供了指定选项。
    /// </summary>
    /// <param name="name">不含双横线的选项名。</param>
    /// <returns>显式提供时返回 true。</returns>
    public bool HasOption(string name)
    {
        return mOptions.HasOption(name);
    }

    /// <summary>
    /// 解析命令行参数；支持 `--name value` 与 `--name=value` 两种写法。
    /// </summary>
    /// <param name="args">原始命令行参数。</param>
    /// <returns>解析后的命令行对象。</returns>
    public static CliCommandLine Parse(string[] args)
    {
        return new CliCommandLine(ToolCommandLineOptions.Parse(args));
    }

    /// <summary>
    /// 读取字符串参数，缺失时返回默认值。
    /// </summary>
    /// <param name="name">参数名。</param>
    /// <param name="defaultValue">默认值。</param>
    /// <returns>参数值或默认值。</returns>
    public string GetOption(string name, string defaultValue)
    {
        return mOptions.GetOption(name, defaultValue);
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
        if (!mOptions.HasOption(name))
        {
            return defaultValue;
        }

        var text = mOptions.GetOption(name, string.Empty);

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
        return mOptions.IsCommand(verbs);
    }
}
