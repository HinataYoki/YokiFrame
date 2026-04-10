#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 快速开始文档
    /// </summary>
    internal static class LocalizationKitDocQuickStart
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "快速开始",
                Description = "LocalizationKit 提供简洁的本地化 API。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "初始化",
                        Code = @"// 创建 JSON 数据提供者
var provider = new JsonLocalizationProvider();
provider.LoadFromResources(); // 从 Resources/Localization 加载

// 设置提供者
LocalizationKit.SetProvider(provider);

// 设置默认语言（用于 fallback）
LocalizationKit.SetDefaultLanguage(LanguageId.ChineseSimplified);",
                        Explanation = "初始化时设置数据提供者，支持 JSON 文件或 TableKit 配置表。"
                    },
                    new()
                    {
                        Title = "获取文本",
                        Code = @"// 使用 int ID 获取文本（推荐）
string text = LocalizationKit.Get(TextId.CONFIRM); // ""确认""

// 带参数的文本
string welcome = LocalizationKit.Get(TextId.WELCOME, ""玩家名"");
// 模板: ""欢迎，{0}！"" -> ""欢迎，玩家名！""

// 命名参数
var args = new Dictionary<string, object>
{
    { ""name"", ""Alice"" },
    { ""count"", 100 }
};
string msg = LocalizationKit.Get(TextId.REWARD_MSG, args);
// 模板: ""{name} 获得了 {count} 金币"" -> ""Alice 获得了 100 金币""",
                        Explanation = "使用 int ID 而非字符串作为 key，避免魔法字符串。推荐定义 TextId 常量类或枚举。"
                    }
                }
            };
        }
    }
}
#endif
