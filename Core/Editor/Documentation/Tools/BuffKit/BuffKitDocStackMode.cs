#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 堆叠模式文档
    /// </summary>
    internal static class BuffKitDocStackMode
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "堆叠模式",
                Description = "三种堆叠模式满足不同需求。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "Independent - 独立模式",
                        Code = @"// 每次添加创建新实例
BuffData.Create(1001, 10f, 99, StackMode.Independent);

var results = new List<BuffInstance>();
container.GetAll(1001, results);",
                        Explanation = "独立模式下每次添加都会创建新的 Buff 实例。"
                    },
                    new()
                    {
                        Title = "Refresh - 刷新模式",
                        Code = @"// 重复添加只刷新持续时间
BuffData.Create(1002, 10f, 1, StackMode.Refresh);

container.Add(1002);  // 创建实例
container.Add(1002);  // 刷新时间",
                        Explanation = "刷新模式下重复添加只会刷新持续时间。"
                    },
                    new()
                    {
                        Title = "Stack - 堆叠模式",
                        Code = @"// 一个实例，多层堆叠
BuffData.Create(1003, 10f, 5, StackMode.Stack);

container.Add(1003);  // 1 层
container.Add(1003);  // 2 层
int stacks = container.GetStackCount(1003);",
                        Explanation = "达到最大堆叠数后只刷新时间。"
                    }
                }
            };
        }
    }
}
#endif
