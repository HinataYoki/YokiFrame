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

// 访问配置表（通过 Luban 生成的 Tables 类访问具体表）
var tables = TableKit.Tables;

// 检查初始化状态
if (TableKit.Initialized)
{
    // 已初始化
}",
                        Explanation = "TableKit 会自动检测 Luban 生成的代码是 Binary 还是 JSON 模式。"
                    },
                    new()
                    {
                        Title = "异步初始化",
                        Code = @"// 异步初始化（需开启「异步加载模式」并安装 UniTask）
// 预缓存策略：先并发异步加载所有表数据，再同步构造 Tables
await TableKit.InitAsync(destroyCancellationToken);

// 初始化完成后正常访问
var tables = TableKit.Tables;

// 自定义异步加载器（在 InitAsync 之前调用）
TableKit.SetAsyncBinaryLoader(async (fileName, ct) =>
{
    // 自定义异步加载逻辑，例如从网络或自定义资源系统加载
    return await YourAsyncLoadMethod(fileName, ct);
});

// 覆盖表文件名列表（可选，默认使用生成时嵌入的列表）
TableKit.SetTableFileNames(new[] { ""tb_item"", ""tb_config"", ""tb_skill"" });

await TableKit.InitAsync(cancellationToken);

// 注意：如果未显式调用 InitAsync，首次访问 TableKit.Tables
// 将自动触发同步 Init() 加载",
                        Explanation = "异步模式使用 UniTask.WhenAll 并发加载所有表数据到缓存，避免主线程阻塞。可通过 SetAsyncBinaryLoader/SetAsyncJsonLoader 自定义加载方式，通过 SetTableFileNames 覆盖预加载的文件列表。"
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

// 异步重新加载（需开启异步加载模式）
// await TableKit.ReloadAsync(cancellationToken);

// 清理所有数据
TableKit.Clear();"
                    }
                }
            };
        }
    }
}
#endif
