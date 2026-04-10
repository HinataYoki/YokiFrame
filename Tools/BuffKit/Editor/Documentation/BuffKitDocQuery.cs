#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 查询与移除文档
    /// </summary>
    internal static class BuffKitDocQuery
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "查询与移除",
                Description = "丰富的查询和移除 API。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "查询 Buff",
                        Code = @"bool hasBuff = container.Has(1001);
BuffInstance instance = container.Get(1001);

var results = new List<BuffInstance>();
container.GetAll(1001, results);
container.GetByTag(100, results);

int stacks = container.GetStackCount(1001);
int count = container.Count;",
                        Explanation = "查询方法支持按 ID 和标签查询。"
                    },
                    new()
                    {
                        Title = "移除 Buff",
                        Code = @"container.Remove(1001);
container.RemoveInstance(instance);
int removed = container.RemoveByTag(100);
container.Clear();",
                        Explanation = "移除方法支持按 ID、实例和标签移除。"
                    }
                }
            };
        }
    }
}
#endif
