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
                Description = "与 YokiFrame Architecture 的 IModel 集成，一键收集和应用所有 Model 数据。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "收集与保存",
                        Code = @"// 从 Architecture 一键注册所有 IModel（等价于对每个 Model 手动 RegisterModule）
var saveData = SaveKit.CreateSaveData();
SaveKit.CollectFromArchitecture<GameArchitecture>(saveData);
await SaveKit.SaveUniTaskAsync(slotId, saveData);

// 注意：CollectFromArchitecture 注册的是对象引用
// 后续可以直接修改 Model 数据然后再次保存，无需重新收集",
                        Explanation = "CollectFromArchitecture 会自动将 Architecture 中所有 IModel 注册到 SaveData。" +
                                     "注册的是引用而非拷贝，后续修改会自动反映到下次保存中。"
                    },
                    new()
                    {
                        Title = "加载与应用",
                        Code = @"// 加载存档数据
var loadedData = await SaveKit.LoadUniTaskAsync(slotId);
if (loadedData != null)
{
    // 一键将数据应用回 Architecture 的所有 IModel
    SaveKit.ApplyToArchitecture<GameArchitecture>(loadedData);
}

// 也兼容手动 RegisterModule 保存的数据
// 无论是通过 CollectFromArchitecture 还是手动注册的数据
// ApplyToArchitecture 都能正确应用",
                        Explanation = "ApplyToArchitecture 通过 JsonUtility.FromJsonOverwrite 直接覆盖 Architecture 中的 Model 数据，" +
                                     "兼容 CollectFromArchitecture 和手动 RegisterModule 两种方式保存的数据。"
                    }
                }
            };
        }
    }
}
#endif
