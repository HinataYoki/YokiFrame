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
    // 同步初始化
    void Start()
    {
        TableKit.SetRuntimePath(""{0}"");
        TableKit.Init();
    }

    // 或异步初始化（需开启异步加载模式 + UniTask）
    // async UniTaskVoid Start()
    // {
    //     TableKit.SetRuntimePath(""{0}"");
    //     await TableKit.InitAsync(destroyCancellationToken);
    // }
}

// 4. 异步 vs 同步选择
// 表少（< 10 张）：同步即可，开销可忽略
// 表多（> 30 张）：推荐异步，避免启动卡顿
// 如果未调用 InitAsync，首次 TableKit.Tables 访问自动同步加载",
                        Explanation = "开启异步加载模式后，生成代码包含 InitAsync/ReloadAsync 方法。可通过 SetAsyncBinaryLoader/SetAsyncJsonLoader 自定义异步加载方式，通过 SetTableFileNames 覆盖预加载的文件列表。配置表 ID 常量可以通过 Luban 的枚举或常量表生成。"
                    }
                }
            };
        }
    }
}
#endif
