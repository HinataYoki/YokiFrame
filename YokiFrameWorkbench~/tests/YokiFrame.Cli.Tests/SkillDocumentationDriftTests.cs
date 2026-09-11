using System.Text.RegularExpressions;
using YokiFrame.Cli;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 守护 Skill 文档与 CLI schema 的一致性：命令事实只有一个来源，文档漂移必须让测试失败。
/// </summary>
public sealed class SkillDocumentationDriftTests
{
    /// <summary>
    /// 验证 commands.md 记录的命令面与 CliCommandSchemaRegistry 完全一致。
    /// </summary>
    [Fact]
    public void CommandsReferenceCoversEverySchemaCommand()
    {
        var document = ReadCommandsReference();

        foreach (var schema in CliCommandSchemaRegistry.Schemas)
        {
            var verb = string.Join(' ', schema.Verbs);
            Assert.True(
                document.Contains(verb, StringComparison.Ordinal),
                "commands.md 缺少命令：" + verb);
        }
    }

    /// <summary>
    /// 验证声明了 --dry-run 的命令在 commands.md 中都有记录，避免新增写入命令后 AI 无从发现 dry-run。
    /// </summary>
    [Fact]
    public void CommandsReferenceDocumentsEveryDryRunCommand()
    {
        var document = ReadCommandsReference();
        var dryRunCommands = CliCommandSchemaRegistry.Schemas
            .Where(static schema => schema.HasOption("dry-run"))
            .Select(static schema => schema.Verbs[0])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(dryRunCommands);
        foreach (var verb in dryRunCommands)
        {
            Assert.True(
                document.Contains(verb, StringComparison.Ordinal),
                "commands.md 缺少带有 --dry-run 的命令族：" + verb);
        }

        Assert.Contains("--dry-run", document, StringComparison.Ordinal);
    }

    /// <summary>
    /// 反向校验：Skill 文档里出现的每个 `yoki` 调用都必须对应一个真实 schema 命令。
    /// 只做正向覆盖检查会让文档写出不存在的命令而不被发现。
    /// </summary>
    [Fact]
    public void SkillDocumentsDoNotInventCliCommands()
    {
        var packageRoot = CliTestHelpers.GetPackageRoot();
        var skillRoot = Path.Combine(packageRoot, "Core", "Editor", "Skills");
        var verbs = CliCommandSchemaRegistry.Schemas.Select(static schema => schema.Verbs).ToArray();

        List<string> invented = new();
        foreach (var path in Directory.EnumerateFiles(skillRoot, "*.md", SearchOption.AllDirectories))
        {
            var lineNumber = 0;
            foreach (var line in File.ReadAllLines(path))
            {
                lineNumber++;
                foreach (var invocation in ExtractCliInvocations(line))
                {
                    if (!MatchesAnySchema(invocation, verbs))
                    {
                        invented.Add(Path.GetFileName(path) + ":" + lineNumber + " -> " + invocation);
                    }
                }
            }
        }

        Assert.True(
            invented.Count == 0,
            "Skill 文档提到不存在的 CLI 命令: " + string.Join("; ", invented));
    }

    /// <summary>提取一行中 `$YOKI` 或 `yoki` 之后的命令动词片段，忽略选项与占位符。</summary>
    /// <param name="line">待扫描的 Markdown 行。</param>
    /// <returns>命令动词候选；没有 CLI 调用时为空。</returns>
    private static IEnumerable<string> ExtractCliInvocations(string line)
    {
        foreach (Match match in Regex.Matches(line, @"(?:\$YOKI|yoki)\s+([a-z][a-z0-9]*(?:\s+[a-z][a-z0-9]*){0,2})"))
        {
            yield return match.Groups[1].Value;
        }
    }

    /// <summary>
    /// 判断候选动词片段是否以任一 schema 命令开头。
    /// 候选可能吞掉参数，因此从最长前缀开始逐级回退，任一前缀命中即视为合法。
    /// </summary>
    /// <param name="invocation">候选命令动词片段。</param>
    /// <param name="verbs">全部 schema 动词序列。</param>
    /// <returns>匹配任一 schema 命令时返回 true。</returns>
    private static bool MatchesAnySchema(string invocation, IReadOnlyList<IReadOnlyList<string>> verbs)
    {
        foreach (var schemaVerbs in verbs)
        {
            var verb = string.Join(' ', schemaVerbs);
            if (string.Equals(invocation, verb, StringComparison.Ordinal)
                || invocation.StartsWith(verb + " ", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 验证 commands.md 不再记录已经移除的 --strict，避免 AI 按旧文档构造参数。
    /// </summary>
    [Fact]
    public void CommandsReferenceDoesNotAdvertiseRemovedStrictOption()
    {
        var document = ReadCommandsReference();

        Assert.DoesNotContain("project status --strict", document, StringComparison.Ordinal);
        Assert.DoesNotContain("project refresh --strict", document, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 kit-index 覆盖当前所有已实现 Kit 的能力索引，避免新增 Kit 后文档静默落后。
    /// </summary>
    [Fact]
    public void KitIndexCoversEveryShippedKit()
    {
        var packageRoot = CliTestHelpers.GetPackageRoot();
        var kitIndex = File.ReadAllText(Path.Combine(
            packageRoot,
            "Core",
            "Editor",
            "Skills",
            "yokiframe",
            "references",
            "kit-index.md"));

        var toolsRoot = Path.Combine(packageRoot, "Tools");
        foreach (var directory in Directory.EnumerateDirectories(toolsRoot))
        {
            var kitName = Path.GetFileName(directory);
            Assert.True(
                kitIndex.Contains(kitName, StringComparison.Ordinal),
                "kit-index.md 缺少 Kit：" + kitName);
        }
    }

    /// <summary>
    /// 读取随包分发的 CLI 命令参考文档。
    /// </summary>
    /// <returns>commands.md 全文。</returns>
    private static string ReadCommandsReference()
    {
        return File.ReadAllText(Path.Combine(
            CliTestHelpers.GetPackageRoot(),
            "Core",
            "Editor",
            "Skills",
            "yokiframe",
            "references",
            "cli-commands.md"));
    }
}
