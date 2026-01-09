#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 与 SaveKit 集成文档
    /// </summary>
    internal static class ArchitectureDocSaveKit
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "与 SaveKit 集成",
                Description = "Architecture 中的 IModel 可以通过 SaveKit 自动收集和恢复数据。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "存档集成示例",
                        Code = @"// 保存所有 Model 数据
var saveData = SaveKit.CreateSaveData();
SaveKit.CollectFromArchitecture<GameArchitecture>(saveData);
SaveKit.Save(0, saveData);

// 加载并恢复 Model 数据
var loadedData = SaveKit.Load(0);
if (loadedData != null)
{
    SaveKit.ApplyToArchitecture<GameArchitecture>(loadedData);
}",
                        Explanation = "SaveKit 会自动遍历 Architecture 中所有实现 IModel 的服务进行序列化/反序列化。"
                    }
                }
            };
        }
    }
}
#endif
