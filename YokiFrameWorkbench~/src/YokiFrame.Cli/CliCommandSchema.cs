using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Cli;

/// <summary>
/// 描述 CLI 选项的词法类型和可选范围。
/// </summary>
internal enum CliOptionValueKind
{
    String,
    Boolean,
    Int32,
    Int64
}

/// <summary>
/// 描述一个命令允许使用的选项。
/// </summary>
internal sealed class CliOptionSpec
{
    /// <summary>
    /// 创建选项描述。
    /// </summary>
    /// <param name="name">不含双横线的选项名。</param>
    /// <param name="valueKind">选项值类型。</param>
    /// <param name="required">是否必须显式提供。</param>
    /// <param name="minimum">数值下限；字符串和布尔值不使用。</param>
    /// <param name="maximum">数值上限；字符串和布尔值不使用。</param>
    public CliOptionSpec(
        string name,
        CliOptionValueKind valueKind = CliOptionValueKind.String,
        bool required = false,
        long? minimum = null,
        long? maximum = null)
    {
        Name = name;
        ValueKind = valueKind;
        Required = required;
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>获取不含双横线的选项名。</summary>
    public string Name { get; }

    /// <summary>获取选项值类型。</summary>
    public CliOptionValueKind ValueKind { get; }

    /// <summary>获取选项是否必填。</summary>
    public bool Required { get; }

    /// <summary>获取数值下限。</summary>
    public long? Minimum { get; }

    /// <summary>获取数值上限。</summary>
    public long? Maximum { get; }
}

/// <summary>
/// 描述一组完整 CLI 动词及其选项契约。
/// </summary>
internal sealed class CliCommandSchema
{
    private readonly HashSet<string> mOptionNames;
    private readonly IReadOnlyList<CliOptionSpec> mOptions;

    /// <summary>
    /// 创建命令 schema。
    /// </summary>
    /// <param name="commandName">稳定命令名称。</param>
    /// <param name="verbs">命令动词片段。</param>
    /// <param name="options">允许的选项描述。</param>
    public CliCommandSchema(
        string commandName,
        IReadOnlyList<string> verbs,
        IReadOnlyList<CliOptionSpec> options)
    {
        CommandName = commandName;
        Verbs = verbs;
        mOptions = options;
        mOptionNames = options
            .Select(static option => option.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>获取稳定命令名称。</summary>
    public string CommandName { get; }

    /// <summary>获取命令动词片段。</summary>
    public IReadOnlyList<string> Verbs { get; }

    /// <summary>
    /// 判断解析结果是否匹配当前 schema。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <returns>动词完全一致时返回 true。</returns>
    public bool Matches(CliCommandLine commandLine)
    {
        return commandLine.IsCommand(Verbs.ToArray());
    }

    /// <summary>
    /// 校验选项名称、必填项、类型和数值范围。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    public void Validate(CliCommandLine commandLine)
    {
        if (commandLine.DuplicateOptionNames.Count > 0)
        {
            var duplicate = commandLine.DuplicateOptionNames.First();
            throw CreateInputException(
                "DuplicateOption",
                "Option --" + duplicate + " must be provided at most once for " + CommandName + ".",
                "Remove the duplicate option and provide one unambiguous value.");
        }

        foreach (var optionName in commandLine.OptionNames)
        {
            if (!mOptionNames.Contains(optionName))
            {
                throw CreateInputException(
                    "UnknownOption",
                    "Unsupported option --" + optionName + " for " + CommandName + ".",
                    "Use only options documented for " + CommandName + ".");
            }
        }

        foreach (var option in mOptions)
        {
            if (option.Required && !HasNonEmptyValue(commandLine, option.Name))
            {
                throw CreateInputException(
                    "MissingOption",
                    "Option --" + option.Name + " is required for " + CommandName + ".",
                    "Provide --" + option.Name + " with a valid value.");
            }

            if (!commandLine.HasOption(option.Name))
            {
                continue;
            }

            ValidateValue(commandLine, option);
        }
    }

    /// <summary>
    /// 判断必填选项是否存在且不是空字符串。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="name">选项名。</param>
    /// <returns>存在非空值时返回 true。</returns>
    private static bool HasNonEmptyValue(CliCommandLine commandLine, string name)
    {
        return commandLine.HasOption(name)
            && !string.IsNullOrWhiteSpace(commandLine.GetOption(name, string.Empty));
    }

    /// <summary>
    /// 按 schema 类型解析选项值，避免业务命令静默回落默认值。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="option">选项描述。</param>
    private static void ValidateValue(CliCommandLine commandLine, CliOptionSpec option)
    {
        var rawValue = commandLine.GetOption(option.Name, string.Empty);
        switch (option.ValueKind)
        {
            case CliOptionValueKind.String:
                return;
            case CliOptionValueKind.Boolean:
                if (!bool.TryParse(rawValue, out _))
                {
                    throw InvalidValue(option, "true or false");
                }

                return;
            case CliOptionValueKind.Int32:
                if (!int.TryParse(rawValue, out var intValue))
                {
                    throw InvalidValue(option, "a 32-bit integer");
                }

                ValidateRange(option, intValue);
                return;
            case CliOptionValueKind.Int64:
                if (!long.TryParse(rawValue, out var longValue))
                {
                    throw InvalidValue(option, "a 64-bit integer");
                }

                ValidateRange(option, longValue);
                return;
            default:
                throw new InvalidOperationException("Unsupported CLI option value kind.");
        }
    }

    /// <summary>
    /// 校验数值选项的上下限。
    /// </summary>
    /// <param name="option">选项描述。</param>
    /// <param name="value">已解析的数值。</param>
    private static void ValidateRange(CliOptionSpec option, long value)
    {
        if (option.Minimum.HasValue && value < option.Minimum.Value
            || option.Maximum.HasValue && value > option.Maximum.Value)
        {
            var range = option.Minimum.HasValue && option.Maximum.HasValue
                ? option.Minimum.Value + ".." + option.Maximum.Value
                : option.Minimum.HasValue
                    ? ">=" + option.Minimum.Value
                    : "<=" + option.Maximum!.Value;
            throw CreateInputException(
                "OptionOutOfRange",
                "Option --" + option.Name + " must be " + range + ".",
                "Provide a value within the documented range.");
        }
    }

    /// <summary>
    /// 创建统一非法选项值错误。
    /// </summary>
    /// <param name="option">选项描述。</param>
    /// <param name="expected">期望类型说明。</param>
    /// <returns>协议异常。</returns>
    private static YokiFrameProtocolException InvalidValue(CliOptionSpec option, string expected)
    {
        return CreateInputException(
            "InvalidOptionValue",
            "Option --" + option.Name + " must be " + expected + ".",
            "Provide a value matching the option type.");
    }

    /// <summary>
    /// 创建统一命令输入异常。
    /// </summary>
    /// <param name="code">稳定错误码。</param>
    /// <param name="message">错误说明。</param>
    /// <param name="suggestion">修复建议。</param>
    /// <returns>协议异常。</returns>
    private static YokiFrameProtocolException CreateInputException(
        string code,
        string message,
        string suggestion)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            code,
            message,
            suggestion,
            Array.Empty<string>()));
    }
}

/// <summary>
/// 保存 CLI 所有公开命令的单一选项 schema，并在进入业务分派前执行校验。
/// </summary>
internal static class CliCommandSchemaRegistry
{
    private const long MIN_TIMEOUT_MS = CommandEnvelope.COMMAND_TIMEOUT_MIN_MS;
    private const long MAX_TIMEOUT_MS = CommandEnvelope.COMMAND_TIMEOUT_MAX_MS;
    private static readonly IReadOnlyList<CliCommandSchema> sSchemas = CreateSchemas();

    /// <summary>
    /// 校验命令及其选项；未知命令也在这里转换为稳定错误。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    public static void Validate(CliCommandLine commandLine)
    {
        var schema = sSchemas.FirstOrDefault(schema => schema.Matches(commandLine));
        if (schema == null)
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "UnknownCommand",
                "Unsupported command.",
                "Use one of the documented YokiFrame CLI commands.",
                Array.Empty<string>()));
        }

        schema.Validate(commandLine);
    }

    /// <summary>
    /// 创建当前 CLI 公开命令的 schema 表。
    /// </summary>
    /// <returns>按命令顺序排列的 schema。</returns>
    private static IReadOnlyList<CliCommandSchema> CreateSchemas()
    {
        CliOptionSpec project = new("project");
        CliOptionSpec engine = new("engine");
        CliOptionSpec source = new("source");
        CliOptionSpec timeout = new("timeout", CliOptionValueKind.Int32, minimum: MIN_TIMEOUT_MS, maximum: MAX_TIMEOUT_MS);
        CliOptionSpec strict = new("strict", CliOptionValueKind.Boolean);
        CliOptionSpec detail = new("detail");
        CliOptionSpec kit = new("kit");
        CliOptionSpec name = new("name");

        return new[]
        {
            Schema("harness status", new[] { "harness", "status" }, project),
            Schema("harness catalog", new[] { "harness", "catalog" }, project, engine, new("refresh-commands", CliOptionValueKind.Boolean), strict, timeout),
            Schema("project status", new[] { "project", "status" }, project, strict, detail),
            Schema("project refresh", new[] { "project", "refresh" }, project, strict, detail, new("package")),
            Schema("player build", new[] { "player", "build" }, project, new("engine", required: true), new("godot", required: true), new("preset", required: true), new("output", required: true), new("configuration")),
            Schema("installer plan", new[] { "installer", "plan" }, new("mode", required: true), new("target", required: true), source, new("git-url"), new("take-over", CliOptionValueKind.Boolean), new("repair-godot", CliOptionValueKind.Boolean), new("enable-godot", CliOptionValueKind.Boolean)),
            Schema("installer apply", new[] { "installer", "apply" }, new("mode", required: true), new("target", required: true), source, new("git-url"), new("take-over", CliOptionValueKind.Boolean), new("repair-godot", CliOptionValueKind.Boolean), new("enable-godot", CliOptionValueKind.Boolean)),
            Schema("engine list", new[] { "engine", "list" }, project),
            Schema("snapshot read", new[] { "snapshot", "read" }, project, engine, kit, name),
            Schema("bridge status", new[] { "bridge", "status" }, project, engine),
            Schema("doctor", new[] { "doctor" }, project, engine),
            Schema("command send", new[] { "command", "send" }, project, engine, kit, new("action"), new("payload"), source, timeout),
            Schema("command status", new[] { "command", "status" }, project, engine, new("request-id", required: true)),
            Schema("telemetry read", new[] { "telemetry", "read" }, project, engine, kit, name, new("maxPayload", CliOptionValueKind.Int32, minimum: 1), new("generation", CliOptionValueKind.Int64, minimum: 1)),
            Schema("fastchannel status", new[] { "fastchannel", "status" }, project, engine),
            Schema("audio index scan", new[] { "audio", "index", "scan" }, project, new("scan"), new("output"), new("manifest"), new("namespace"), new("class"), new("start-id", CliOptionValueKind.Int32, minimum: 0)),
            Schema("audio index generate", new[] { "audio", "index", "generate" }, project, new("scan"), new("output"), new("manifest"), new("namespace"), new("class"), new("start-id", CliOptionValueKind.Int32, minimum: 0)),
            Schema("spatialkit stats", new[] { "spatialkit", "stats" }, project, engine, source, timeout),
            Schema("spatialkit indexes", new[] { "spatialkit", "indexes" }, project, engine, source, timeout),
            Schema("spatialkit density", new[] { "spatialkit", "density" }, project, engine, source, timeout, new("index"), new("resolution", CliOptionValueKind.Int32, minimum: 4, maximum: 64)),
            Schema("spatialkit analyze", new[] { "spatialkit", "analyze" }, project, engine, source, timeout),
            Schema("localization search", new[] { "localization", "search" }, project, source, new("keyword"), new("missing-only", CliOptionValueKind.Boolean), new("limit", CliOptionValueKind.Int32, minimum: 1)),
            Schema("localization check", new[] { "localization", "check" }, project, source),
            Schema("localization add", new[] { "localization", "add" }, project, source, new("text-id", CliOptionValueKind.Int32), new("language", required: true), new("value", required: true), new("plural"), new("force", CliOptionValueKind.Boolean)),
            Schema("localization template generate", new[] { "localization", "template", "generate" }, project, new("languages"), new("force", CliOptionValueKind.Boolean), new("luban-config"), new("luban"), new("luban-workdir"), new("target")),
            Schema("localization preview", new[] { "localization", "preview" }, project, new("luban-config"), new("luban"), new("luban-workdir"), new("target"))
        };
    }

    /// <summary>
    /// 创建单个命令 schema。
    /// </summary>
    /// <param name="commandName">稳定命令名。</param>
    /// <param name="verbs">命令动词。</param>
    /// <param name="options">选项描述。</param>
    /// <returns>命令 schema。</returns>
    private static CliCommandSchema Schema(
        string commandName,
        IReadOnlyList<string> verbs,
        params CliOptionSpec[] options)
    {
        return new CliCommandSchema(commandName, verbs, options);
    }
}
