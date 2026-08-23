using System.Text.RegularExpressions;
using Xunit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 防止 zh/en 两份 i18n 资源字典发生键集漂移。
/// 历史缺陷：多轮手工同步漏更英文表，导致英文界面残留中文。
/// </summary>
public sealed class WorkbenchI18nDictionarySyncTests
{
    /// <summary>断言 zh/en 两份资源字典的键集合完全一致。</summary>
    [Fact]
    public void DictionaryKeys_StayInSyncBetweenLanguages()
    {
        string zhAxaml = WorkbenchContractTestFiles.ReadSource(
            "Resources", "I18n", "Strings.zh-CN.axaml");
        string enAxaml = WorkbenchContractTestFiles.ReadSource(
            "Resources", "I18n", "Strings.en-US.axaml");

        var zhKeys = ExtractAxamlKeys(zhAxaml);
        var enKeys = ExtractAxamlKeys(enAxaml);

        var missingInEn = zhKeys.Except(enKeys).OrderBy(static key => key).ToArray();
        Assert.True(
            missingInEn.Length == 0,
            "en-US 字典缺失以下键（英文界面会残留中文）: " + string.Join(", ", missingInEn));

        var missingInZh = enKeys.Except(zhKeys).OrderBy(static key => key).ToArray();
        Assert.True(
            missingInZh.Length == 0,
            "zh-CN 字典缺失以下键: " + string.Join(", ", missingInZh));
    }

    /// <summary>从资源字典 XAML 源码提取全部以 String. 开头的资源键。</summary>
    private static IReadOnlySet<string> ExtractAxamlKeys(string axaml)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(axaml, "x:Key=\"([^\"]+)\""))
        {
            if (match.Groups[1].Value.StartsWith("String.", StringComparison.Ordinal))
                keys.Add(match.Groups[1].Value);
        }
        return keys;
    }
}
