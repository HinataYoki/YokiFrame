#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 数据提供者文档
    /// </summary>
    internal static class LocalizationKitDocProvider
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "数据提供者",
                Description = "支持多种数据源。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "JSON 提供者",
                        Code = @"// 从 Resources 加载
var provider = new JsonLocalizationProvider(
    pathPattern: ""Localization/localization"",
    useResources: true
);
provider.LoadFromResources();

// 从 JSON 字符串加载
var json = @""{
    """"languages"""": [
        { """"id"""": 0, """"displayNameTextId"""": 1 },
        { """"id"""": 2, """"displayNameTextId"""": 2 }
    ],
    """"texts"""": [
        { """"id"""": 1001, """"values"""": [""""确认"""", """"Confirm""""] }
    ]
}"";
provider.LoadFromJson(json);

// 手动添加文本（用于测试）
provider.AddText(LanguageId.English, 1001, ""Hello"");
provider.AddPluralText(LanguageId.English, 1002, PluralCategory.One, ""1 item"");
provider.AddPluralText(LanguageId.English, 1002, PluralCategory.Other, ""{0} items"");"
                    },
                    new()
                    {
                        Title = "TableKit 提供者",
                        Code = @"// 使用 TableKit 配置表（需要 YOKIFRAME_LUBAN_SUPPORT）
var provider = new TableKitLocalizationProvider();
LocalizationKit.SetProvider(provider);

// TableKit 提供者会自动从 Luban 生成的配置表读取数据",
                        Explanation = "需要先通过 TableKit 生成本地化配置表代码。"
                    }
                }
            };
        }
    }
}
#endif
