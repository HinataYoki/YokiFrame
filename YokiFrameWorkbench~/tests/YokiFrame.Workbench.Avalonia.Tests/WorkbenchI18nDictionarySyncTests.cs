using System.Text.RegularExpressions;
using Xunit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 防止 zh/en 两份 i18n 字符串表发生键集漂移。
/// 历史缺陷：多轮手工同步漏更英文表，导致英文界面残留中文。
/// 字符串表是唯一事实源，按语言拆分为 WorkbenchI18nService.Strings.*.cs。
/// </summary>
public sealed class WorkbenchI18nDictionarySyncTests
{
    /// <summary>断言 zh/en 两份字符串表的键集合完全一致。</summary>
    [Fact]
    public void DictionaryKeys_StayInSyncBetweenLanguages()
    {
        var zhSource = WorkbenchContractTestFiles.ReadSource(
            "Services", "WorkbenchI18nService.Strings.Zh.cs");
        var enSource = WorkbenchContractTestFiles.ReadSource(
            "Services", "WorkbenchI18nService.Strings.En.cs");

        var zhKeys = ExtractKeys(zhSource);
        var enKeys = ExtractKeys(enSource);

        Assert.NotEmpty(zhKeys);
        Assert.NotEmpty(enKeys);

        var missingInEn = zhKeys.Except(enKeys).OrderBy(static key => key).ToArray();
        Assert.True(
            missingInEn.Length == 0,
            "en-US 字符串表缺失以下键（英文界面会残留中文）: " + string.Join(", ", missingInEn));

        var missingInZh = enKeys.Except(zhKeys).OrderBy(static key => key).ToArray();
        Assert.True(
            missingInZh.Length == 0,
            "zh-CN 字符串表缺失以下键: " + string.Join(", ", missingInZh));
    }

    /// <summary>从字符串表 C# 源码提取全部以 String. 开头的资源键。</summary>
    /// <param name="source">字符串表源码文本。</param>
    /// <returns>去重后的资源键集合。</returns>
    private static IReadOnlySet<string> ExtractKeys(string source)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(source, "\\[\"(String\\.[^\"]+)\"\\]\\s*="))
        {
            keys.Add(match.Groups[1].Value);
        }

        return keys;
    }
}
