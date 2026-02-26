#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 序列化存档文档
    /// </summary>
    internal static class BuffKitDocSerialization
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "序列化存档",
                Description = "保存和恢复 Buff 状态。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "导出和恢复",
                        Code = @"// 导出存档数据
BuffContainerSaveData saveData = container.ToSaveData();

// 存入存档系统
var gameSave = SaveKit.CreateSaveData();
gameSave.RegisterModule(saveData);
await SaveKit.SaveUniTaskAsync(0, gameSave);

// 从存档恢复
var loadedSave = await SaveKit.LoadUniTaskAsync(0);
var buffSaveData = loadedSave.GetModule<BuffContainerSaveData>();
container.FromSaveData(buffSaveData);",
                        Explanation = "保存 Buff ID、剩余时间、堆叠数和免疫标签。"
                    }
                }
            };
        }
    }
}
#endif
