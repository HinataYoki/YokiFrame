#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// TableKit 运行时使用文档
    /// </summary>
    internal static class TableKitDocRuntime
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
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
            };
        }
    }
}
#endif
