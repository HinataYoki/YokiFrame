#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 免疫与排斥文档
    /// </summary>
    internal static class BuffKitDocImmunity
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "免疫与排斥",
                Description = "控制 Buff 的添加规则。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "免疫系统",
                        Code = @"// 添加免疫标签
container.AddImmunity(100);

// 带标签 100 的 Buff 无法添加
var buff = BuffData.Create(1001, 10f).WithTags(100);
bool added = container.Add(buff);  // false

// 检查和移除免疫
bool isImmune = container.IsImmune(100);
container.RemoveImmunity(100);",
                        Explanation = "免疫系统用于实现无敌状态、控制免疫等。"
                    },
                    new()
                    {
                        Title = "排斥标签",
                        Code = @"// 添加此 Buff 时移除带指定标签的现有 Buff
var fireBuff = BuffData.Create(1001, 10f)
    .WithTags(100)
    .WithExclusionTags(200);

var iceBuff = BuffData.Create(1002, 10f)
    .WithTags(200);

container.Add(iceBuff);
container.Add(fireBuff);  // 自动移除冰系",
                        Explanation = "排斥标签用于实现互斥效果。"
                    }
                }
            };
        }
    }
}
#endif
