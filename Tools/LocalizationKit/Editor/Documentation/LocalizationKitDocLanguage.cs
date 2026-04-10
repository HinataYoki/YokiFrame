#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 语言切换文档
    /// </summary>
    internal static class LocalizationKitDocLanguage
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "语言切换",
                Description = "运行时切换语言，自动刷新所有绑定的 UI。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "切换语言",
                        Code = @"// 切换到英文
bool success = LocalizationKit.SetLanguage(LanguageId.English);

// 获取当前语言
LanguageId current = LocalizationKit.GetCurrentLanguage();

// 获取支持的语言列表
var languages = LocalizationKit.GetAvailableLanguages();

// 监听语言切换事件
LocalizationKit.OnLanguageChanged += newLang =>
{
    Debug.Log($""语言已切换到: {newLang}"");
};",
                        Explanation = "切换语言时会自动清除缓存并通知所有绑定器刷新。"
                    },
                    new()
                    {
                        Title = "语言信息",
                        Code = @"// 获取语言详细信息
var info = LocalizationKit.GetLanguageInfo(LanguageId.English);
// info.DisplayNameTextId -> 显示名称的文本ID
// info.NativeNameTextId -> 原生名称的文本ID
// info.IconSpriteId -> 图标资源ID

// 检查语言是否已加载
if (LocalizationKit.IsLanguageLoaded(LanguageId.Japanese))
{
    // 日语数据已加载
}"
                    }
                }
            };
        }
    }
}
#endif
