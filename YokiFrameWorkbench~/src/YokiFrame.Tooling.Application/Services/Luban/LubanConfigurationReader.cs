using System.Text.Json.Nodes;
using YokiFrame.Tooling.Application.Models.Luban;

namespace YokiFrame.Tooling.Application.Services.Luban;

/// <summary>读取 luban.conf 的跨 Kit 最小投影，不承担任何 Kit 的代码生成契约。</summary>
public sealed class LubanConfigurationReader
{
    /// <summary>读取配置、目标、数据目录和 schemaFiles 的规范化路径。</summary>
    /// <param name="configPath">luban.conf 的绝对或当前进程相对路径。</param>
    /// <returns>供 TableKit 和其它 Tooling 用例共享的配置投影。</returns>
    public LubanConfiguration Read(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("luban.conf 路径不能为空。", nameof(configPath));
        }

        string fullConfigPath = Path.GetFullPath(configPath);
        if (!File.Exists(fullConfigPath))
        {
            throw new FileNotFoundException("找不到 luban.conf。", fullConfigPath);
        }

        JsonNode? root = JsonNode.Parse(File.ReadAllText(fullConfigPath));
        if (root is not JsonObject config)
        {
            throw new InvalidDataException("luban.conf 根节点必须是对象。");
        }

        string configDirectory = Path.GetDirectoryName(fullConfigPath)!;
        return new LubanConfiguration
        {
            ConfigPath = fullConfigPath,
            ConfigDirectory = configDirectory,
            DataDirectory = ResolveDataDirectory(config, configDirectory),
            TargetNames = ReadTargetNames(config),
            SchemaSources = ReadSchemaSources(config, configDirectory),
            DefinitionFiles = ResolveDefinitionFiles(config, configDirectory)
        };
    }

    /// <summary>从可选 dataDir 解析数据目录；需要作者表目录的领域服务自行校验该字段。</summary>
    /// <param name="config">已解析的 luban.conf 根节点。</param>
    /// <param name="configDirectory">luban.conf 所在目录。</param>
    /// <returns>规范化后的数据目录；配置未声明时返回空文本。</returns>
    private static string ResolveDataDirectory(JsonObject config, string configDirectory)
    {
        string dataDirectory = config["dataDir"]?.GetValue<string>()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            return string.Empty;
        }

        return Path.GetFullPath(Path.IsPathFullyQualified(dataDirectory)
            ? dataDirectory
            : Path.Combine(configDirectory, dataDirectory));
    }

    /// <summary>读取去重后的 Luban target 名称，供调用方选择验证或预览目标。</summary>
    /// <param name="config">已解析的 luban.conf 根节点。</param>
    /// <returns>按配置顺序排列的稳定 target 名称。</returns>
    private static IReadOnlyList<string> ReadTargetNames(JsonObject config)
    {
        List<string> targetNames = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        if (config["targets"] is not JsonArray targets)
        {
            return targetNames;
        }

        foreach (JsonObject target in targets.OfType<JsonObject>())
        {
            string name = target["name"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (name.Length > 0 && seen.Add(name))
            {
                targetNames.Add(name);
            }
        }

        return targetNames;
    }

    /// <summary>读取 schemaFiles 的原始类型和规范化路径，保留目录来源以支持模板注册判断。</summary>
    /// <param name="config">已解析的 luban.conf 根节点。</param>
    /// <param name="configDirectory">luban.conf 所在目录。</param>
    /// <returns>按 luban.conf 声明顺序排列的 schema 来源。</returns>
    private static IReadOnlyList<LubanSchemaSource> ReadSchemaSources(JsonObject config, string configDirectory)
    {
        List<LubanSchemaSource> sources = new();
        if (config["schemaFiles"] is not JsonArray schemaFiles)
        {
            return sources;
        }

        foreach (JsonObject source in schemaFiles.OfType<JsonObject>())
        {
            string fileName = source["fileName"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (fileName.Length == 0)
            {
                continue;
            }

            string fullPath = Path.GetFullPath(Path.IsPathFullyQualified(fileName)
                ? fileName
                : Path.Combine(configDirectory, fileName));
            sources.Add(new LubanSchemaSource
            {
                FileName = fileName,
                Type = source["type"]?.GetValue<string>()?.Trim() ?? string.Empty,
                FullPath = fullPath,
                IsDirectory = Directory.Exists(fullPath)
            });
        }

        return sources;
    }

    /// <summary>展开 schemaFiles 中存在的 XML 文件和目录，供 TableKit 的 mapper 解析复用。</summary>
    /// <param name="config">已解析的 luban.conf 根节点。</param>
    /// <param name="configDirectory">luban.conf 所在目录。</param>
    /// <returns>去重、排序后的 XML 定义文件绝对路径。</returns>
    private static IReadOnlyList<string> ResolveDefinitionFiles(JsonObject config, string configDirectory)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (LubanSchemaSource source in ReadSchemaSources(config, configDirectory))
        {
            if (File.Exists(source.FullPath)
                && string.Equals(Path.GetExtension(source.FullPath), ".xml", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(source.FullPath);
                continue;
            }

            if (!source.IsDirectory)
            {
                continue;
            }

            foreach (string definitionPath in Directory.EnumerateFiles(source.FullPath, "*.xml", SearchOption.AllDirectories)
                         .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(Path.GetFullPath(definitionPath));
            }
        }

        return paths.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
