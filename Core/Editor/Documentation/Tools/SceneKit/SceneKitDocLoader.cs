#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit 自定义加载器文档
    /// </summary>
    internal static class SceneKitDocLoader
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
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
            };
        }
    }
}
#endif
