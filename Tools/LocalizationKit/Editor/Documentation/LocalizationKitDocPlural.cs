#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 复数形式文档
    /// </summary>
    internal static class LocalizationKitDocPlural
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "复数形式",
                Description = "根据数量自动选择正确的复数形式。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "复数文本",
                        Code = @"// 英文复数
// One: ""1 item""
// Other: ""{0} items""
string text = LocalizationKit.GetPlural(TextId.ITEM_COUNT, 1);  // ""1 item""
string text2 = LocalizationKit.GetPlural(TextId.ITEM_COUNT, 5); // ""5 items""

// 中文（无复数变化）
// Other: ""{0} 个物品""
string zhText = LocalizationKit.GetPlural(TextId.ITEM_COUNT, 1);  // ""1 个物品""
string zhText2 = LocalizationKit.GetPlural(TextId.ITEM_COUNT, 5); // ""5 个物品""

// 带额外参数
string msg = LocalizationKit.GetPlural(TextId.REWARD, count, ""金币"");
// 模板: ""{0} 个{1}"" -> ""5 个金币""",
                        Explanation = "遵循 ICU 复数规则，支持 Zero/One/Two/Few/Many/Other 六种类别。"
                    }
                }
            };
        }
    }
}
#endif
