#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// TableKit 编辑器配置文档
    /// </summary>
    internal static class TableKitDocEditorConfig
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "编辑器配置",
                Description = "通过 YokiFrame Tools 面板配置 Luban 生成参数。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "配置项说明",
                        Code = @"// 快捷键：Ctrl+E 打开 YokiFrame Tools 面板
// 选择 TableKit 标签页（需要安装 Luban 包）

// Luban 生成配置：
// - Luban 工作目录：包含 luban.conf 的目录
// - Luban.dll 路径：Luban 工具的 DLL 文件
// - Target (-t)：client / server / all
// - Code Target (-c)：cs-bin / cs-simple-json 等
// - Data Target (-d)：bin / json
// - 数据输出目录：生成的数据文件存放位置
// - 代码输出目录：生成的代码存放位置

// 可选配置：
// - 使用独立程序集：生成 .asmdef 文件
// - 程序集名称：自定义程序集名称（默认 Game.Tables）
// - 生成 ExternalTypeUtil：Luban vector 类型转换工具",
                        Explanation = "配置会自动保存到 EditorPrefs，下次打开时自动加载。"
                    }
                }
            };
        }
    }
}
#endif
