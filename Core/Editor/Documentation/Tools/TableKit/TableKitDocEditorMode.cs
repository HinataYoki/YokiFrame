#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// TableKit 编辑器模式文档
    /// </summary>
    internal static class TableKitDocEditorMode
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "编辑器模式",
                Description = "在编辑器中直接访问配置表数据，无需运行游戏。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "编辑器访问",
                        Code = @"#if UNITY_EDITOR
// 设置编辑器数据路径
TableKit.SetEditorDataPath(""Assets/Art/Table/"");

// 访问编辑器配置表（自动初始化）
var item = TableKit.TablesEditor.TbItem.Get(1001);
Debug.Log($""[Editor] 物品: {item.Name}"");

// 刷新编辑器缓存（数据文件更新后）
TableKit.RefreshEditor();
#endif",
                        Explanation = "编辑器模式直接从 AssetDatabase 加载数据，不依赖资源管理系统。"
                    }
                }
            };
        }
    }
}
#endif
