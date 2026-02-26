#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 快速开始文档
    /// </summary>
    internal static class BuffKitDocQuickStart
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "快速开始",
                Description = "BuffKit 提供简洁的 Buff 管理 API。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "注册 Buff 配置",
                        Code = @"// 使用 BuffData 静态工厂
BuffKit.RegisterBuffData(BuffData.Create(
    buffId: 1001,
    duration: 10f,
    maxStack: 5,
    stackMode: StackMode.Stack
));

// 链式配置
BuffKit.RegisterBuffData(
    BuffData.Create(1002, 5f)
        .WithTags(100, 101)
        .WithExclusionTags(200)
        .WithTickInterval(1f)
);",
                        Explanation = "使用 int ID 作为 Buff 标识，避免魔法字符串。"
                    },
                    new()
                    {
                        Title = "创建容器并添加 Buff",
                        Code = @"// 创建容器
var container = BuffKit.CreateContainer();

// 添加 Buff
container.Add(1001);
container.Add(1001, 3);  // 添加指定层数

// 更新时间
container.Update(Time.deltaTime);

// 释放
container.Dispose();",
                        Explanation = "每个实体创建一个容器，销毁时调用 Dispose()。"
                    }
                }
            };
        }
    }
}
#endif
