#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SaveKit Architecture 集成文档
    /// </summary>
    internal static class SaveKitDocArchitecture
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "Architecture 集成",
                Description = "与 YokiFrame Architecture 的 IModel 集成。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "收集与应用",
                        Code = @"// 从 Architecture 收集所有 IModel 数据
var saveData = SaveKit.CreateSaveData();
SaveKit.CollectFromArchitecture<GameArchitecture>(saveData);
await SaveKit.SaveUniTaskAsync(slotId, saveData);

// 将存档数据应用到 Architecture
var loadedData = await SaveKit.LoadUniTaskAsync(slotId);
if (loadedData != null)
{
    SaveKit.ApplyToArchitecture<GameArchitecture>(loadedData);
}",
                        Explanation = "自动序列化/反序列化所有注册的 IModel 服务。"
                    }
                }
            };
        }
    }
}
#endif
