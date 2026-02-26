#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// TableKit 最佳实践文档
    /// </summary>
    internal static class TableKitDocBestPractice
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "最佳实践",
                Description = "使用 TableKit 的推荐方式。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "推荐用法",
                        Code = @"// 1. 使用 Binary 模式（性能更好）
// 在 TableKit 工具中设置 Code Target 为 cs-bin，Data Target 为 bin

// 2. 使用独立程序集（编译隔离）
// 勾选「使用独立程序集」，配置表代码变更不会触发全项目重编译

// 3. 在游戏启动时初始化
public class GameLauncher : MonoBehaviour
{
    void Start()
    {
        // 设置路径（根据资源管理方案）
        TableKit.SetRuntimePath(""{0}"");
        
        // 初始化配置表
        TableKit.Init();
        
        // 继续游戏初始化...
    }
}

// 4. 避免魔法数字，使用配置表 ID
var item = TableKit.Tables.TbItem.Get(ItemIds.SWORD_001);
// 而不是
var item = TableKit.Tables.TbItem.Get(1001);",
                        Explanation = "配置表 ID 常量可以通过 Luban 的枚举或常量表生成。"
                    }
                }
            };
        }
    }
}
#endif
