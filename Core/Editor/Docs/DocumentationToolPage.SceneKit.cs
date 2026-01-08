#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    public partial class DocumentationToolPage
    {
        private DocModule CreateSceneKitDoc()
        {
            return new DocModule
            {
                Name = "SceneKit",
                Icon = KitIcons.SCENEKIT,
                Category = "TOOLS",
                Description = "场景管理工具，提供统一的场景加载、切换、卸载、预加载、过渡效果等功能。支持 YooAsset 扩展和自定义加载器。",
                Keywords = new List<string> { "场景切换", "过渡效果", "预加载", "异步" },
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "基本加载",
                        Description = "SceneKit 提供简洁的场景加载 API。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "同步加载",
                                Code = @"// 同步加载场景（仅用于编辑器或特殊场景）
SceneKit.LoadScene(""GameScene"");

// 指定加载模式
SceneKit.LoadScene(""UIScene"", SceneLoadMode.Additive);

// 通过 BuildIndex 加载
SceneKit.LoadScene(1, SceneLoadMode.Single);",
                                Explanation = "同步加载会阻塞主线程，建议仅在编辑器或启动场景使用。"
                            },
                            new()
                            {
                                Title = "异步加载",
                                Code = @"// 异步加载场景
SceneKit.LoadSceneAsync(""GameScene"");

// 带回调的异步加载
SceneKit.LoadSceneAsync(""GameScene"", SceneLoadMode.Single,
    onComplete: handler => Debug.Log($""场景加载完成: {handler.SceneName}""),
    onProgress: progress => Debug.Log($""加载进度: {progress:P0}""));

// 叠加模式加载
SceneKit.LoadSceneAsync(""UIScene"", SceneLoadMode.Additive);

// 通过 BuildIndex 异步加载
SceneKit.LoadSceneAsync(1, SceneLoadMode.Single);"
                            },
                            new()
                            {
                                Title = "带场景数据加载",
                                Code = @"// 定义场景数据
public class BattleSceneData : ISceneData
{
    public int LevelId { get; set; }
    public int Difficulty { get; set; }
}

// 加载时传递数据
var data = new BattleSceneData { LevelId = 1001, Difficulty = 2 };
SceneKit.LoadSceneAsync(""BattleScene"", data: data);

// 在新场景中获取数据
var battleData = SceneKit.GetSceneData<BattleSceneData>();
Debug.Log($""关卡: {battleData.LevelId}, 难度: {battleData.Difficulty}"");"
                            }
                        }
                    },
                    new()
                    {
                        Title = "场景切换（带过渡效果）",
                        Description = "支持淡入淡出等过渡效果的场景切换。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "淡入淡出切换",
                                Code = @"// 使用默认淡入淡出效果
var transition = new FadeTransition();
SceneKit.SwitchSceneAsync(""GameScene"", transition);

// 自定义淡入淡出参数
var transition = new FadeTransition(
    fadeDuration: 0.5f,      // 淡入淡出时长
    fadeColor: Color.black   // 淡入淡出颜色
);
SceneKit.SwitchSceneAsync(""GameScene"", transition);

// 带回调的切换
SceneKit.SwitchSceneAsync(""GameScene"", transition,
    onComplete: handler => Debug.Log(""切换完成""));"
                            },
                            new()
                            {
                                Title = "自定义过渡效果",
                                Code = @"// 实现自定义过渡效果
public class SlideTransition : ISceneTransition
{
    public float Progress { get; private set; }
    public bool IsTransitioning { get; private set; }

    public void FadeOutAsync(Action onComplete)
    {
        IsTransitioning = true;
        // 实现滑出动画...
        onComplete?.Invoke();
    }

    public void FadeInAsync(Action onComplete)
    {
        // 实现滑入动画...
        IsTransitioning = false;
        onComplete?.Invoke();
    }

    public void Dispose() { }
}

// 使用自定义过渡
SceneKit.SwitchSceneAsync(""GameScene"", new SlideTransition());"
                            }
                        }
                    },
                    new()
                    {
                        Title = "预加载与暂停/恢复",
                        Description = "支持场景预加载和加载暂停/恢复，用于优化加载体验。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "预加载场景",
                                Code = @"// 预加载场景（加载到 90% 后暂停）
var handler = SceneKit.PreloadSceneAsync(""NextLevel"",
    onComplete: h => Debug.Log(""预加载完成，等待激活""),
    onProgress: p => Debug.Log($""预加载进度: {p:P0}""),
    suspendAtProgress: 0.9f);

// 稍后激活预加载的场景
SceneKit.ActivatePreloadedScene(handler);",
                                Explanation = "预加载默认在 90% 进度暂停，兼容 YooAsset 的加载机制。"
                            },
                            new()
                            {
                                Title = "手动暂停/恢复",
                                Code = @"// 加载时指定暂停阈值
var handler = SceneKit.LoadSceneAsync(""GameScene"",
    suspendAtProgress: 0.9f);

// 手动暂停加载
SceneKit.SuspendLoad(handler);

// 恢复加载
SceneKit.ResumeLoad(handler);

// 检查暂停状态
if (handler.IsSuspended)
{
    Debug.Log(""场景加载已暂停"");
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "场景卸载",
                        Description = "卸载已加载的场景。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "卸载场景",
                                Code = @"// 通过场景名卸载
SceneKit.UnloadSceneAsync(""UIScene"", () =>
{
    Debug.Log(""场景已卸载"");
});

// 通过句柄卸载
var handler = SceneKit.GetSceneHandler(""UIScene"");
SceneKit.UnloadSceneAsync(handler);

// 清理所有附加场景（保留活动场景）
SceneKit.ClearAllScenes(preserveActive: true, () =>
{
    Debug.Log(""所有附加场景已清理"");
});

// 卸载未使用的资源
SceneKit.UnloadUnusedAssets();"
                            }
                        }
                    },
                    new()
                    {
                        Title = "场景查询",
                        Description = "查询场景状态和信息。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "查询场景",
                                Code = @"// 获取当前活动场景
var activeScene = SceneKit.GetActiveScene();
Debug.Log($""活动场景: {activeScene.name}"");

// 获取活动场景句柄
var handler = SceneKit.GetActiveSceneHandler();

// 检查场景是否已加载
if (SceneKit.IsSceneLoaded(""GameScene""))
{
    Debug.Log(""GameScene 已加载"");
}

// 获取指定场景的句柄
var gameHandler = SceneKit.GetSceneHandler(""GameScene"");
Debug.Log($""状态: {gameHandler.State}, 进度: {gameHandler.Progress}"");

// 获取所有已加载场景
var loadedScenes = SceneKit.GetLoadedScenes();
foreach (var h in loadedScenes)
{
    Debug.Log($""场景: {h.SceneName}, 状态: {h.State}"");
}

// 检查是否正在过渡
if (SceneKit.IsTransitioning)
{
    Debug.Log(""场景切换进行中..."");
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "场景事件",
                        Description = "监听场景加载、卸载等事件。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "事件监听",
                                Code = @"// 监听场景加载开始
EventKit.Type.Register<SceneLoadStartEvent>(e =>
{
    Debug.Log($""开始加载: {e.SceneName}, 模式: {e.Mode}"");
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 监听加载进度
EventKit.Type.Register<SceneLoadProgressEvent>(e =>
{
    Debug.Log($""加载进度: {e.SceneName} - {e.Progress:P0}"");
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 监听加载完成
EventKit.Type.Register<SceneLoadCompleteEvent>(e =>
{
    Debug.Log($""加载完成: {e.SceneName}"");
    // 可以访问 e.Scene 和 e.Handler
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 监听场景卸载
EventKit.Type.Register<SceneUnloadEvent>(e =>
{
    Debug.Log($""场景已卸载: {e.SceneName}"");
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 监听活动场景切换
EventKit.Type.Register<ActiveSceneChangedEvent>(e =>
{
    Debug.Log($""活动场景从 {e.PreviousScene.name} 切换到 {e.NewScene.name}"");
}).UnRegisterWhenGameObjectDestroyed(gameObject);"
                            }
                        }
                    },
                    new()
                    {
                        Title = "UniTask 支持",
                        Description = "使用 UniTask 进行异步场景操作。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "UniTask 异步加载",
                                Code = @"// 需要安装 UniTask 并定义 YOKIFRAME_UNITASK_SUPPORT

// 异步加载场景
var handler = await SceneKit.LoadSceneUniTaskAsync(""GameScene"");
Debug.Log($""场景加载完成: {handler.SceneName}"");

// 带取消令牌
var cts = new CancellationTokenSource();
try
{
    var handler = await SceneKit.LoadSceneUniTaskAsync(
        ""GameScene"",
        cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    Debug.Log(""加载已取消"");
}

// 异步切换场景
await SceneKit.SwitchSceneUniTaskAsync(""GameScene"", new FadeTransition());

// 异步卸载场景
await SceneKit.UnloadSceneUniTaskAsync(""UIScene"");"
                            }
                        }
                    },
                    new()
                    {
                        Title = "自定义加载器",
                        Description = "SceneKit 默认使用 ResKit 的场景加载器，支持自定义扩展。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "使用 YooAsset 加载器",
                                Code = @"// 在游戏初始化时配置 YooAsset 场景加载器
var package = YooAssets.GetPackage(""DefaultPackage"");

// 方式1：通过 ResKit 设置（推荐）
#if YOKIFRAME_UNITASK_SUPPORT
ResKit.SetSceneLoaderPool(new YooAssetSceneLoaderUniTaskPool(package));
#else
ResKit.SetSceneLoaderPool(new YooAssetSceneLoaderPool(package));
#endif

// SceneKit 会自动使用 ResKit 的场景加载器
SceneKit.LoadSceneAsync(""GameScene"");

// 方式2：直接设置 SceneKit 加载器池
SceneKit.SetLoaderPool(new ResKitSceneLoaderPool());",
                                Explanation = "ResKit 的场景加载器支持 YooAsset 的 90% 暂停加载特性。"
                            },
                            new()
                            {
                                Title = "自定义加载器实现",
                                Code = @"// 实现自定义场景加载器
public class CustomSceneLoader : ISceneLoader
{
    private readonly ISceneLoaderPool mPool;

    public bool IsSuspended { get; private set; }
    public float Progress { get; private set; }

    public CustomSceneLoader(ISceneLoaderPool pool) => mPool = pool;

    public void LoadAsync(string sceneName, SceneLoadMode mode,
        Action<Scene> onComplete, Action<float> onProgress = null,
        float suspendAtProgress = 1f)
    {
        // 实现自定义加载逻辑...
    }

    public void LoadAsync(int buildIndex, SceneLoadMode mode,
        Action<Scene> onComplete, Action<float> onProgress = null,
        float suspendAtProgress = 1f)
    {
        // 实现自定义加载逻辑...
    }

    public void UnloadAsync(Scene scene, Action onComplete)
    {
        // 实现自定义卸载逻辑...
    }

    public void SuspendLoad() => IsSuspended = true;
    public void ResumeLoad() => IsSuspended = false;
    public void Recycle() => mPool?.Recycle(this);
}

// 实现加载器池
public class CustomSceneLoaderPool : ISceneLoaderPool
{
    private readonly Stack<ISceneLoader> mPool = new();

    public ISceneLoader Allocate() =>
        mPool.Count > 0 ? mPool.Pop() : new CustomSceneLoader(this);

    public void Recycle(ISceneLoader loader) => mPool.Push(loader);
}

// 使用自定义加载器
SceneKit.SetLoaderPool(new CustomSceneLoaderPool());"
                            }
                        }
                    },
                    new()
                    {
                        Title = "编辑器工具",
                        Description = "SceneKit 提供编辑器工具面板，用于查看和管理运行时场景。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "使用编辑器工具",
                                Code = @"// 快捷键：Ctrl+E 打开 YokiFrame Tools 面板
// 选择 SceneKit 标签页

// 功能：
// - 查看所有已加载场景列表
// - 查看场景状态（加载中/已加载/卸载中）
// - 查看加载进度和暂停状态
// - 卸载指定场景
// - 设置活动场景
// - 卸载所有非活动场景",
                                Explanation = "编辑器工具在运行时自动刷新，方便调试场景管理。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "最佳实践",
                        Description = "场景管理的推荐用法。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "场景管理模式",
                                Code = @"// 推荐的场景组织方式
// 1. 启动场景（Bootstrap）- 初始化框架和资源
// 2. 主场景（Main）- 主菜单、大厅等
// 3. 游戏场景（Game）- 实际游戏内容
// 4. UI 场景（UI）- 叠加的 UI 层

// 启动流程示例
public class GameBootstrap : MonoBehaviour
{
    async void Start()
    {
        // 1. 初始化框架
        await InitializeFramework();

        // 2. 预加载常用场景
        SceneKit.PreloadSceneAsync(""MainMenu"");

        // 3. 切换到主菜单
        await SceneKit.SwitchSceneUniTaskAsync(""MainMenu"",
            new FadeTransition(0.5f));
    }
}

// 游戏场景切换示例
public class SceneController
{
    public async UniTask EnterBattle(int levelId)
    {
        // 传递场景数据
        var data = new BattleSceneData { LevelId = levelId };

        // 带过渡效果切换
        await SceneKit.SwitchSceneUniTaskAsync(""Battle"",
            new FadeTransition(),
            data);

        // 叠加 UI 场景
        await SceneKit.LoadSceneUniTaskAsync(""BattleUI"",
            SceneLoadMode.Additive);
    }

    public async UniTask ExitBattle()
    {
        // 卸载 UI 场景
        await SceneKit.UnloadSceneUniTaskAsync(""BattleUI"");

        // 返回主菜单
        await SceneKit.SwitchSceneUniTaskAsync(""MainMenu"",
            new FadeTransition());
    }
}",
                                Explanation = "使用 Single 模式切换主场景，Additive 模式叠加 UI 场景。"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
