# SceneKit 场景

## 适用场景

SceneKit 统一场景的加载、预加载、激活、挂起、恢复、卸载和状态查询。业务代码只需要处理场景名称、加载模式和场景数据，不必直接依赖 Unity、Godot 或资源方案的场景 API。

## 使用前提

SceneKit 默认通过 ResKit 加载场景，因此项目应先完成宿主资源接入。使用 YooAsset 时，资源包的初始化可以由对应 Integration 完成，但资源包的销毁和整体生命周期仍由项目负责。

SceneKit 没有独立的 Workbench 页面；需要观察运行状态时使用资源工具入口。

## 快速上手

按名称加载并在完成后使用场景：

```csharp
using YokiFrame;

SceneHandler handler = SceneKit.LoadSceneAsync(
    "Gameplay",
    SceneLoadMode.Single,
    onComplete: loaded =>
    {
        if (loaded == null || loaded.State != SceneState.Loaded)
        {
            return;
        }

        // 场景已加载并激活。
    },
    onProgress: progress => { });

// 离开场景时再卸载：
SceneKit.UnloadSceneAsync(handler);
```

需要先加载资源、稍后再切换时使用预加载：

```csharp
SceneHandler preload = SceneKit.PreloadSceneAsync(
    "Battle",
    onSuspended: ready => SceneKit.ActivatePreloadedScene(ready));
```

`SceneHandler` 用于读取状态、进度、场景句柄和附加数据，也用于后续卸载。

## 核心 API

### 加载和激活

| API | 说明 |
|---|---|
| `LoadSceneAsync(string sceneName, ...)` | 按场景名称异步加载。 |
| `LoadSceneAsync(int buildIndex, ...)` | 按构建索引异步加载；宿主不支持时会进入失败状态。 |
| `SceneLoadMode.Single` | 加载后替换当前场景集合。 |
| `SceneLoadMode.Additive` | 保留当前场景并叠加新场景。 |
| `PreloadSceneAsync(...)` | 预加载场景，在回调中等待激活。 |
| `ActivatePreloadedScene(handler)` | 激活已预加载或已挂起的场景。 |
| `SuspendLoad(handler)` / `ResumeLoad(handler)` | 暂停或继续加载操作；实际挂起粒度由宿主决定。 |

加载回调收到 `null` 表示失败；进度回调的值范围为 `0..1`。加载请求可以附带实现 `ISceneData` 的业务数据：

```csharp
SceneKit.LoadSceneAsync(
    "Battle",
    data: new BattleSceneData { LevelId = 3 },
    onComplete: loaded =>
    {
        BattleSceneData data = SceneKit.GetSceneData<BattleSceneData>();
    });
```

### 卸载和清理

```csharp
SceneKit.UnloadSceneAsync("Battle");
SceneKit.ClearAllScenes(preserveActive: true);
SceneKit.UnloadUnusedAssets();
```

加载尚未完成时也可以请求卸载；SceneKit 会等待当前操作进入可卸载状态，并且同一场景的卸载完成回调只会调用一次。`ClearAllScenes(true)` 保留当前激活场景，传入 `false` 则全部清理。

### 查询状态

| API | 说明 |
|---|---|
| `IsTransitioning` | 是否存在正在加载或卸载的场景。 |
| `GetActiveSceneHandler()` / `GetActiveScene()` | 获取当前激活场景的 Handler 或句柄。 |
| `GetLoadedScenes()` | 获取已登记的场景列表。 |
| `GetSceneHandler(sceneName)` | 按名称获取 Handler。 |
| `IsSceneLoaded(sceneName)` | 判断场景是否正在加载或已经加载。 |
| `GetSceneData<T>()` / `GetSceneData<T>(sceneName)` | 读取激活场景或指定场景的业务数据。 |
| `SceneHandler.State` | `None`、`Loading`、`Loaded`、`Unloading`、`Unloaded` 或 `Failed`。 |
| `SceneHandler.Progress` | 当前加载进度。 |

### 使用自有场景系统

如果项目已有独立场景系统，可以实现 `ISceneBackend` 并显式设置：

```csharp
SceneKit.SetBackend(new ProjectSceneBackend());
SceneKit.LoadSceneAsync("Gameplay");

SceneKit.ClearBackend(); // 恢复使用 ResKit 的默认场景能力
```

显式后端只影响之后创建的场景 Handler。已有 Handler 仍由创建它的后端负责卸载，避免切换资源方案后释放错误的场景。

## 生命周期与错误边界

- 场景加载失败时，完成回调收到 `null`，Handler 状态为 `Failed`。
- `Single` 模式会卸载被替换的旧场景；`Additive` 模式不会自动卸载当前场景。
- 场景卸载完成后，原 `SceneHandler` 和句柄不应继续用于新的加载流程。
- 最后一个场景能否被宿主卸载由宿主场景系统决定，SceneKit 不强行阻止请求。
- 切换自有场景后端不会转移已有场景的卸载责任；YooAsset 资源包也不会由 SceneKit 自动销毁。

## 在工具中查看

SceneKit 没有独立工具页面。需要查看场景资源、当前激活场景或加载历史时，使用 ResKit 的 Workbench/CLI 观察入口。

## 限制与相关资料

SceneKit 负责场景生命周期编排，不负责场景内的业务逻辑、远程场景编辑或资源包管理。自定义场景系统应实现 `ISceneBackend` 的完整加载、卸载、激活和资源清理能力。
