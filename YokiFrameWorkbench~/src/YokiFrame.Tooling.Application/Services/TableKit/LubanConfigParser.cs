using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using YokiFrame.Tooling.Application.Models.Luban;
using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.Luban;

namespace YokiFrame.Tooling.Application.Services.TableKit;

/// <summary>解析 Luban 配置并提取当前 target 的动态命名空间与 manager。</summary>
public sealed class LubanConfigParser
{
    /// <summary>读取配置文件并返回 TableKit 生成契约。</summary>
    /// <param name="options">Workbench 生成选项。</param>
    /// <returns>包含实际 topModule、manager 和输出目录的契约。</returns>
    public TableKitContract Parse(TableKitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!File.Exists(options.LubanConfigPath))
        {
            throw new FileNotFoundException("找不到 luban.conf。", options.LubanConfigPath);
        }

        JsonNode? root = JsonNode.Parse(File.ReadAllText(options.LubanConfigPath));
        JsonArray targets = root?["targets"] as JsonArray
            ?? throw new InvalidDataException("luban.conf 缺少 targets 数组。");
        JsonObject? target = targets
            .OfType<JsonObject>()
            .FirstOrDefault(candidate => string.IsNullOrWhiteSpace(options.TargetName)
                || string.Equals(candidate["name"]?.GetValue<string>(), options.TargetName, StringComparison.Ordinal));
        if (target == null)
        {
            throw new InvalidDataException("luban.conf 未找到目标 target: " + options.TargetName);
        }

        string topModule = ReadIdentifier(target, "topModule");
        string manager = ReadIdentifier(target, "manager");
        string assemblyName = ReadAssemblyName(options.AssemblyName);
        string outputCodeDirectory = ResolvePath(options.ProjectRoot, options.OutputCodeDir);
        string outputDataDirectory = ResolvePath(options.ProjectRoot, options.OutputDataDir);
        IReadOnlyList<TableKitExternalTypeMapping> externalTypeMappings = options.GenerateExternalTypeUtil
            ? ParseExternalTypeMappings(options, topModule)
            : Array.Empty<TableKitExternalTypeMapping>();
        return new TableKitContract
        {
            ConfigPath = Path.GetFullPath(options.LubanConfigPath),
            TargetName = target["name"]?.GetValue<string>() ?? options.TargetName,
            TopModule = topModule,
            Manager = manager,
            CodeTarget = options.CodeTarget,
            DataTarget = options.DataTarget,
            DataExtension = ResolveDataExtension(root, options.DataTarget),
            OutputCodeDirectory = outputCodeDirectory,
            OutputDataDirectory = outputDataDirectory,
            GenerateExternalTypeUtil = options.GenerateExternalTypeUtil,
            UseAssemblyDefinition = options.UseAssemblyDefinition,
            AssemblyName = assemblyName,
            ExternalTypeMappings = externalTypeMappings
        };
    }

    /// <summary>读取并校验 Luban 点分隔名称，避免生成无效 C# 命名空间或类型名。</summary>
    /// <param name="target">当前 Luban target。</param>
    /// <param name="propertyName">属性名。</param>
    /// <returns>去除首尾空白后的合法点分隔 C# 名称。</returns>
    private static string ReadIdentifier(JsonObject target, string propertyName)
    {
        string value = target[propertyName]?.GetValue<string>()?.Trim() ?? string.Empty;
        if (!IsQualifiedCSharpIdentifier(value))
        {
            throw new InvalidDataException("Luban target 的 " + propertyName + " 不是有效标识。");
        }

        return value;
    }

    /// <summary>校验生成程序集名，避免用户输入把 asmdef/csproj 写出 TableKit 根目录。</summary>
    /// <param name="assemblyName">用户配置的程序集名称。</param>
    /// <returns>去除首尾空白后的安全程序集名。</returns>
    private static string ReadAssemblyName(string assemblyName)
    {
        string value = assemblyName.Trim();
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith(".", StringComparison.Ordinal)
            || value.EndsWith(".", StringComparison.Ordinal)
            || value.Contains("..", StringComparison.Ordinal)
            || value.Any(character => !(char.IsLetterOrDigit(character) || character == '_' || character == '.')))
        {
            throw new InvalidDataException("TableKit 程序集名称不是有效标识。");
        }
        return value;
    }

    /// <summary>解析数据 target 的扩展名，支持 fileExt 配置和常见 Luban target。</summary>
    /// <param name="root">Luban 配置根节点。</param>
    /// <param name="dataTarget">数据 target 名称。</param>
    /// <returns>不带点号的扩展名。</returns>
    private static string ResolveDataExtension(JsonNode? root, string dataTarget)
    {
        JsonObject? dataTargets = root?["dataTargets"] as JsonObject;
        string? configured = dataTargets?[dataTarget]?["fileExt"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim().TrimStart('.');
        int separator = dataTarget.LastIndexOf('-');
        string suffix = separator >= 0 ? dataTarget[(separator + 1)..] : dataTarget;
        return suffix switch
        {
            "bin" => "bytes",
            "json" => "json",
            "xml" => "xml",
            "yaml" => "yaml",
            "lua" => "lua",
            "bson" => "bson",
            "msgpack" => "msgpack",
            _ => suffix
        };
    }

    /// <summary>读取中立 Luban 配置服务展开的 XML，并提取当前 target/codeTarget 的 bean mapper。</summary>
    /// <param name="options">Workbench 生成选项，用于定位 luban.conf 所在目录。</param>
    /// <param name="topModule">当前 target 的顶层命名空间。</param>
    /// <returns>按 XML 文件和 bean 顺序稳定返回的外部类型映射。</returns>
    private static IReadOnlyList<TableKitExternalTypeMapping> ParseExternalTypeMappings(
        TableKitOptions options,
        string topModule)
    {
        List<TableKitExternalTypeMapping> mappings = new();
        LubanConfiguration configuration = new LubanConfigurationReader().Read(Path.GetFullPath(options.LubanConfigPath));
        foreach (string definitionPath in configuration.DefinitionFiles)
        {
            mappings.AddRange(ParseDefinitionMappings(options, topModule, definitionPath));
        }

        return mappings;
    }

    /// <summary>解析单个定义 XML 中所有匹配当前生成目标的 bean mapper。</summary>
    /// <param name="options">当前 target 与 codeTarget。</param>
    /// <param name="topModule">Luban 生成代码的顶层命名空间。</param>
    /// <param name="definitionPath">定义 XML 绝对路径。</param>
    /// <returns>该文件中的外部类型映射。</returns>
    private static IReadOnlyList<TableKitExternalTypeMapping> ParseDefinitionMappings(
        TableKitOptions options,
        string topModule,
        string definitionPath)
    {
        List<TableKitExternalTypeMapping> mappings = new();
        XDocument document = XDocument.Load(definitionPath, LoadOptions.PreserveWhitespace);
        foreach (XElement bean in document.Descendants().Where(static element => element.Name.LocalName == "bean"))
        {
            string beanName = ReadXmlIdentifier(bean, "name", "bean");
            string sourceTypeName = BuildSourceTypeName(bean, topModule, beanName);
            IReadOnlyList<string> memberNames = ReadBeanMemberNames(bean);
            foreach (XElement mapper in bean.Elements().Where(static element => element.Name.LocalName == "mapper"))
            {
                TableKitExternalTypeMapping? mapping = ParseBeanMapper(
                    options, mapper, sourceTypeName, memberNames, topModule, definitionPath, beanName);
                if (mapping != null) mappings.Add(mapping);
            }
        }
        return mappings;
    }

    /// <summary>解析一条匹配的 bean mapper；未命中目标或未配置 constructor 时忽略。</summary>
    /// <param name="options">当前 target 与 codeTarget。</param>
    /// <param name="mapper">Luban bean mapper 节点。</param>
    /// <param name="sourceTypeName">生成后的 bean 完整类型名。</param>
    /// <param name="memberNames">生成后的 bean 成员名。</param>
    /// <param name="topModule">Luban 顶层命名空间。</param>
    /// <param name="definitionPath">来源 XML 路径。</param>
    /// <param name="beanName">来源 bean 名称。</param>
    /// <returns>可生成的 mapping；无需生成时返回 null。</returns>
    private static TableKitExternalTypeMapping? ParseBeanMapper(
        TableKitOptions options,
        XElement mapper,
        string sourceTypeName,
        IReadOnlyList<string> memberNames,
        string topModule,
        string definitionPath,
        string beanName)
    {
        if (!MatchesMapperTarget((string?)mapper.Attribute("target"), options.TargetName)
            || !MatchesMapperTarget((string?)mapper.Attribute("codeTarget"), options.CodeTarget)) return null;
        string? targetType = ReadMapperOption(mapper, "type")?.Trim();
        string? constructor = ReadMapperOption(mapper, "constructor")?.Trim();
        if (string.IsNullOrWhiteSpace(constructor)) return null;
        if (string.IsNullOrWhiteSpace(targetType))
        {
            throw new InvalidDataException(
                "Luban bean mapper " + beanName + " 配置了 constructor，但缺少 type option。文件: " + definitionPath);
        }
        if (memberNames.Count == 0)
        {
            throw new InvalidDataException(
                "Luban bean mapper " + beanName + " 配置了 constructor，但没有可传入的 var 字段。文件: " + definitionPath);
        }

        (string helperNamespace, string helperTypeName, string helperMethodName) =
            ParseConstructor(constructor, topModule, definitionPath, beanName);
        return new TableKitExternalTypeMapping
        {
            SourceTypeName = sourceTypeName,
            TargetTypeName = targetType,
            HelperNamespace = helperNamespace,
            HelperTypeName = helperTypeName,
            HelperMethodName = helperMethodName,
            MemberNames = memberNames
        };
    }

    /// <summary>组合 topModule、XML module 路径和 bean 名称得到生成类型名。</summary>
    /// <param name="bean">当前 bean 节点。</param>
    /// <param name="topModule">Luban 顶层命名空间。</param>
    /// <param name="beanName">已校验的 bean 名称。</param>
    /// <returns>带 global 前缀的生成类型名。</returns>
    private static string BuildSourceTypeName(XElement bean, string topModule, string beanName)
    {
        string modulePrefix = string.Join(
            ".",
            bean.Ancestors()
                .Where(static element => element.Name.LocalName == "module")
                .Reverse()
                .Select(static element => new
                {
                    Element = element,
                    Name = ((string?)element.Attribute("name"))?.Trim()
                })
                .Where(static item => !string.IsNullOrWhiteSpace(item.Name))
                .Select(static item => ReadXmlIdentifier(item.Element, "name", "module")));
        return "global::" + topModule
            + (string.IsNullOrWhiteSpace(modulePrefix) ? string.Empty : "." + modulePrefix)
            + "." + beanName;
    }

    /// <summary>读取 bean var，并转换为 Luban C# 生成器实际使用的成员名。</summary>
    /// <param name="bean">当前 bean 节点。</param>
    /// <returns>保持字段声明顺序的 C# 成员名。</returns>
    private static IReadOnlyList<string> ReadBeanMemberNames(XElement bean)
    {
        return bean.Elements()
            .Where(static element => element.Name.LocalName == "var")
            .Select(element => ToGeneratedMemberName(ReadXmlIdentifier(element, "name", "var")))
            .ToArray();
    }

    /// <summary>判断 mapper 的逗号分隔 target/codeTarget 是否包含当前值。</summary>
    /// <param name="configuredTargets">XML mapper 属性值。</param>
    /// <param name="currentTarget">当前 Luban 命令行 target 或 codeTarget。</param>
    /// <returns>包含当前值或通配符时返回 true。</returns>
    private static bool MatchesMapperTarget(string? configuredTargets, string currentTarget)
    {
        if (string.IsNullOrWhiteSpace(configuredTargets)) return false;
        return configuredTargets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => value == "*" || string.Equals(value, currentTarget, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>读取 mapper 下指定名称的 option value。</summary>
    /// <param name="mapper">当前 bean mapper。</param>
    /// <param name="optionName">option 名称。</param>
    /// <returns>配置值，不存在时返回 null。</returns>
    private static string? ReadMapperOption(XElement mapper, string optionName)
    {
        return mapper.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "option"
                && string.Equals((string?)element.Attribute("name"), optionName, StringComparison.Ordinal))
            ?.Attribute("value")?.Value;
    }

    /// <summary>解析并校验 XML 标识，避免生成源码时出现无效类型或成员名。</summary>
    /// <param name="element">包含标识属性的 XML 元素。</param>
    /// <param name="attributeName">标识属性名。</param>
    /// <param name="elementName">错误信息中的元素名。</param>
    /// <returns>去除首尾空白的标识。</returns>
    private static string ReadXmlIdentifier(XElement element, string attributeName, string elementName)
    {
        string value = ((string?)element.Attribute(attributeName))?.Trim() ?? string.Empty;
        if (!IsCSharpIdentifier(value))
        {
            throw new InvalidDataException("Luban " + elementName + " 的 " + attributeName + " 不是有效 C# 标识: " + value);
        }
        return value;
    }

    /// <summary>解析 Luban constructor 的 helper 类型和方法名。</summary>
    /// <param name="constructor">例如 ConfiguredTypeMapper.CreateVector2。</param>
    /// <param name="topModule">当前 target 的顶层命名空间。</param>
    /// <param name="definitionPath">来源 XML 路径。</param>
    /// <param name="beanName">来源 bean 名称。</param>
    /// <returns>helper 命名空间、类型名和方法名。</returns>
    private static (string HelperNamespace, string HelperTypeName, string HelperMethodName) ParseConstructor(
        string constructor,
        string topModule,
        string definitionPath,
        string beanName)
    {
        int separator = constructor.LastIndexOf('.');
        if (separator <= 0 || separator == constructor.Length - 1)
        {
            throw new InvalidDataException(
                "Luban bean mapper " + beanName + " 的 constructor 必须是 Type.Method: " + constructor + "。文件: " + definitionPath);
        }

        string owner = constructor[..separator].Trim();
        if (owner.StartsWith("global::", StringComparison.Ordinal)) owner = owner[8..];
        string methodName = constructor[(separator + 1)..].Trim();
        string[] ownerParts = owner.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ownerParts.Length == 0 || !ownerParts.All(IsCSharpIdentifier) || !IsCSharpIdentifier(methodName))
        {
            throw new InvalidDataException("Luban bean mapper 的 constructor 不是有效 C# 成员表达式: " + constructor);
        }

        string helperTypeName = ownerParts[^1];
        string helperNamespace = ownerParts.Length == 1
            ? topModule
            : string.Join('.', ownerParts[..^1]);
        return (helperNamespace, helperTypeName, methodName);
    }

    /// <summary>判断单段 C# 标识符是否合法。</summary>
    /// <param name="value">待检查文本。</param>
    /// <returns>符合 C# 标识符基本规则时返回 true。</returns>
    private static bool IsCSharpIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !(char.IsLetter(value[0]) || value[0] == '_')) return false;
        return value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    /// <summary>判断点分隔的命名空间或类型名称中每一段都符合 C# 标识符基本规则。</summary>
    /// <param name="value">待校验的点分隔名称。</param>
    /// <returns>所有名称段均合法时返回 true。</returns>
    private static bool IsQualifiedCSharpIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Split('.', StringSplitOptions.None).All(IsCSharpIdentifier);
    }

    /// <summary>把 Luban var 名称转换为 cs-bin 生成代码使用的首字母大写成员名。</summary>
    /// <param name="value">XML 中的字段名。</param>
    /// <returns>生成 C# 类型中的成员名。</returns>
    private static string ToGeneratedMemberName(string value)
    {
        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    /// <summary>解析项目根相对路径并确保结果仍位于项目根内。</summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="path">绝对或相对路径。</param>
    /// <returns>规范化绝对路径。</returns>
    private static string ResolvePath(string projectRoot, string path)
    {
        string root = Path.GetFullPath(projectRoot);
        string full = Path.GetFullPath(Path.IsPathFullyQualified(path) ? path : Path.Combine(root, path));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!full.StartsWith(prefix, comparison)) throw new InvalidDataException("TableKit 输出路径越出项目根。");
        return full;
    }
}
