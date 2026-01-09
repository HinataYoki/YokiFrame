#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit SaveKit 集成文档
    /// </summary>
    internal static class LocalizationKitDocSaveKit
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "SaveKit 集成",
                Description = "持久化语言偏好设置。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "保存/加载语言设置",
                        Code = @"// 保存当前语言设置
var saveData = SaveKit.CreateSaveData();
LocalizationKitSaveIntegration.SaveLanguagePreference(saveData);
SaveKit.Save(slotId, saveData);

// 加载语言设置
var loadedData = SaveKit.Load(slotId);
if (LocalizationKitSaveIntegration.HasLanguagePreference(loadedData))
{
    LocalizationKitSaveIntegration.LoadLanguagePreference(loadedData);
}

// 清除语言设置
LocalizationKitSaveIntegration.ClearLanguagePreference(saveData);",
                        Explanation = "语言偏好会保存到 SaveData 中，下次启动时自动恢复。"
                    }
                }
            };
        }
    }
}
#endif
