#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SaveKit 自动保存文档
    /// </summary>
    internal static class SaveKitDocAutoSave
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "自动保存",
                Description = "定时自动保存，序列化在线程池执行，不影响游戏性能。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "启用自动保存",
                        Code = @"// 游戏初始化时
var saveData = SaveKit.CreateSaveData();
saveData.RegisterModule(playerData);
saveData.RegisterModule(inventoryData);

// 启用 UniTask 自动保存（推荐）
SaveKit.EnableAutoSaveUniTask(
    slotId: 0,
    data: saveData,
    intervalSeconds: 60f,
    onBeforeSave: () =>
    {
        // 保存前回调（可选）
        Debug.Log(""即将自动保存..."");
    }
);

// 禁用自动保存
SaveKit.DisableAutoSave();

// 检查状态
if (SaveKit.IsAutoSaveEnabled)
{
    Debug.Log(""自动保存已启用"");
}",
                        Explanation = "自动保存时，序列化在线程池执行，主线程只做轻量准备工作。"
                    }
                }
            };
        }
    }
}
#endif
