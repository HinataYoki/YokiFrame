#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LogKit 编辑器工具文档
    /// </summary>
    internal static class LogKitDocEditor
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "编辑器工具",
                Description = "编辑器菜单提供日志目录打开和解密功能。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "菜单位置",
                        Code = @"// 菜单路径
// YokiFrame > KitLogger > 打开日志目录
// YokiFrame > KitLogger > 解密日志文件

// 日志文件位置
// Application.persistentDataPath/LogFiles/editor.log (编辑器)
// Application.persistentDataPath/LogFiles/player.log (运行时)"
                    }
                }
            };
        }
    }
}
#endif
