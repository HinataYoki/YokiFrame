# SceneKit 场景

## 配置后端

Unity 项目通常由统一初始化安装 ResKit Provider 和场景后端：

```csharp
using YokiFrame;

YokiFrameKit.Initialize(YokiFrameEngine.Unity);
```

内置 `UnityResourceProvider` 和 `YooAssetResourceProvider` 同时提供场景后端。切换 YooAsset 时只换 ResKit Provider：

```csharp
ResKit.SetProvider(new YooAssetResourceProvider());
```

项目自定义场景系统时再显式设置：

```csharp
SceneKit.SetBackend(new ProjectSceneBackend());
```

## 加载场景

```csharp
SceneKit.LoadSceneAsync(
    "Gameplay",
    SceneLoadMode.Single,
    handler => { /* loaded */ },
    progress => { /* 0..1 */ },
    suspendAtProgress: 1f,
    data: new GameplaySceneData());
```

预加载和激活：

```csharp
var handler = SceneKit.PreloadSceneAsync("Battle", suspendAtProgress: 0.9f);
SceneKit.ActivatePreloadedScene(handler);
```

暂停、恢复、卸载：

```csharp
SceneKit.SuspendLoad(handler);
SceneKit.ResumeLoad(handler);
SceneKit.UnloadSceneAsync("Battle");
SceneKit.UnloadUnusedAssets();
```

## 查询状态

| 方法 | 说明 |
|---|---|
| `IsTransitioning` | 是否正在切换。 |
| `GetActiveSceneHandler()` | 当前活跃场景 Handler。 |
| `GetActiveScene()` | 当前活跃场景句柄。 |
| `GetLoadedScenes()` | 已加载场景列表。 |
| `IsSceneLoaded(sceneName)` | 场景是否已加载。 |
| `GetSceneData<T>()` | 当前活跃场景数据。 |

## 工作台诊断

SceneKit 页面用于查看后端、当前场景、已加载场景和切换状态。

| 在工作台里看什么 | 用途 |
|---|---|
| Backend | 确认场景加载由 Unity、Godot 还是项目后端处理。 |
| Active Scene | 查看当前激活场景。 |
| Loaded Scenes | 检查 additive 场景是否仍在内存中。 |
| Transition State | 判断是否正在加载、预加载或卸载。 |
| History | 回看最近场景操作。 |

场景卡住时，先看 Transition State，再看 Loaded Scenes 是否有旧场景未卸载。卸载场景会改变运行状态，只在明确目标时执行。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 后端是 None | 初始化宿主，或确认当前 ResKit Provider 实现场景能力。 |
| 卸载没生效 | 检查场景名是否匹配 `SceneHandler.SceneName`。 |
| YooAsset 场景找不到 | 检查 scene location 和当前 Provider。 |
| 预加载卡住 | 检查 `suspendAtProgress`，需要时调用 `ActivatePreloadedScene()` 或 `ResumeLoad()`。 |
| Unity/Godot 加载差异 | 差异放 `ISceneBackend`，业务仍调用 `SceneKit`。 |
