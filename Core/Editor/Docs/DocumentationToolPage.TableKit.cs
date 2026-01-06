#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    public partial class DocumentationToolPage
    {
        private DocModule CreateTableKitDoc()
        {
            return new DocModule
            {
                Name = "TableKit",
                Icon = "📊",
                Category = "TOOLS",
                Description = "Luban 配置表集成工具，提供编辑器配置界面和运行时代码生成。支持 Binary 和 JSON 两种数据格式，自动检测加载模式。",
                Sections = new List<DocSection>
                {
                    new()
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
                    },
                    new()
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
                    },
                    new()
                    {
                        Title = "运行时使用",
                        Description = "生成代码后，通过 TableKit 静态类访问配置表。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "基本使用",
                                Code = @"// 初始化（首次访问 Tables 时自动调用）
TableKit.Init();

// 访问配置表
var item = TableKit.Tables.TbItem.Get(1001);
Debug.Log($""物品: {item.Name}, 价格: {item.Price}"");

// 遍历配置表
foreach (var entry in TableKit.Tables.TbItem.DataList)
{
    Debug.Log($""ID: {entry.Id}, Name: {entry.Name}"");
}

// 检查初始化状态
if (TableKit.Initialized)
{
    // 已初始化
}",
                                Explanation = "TableKit 会自动检测 Luban 生成的代码是 Binary 还是 JSON 模式。"
                            },
                            new()
                            {
                                Title = "设置资源路径",
                                Code = @"// 设置运行时路径模式（{0} 为文件名占位符）
// YooAsset 文件名定位
TableKit.SetRuntimePath(""{0}"");

// Addressables 路径
TableKit.SetRuntimePath(""Tables/{0}"");

// 自定义路径
TableKit.SetRuntimePath(""Assets/Data/Tables/{0}"");",
                                Explanation = "路径模式用于运行时加载配置表数据文件。"
                            },
                            new()
                            {
                                Title = "重新加载",
                                Code = @"// 重新加载配置表（热更新后使用）
TableKit.Reload(() =>
{
    Debug.Log(""配置表重新加载完成"");
});

// 清理所有数据
TableKit.Clear();"
                            }
                        }
                    },
                    new()
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
                    },
                    new()
                    {
                        Title = "ExternalTypeUtil",
                        Description = "可选的类型转换工具，将 Luban 的 vector 类型转换为 Unity 的 Vector 类型。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "类型转换",
                                Code = @"// 在 TableKit 工具中勾选「生成 ExternalTypeUtil」后可用

// Luban vector2 -> Unity Vector2
Vector2 pos = ExternalTypeUtil.NewVector2(item.Position);

// Luban vector3 -> Unity Vector3
Vector3 scale = ExternalTypeUtil.NewVector3(item.Scale);

// Luban vector4 -> Unity Vector4
Vector4 color = ExternalTypeUtil.NewVector4(item.Color);

// 也支持 Int 版本
Vector2Int gridPos = ExternalTypeUtil.NewVector2Int(item.GridPosition);
Vector3Int cellPos = ExternalTypeUtil.NewVector3Int(item.CellPosition);",
                                Explanation = "如果配置表中没有使用 vector 类型，可以不生成此文件。"
                            }
                        }
                    },
                    new()
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
                    }
                }
            };
        }
    }
}
#endif
