#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// TableKit 概述文档
    /// </summary>
    internal static class TableKitDocOverview
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "概述",
                Description = "TableKit 是一个纯编辑器工具，用于配置和生成 Luban 配置表代码。生成的代码会放在用户指定的目录，与 YokiFrame 框架解耦。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "目录结构",
                        Code = @"// 生成后的目录结构
Assets/Scripts/TabCode/           // 用户指定的代码输出目录
├── Luban/                        // Luban 生成的代码
│   ├── Tables.cs
│   └── cfg/
│       ├── TbItem.cs
│       └── ...
├── TableKit.cs                   // 自动生成的运行时入口
├── ExternalTypeUtil.cs           // 可选：Luban vector 转 Unity Vector
└── Game.Tables.asmdef            // 可选：独立程序集",
                        Explanation = "TableKit.cs 和 ExternalTypeUtil.cs 由工具自动生成，Luban 代码放在 Luban 子目录中。"
                    }
                }
            };
        }
    }
}
#endif
