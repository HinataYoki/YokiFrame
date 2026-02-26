#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 最佳实践文档
    /// </summary>
    internal static class LocalizationKitDocBestPractice
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "最佳实践",
                Description = "推荐的使用方式。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "定义文本 ID 常量",
                        Code = @"// 推荐：使用静态类定义文本 ID
public static class TextId
{
    // UI 文本
    public const int CONFIRM = 1001;
    public const int CANCEL = 1002;
    public const int TITLE = 1003;
    
    // 游戏文本
    public const int ITEM_NAME = 2001;
    public const int SKILL_DESC = 2002;
    
    // 系统消息
    public const int ERROR_NETWORK = 3001;
    public const int ERROR_SAVE = 3002;
}

// 使用
string text = LocalizationKit.Get(TextId.CONFIRM);",
                        Explanation = "使用 int 常量而非字符串，避免魔法值，便于重构和查找引用。"
                    },
                    new()
                    {
                        Title = "初始化流程",
                        Code = @"// 游戏启动时初始化
public class GameInitializer
{
    public void Initialize()
    {
        // 1. 设置数据提供者
        var provider = new JsonLocalizationProvider();
        provider.LoadFromResources();
        LocalizationKit.SetProvider(provider);
        
        // 2. 设置默认语言
        LocalizationKit.SetDefaultLanguage(LanguageId.ChineseSimplified);
        
        // 3. 从存档加载语言偏好
        var saveData = SaveKit.Load(0);
        if (saveData != null)
        {
            LocalizationKitSaveIntegration.LoadLanguagePreference(saveData);
        }
        
        // 4. 监听语言切换，保存偏好
        LocalizationKit.OnLanguageChanged += _ =>
        {
            var data = SaveKit.Load(0) ?? SaveKit.CreateSaveData();
            LocalizationKitSaveIntegration.SaveLanguagePreference(data);
            SaveKit.Save(0, data);
        };
    }
}"
                    }
                }
            };
        }
    }
}
#endif
