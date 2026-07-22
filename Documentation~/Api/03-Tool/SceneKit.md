# SceneKit 场景

> 面向读者：需要把业务场景流程与宿主场景 API 解耦的 Runtime 开发者
>
> 主要入口：`SceneKit`、`SceneHandler`
>
> 运行边界：跨宿主 Runtime；默认场景能力来自 ResKit Provider
>
> 状态来源：`Documentation~/Api/00-GettingStarted/Kit_Status.md`

## 适用场景

SceneKit 负责场景加载、预加载、激活、挂起/恢复、卸载、场景数据和生命周期事件。它适合把业务场景流程与 Unity、Godot 或 YooAsset 的具体场景 API 解耦，并让场景 Handler 保留明确的后端所有权。

SceneKit 不负责资源 package 初始化、场景内容业务逻辑、Player/Editor 通信或 Workbench 诊断。YooAsset Integration 可以通过 `YooAssetInitializer` 完成 package 初始化和 ResKit Provider 安装，但 package 销毁仍归项目生命周期。默认场景能力来自当前 ResKit Provider 的 `IResSceneProvider`；项目有独立场景系统时才注入显式 Backend。

## 入口与当前状态

SceneKit Runtime API、ResKit 场景 Provider 契约以及 Unity、Godot、YooAsset 默认实现已经迁入。SceneKit 被明确定位为纯 Runtime 场景编排能力，**不规划** Kit Interaction、CLI command、Application 强类型 read model 或 Workbench 页面；本页只描述可调用的 Runtime API。

## 快速上手

默认路径下，先注入或让 ResKit 惰性创建当前宿主 Provider，再通过场景名加载：

```csharp
SceneHandler handler = SceneKit.LoadSceneAsync(
    "Gameplay",
    SceneLoadMode.Single,
    onComplete: loaded =>
    {
        if (loaded != null && loaded.State == SceneState.Loaded)
        {
            // 场景已加载并按 Single 规则激活。
        }
    },
    onProgress: progress => { });

SceneKit.UnloadSceneAsync("Gameplay");
```

需要在完成加载后再激活时使用预加载：

```csharp
SceneHandler preload = SceneKit.PreloadSceneAsync(
    "Battle",
    onSuspended: ready => SceneKit.ActivatePreloadedScene(ready));
```

场景加载完成后，Handler 仍由调用方持有用于查询和卸载；场景数据通过 `ISceneData` 随请求传入，读取当前数据使用 `GetSceneData<T>()`。

## 核心 API

### 后端所有权

ResKit 的三个能力接口保持分离：

| 接口 | 职责 |
|---|---|
| `IResourceProvider` | 普通资源、缓存 entry 和 lease 的底层加载/释放。 |
| `IRawResourceProvider` | 不进入对象缓存的 bytes 和文本。 |
| `IResSceneProvider` | 场景加载、卸载、激活、进度和加载操作。 |

Unity、Godot 和 YooAsset Provider 同时实现匹配的 `IResSceneProvider`。SceneKit 未设置显式 Backend 时会跟随 ResKit 当前 Provider，因此切换 YooAsset 只需切换一次：

```csharp
YooAssetInitializer.InstallProvider(initializedPackage);
SceneKit.LoadSceneAsync("Gameplay");
```

项目拥有独立场景系统时可以显式覆盖；显式 Backend 始终优先：

```csharp
SceneKit.SetBackend(new ProjectSceneBackend());
```

调用 `SceneKit.ClearBackend()` 后恢复跟随 ResKit Provider。`GetBackend()` 只查询当前状态，不会为了诊断创建默认 Provider；第一次真实加载或清理调用会按 ResKit 规则惰性创建宿主默认 Provider。每个 `SceneHandler` 保存创建它的后端，Provider 后续切换不会把旧场景交给新 Provider 卸载。

### 加载场景

```csharp
SceneKit.LoadSceneAsync(
    "Gameplay",
    SceneLoadMode.Single,
    handler => { /* loaded；失败时为 null */ },
    progress => { /* 0..1 */ },
    suspendAtProgress: 1f,
    data: new GameplaySceneData());
```

也可以使用 Unity BuildIndex；不支持 BuildIndex 的 Provider 会返回无效结果并使 Handler 进入 `SceneState.Failed`。

预加载和激活：

```csharp
SceneHandler handler = SceneKit.PreloadSceneAsync(
    "Battle",
    suspendAtProgress: 0.9f,
    onSuspended: ready => { /* ready to activate */ });

SceneKit.ActivatePreloadedScene(handler);
```

Provider 只能兑现自身支持的挂起粒度。Unity SceneManager 和 YooAsset 在宿主帧循环中报告进度阈值；Godot 先同步解析 PackedScene，预加载或低于 1 的挂起阈值会延迟实例化/切场景，直到操作恢复后才提交激活。

### 卸载与清理

```csharp
SceneKit.SuspendLoad(handler);
SceneKit.ResumeLoad(handler);
SceneKit.UnloadSceneAsync("Battle");
SceneKit.ClearAllScenes(preserveActive: true);
SceneKit.UnloadUnusedAssets();
```

最后一个场景不会被 SceneKit 策略阻止卸载；具体宿主能否卸载当前唯一场景由 Provider 兑现。Single 模式会让 SceneKit 对旧 Handler 执行真实后端卸载确认，不只清理逻辑缓存。

加载尚未完成时可以请求卸载。SceneKit 会先把 Handler 转为 `Unloading`，等待加载得到有效宿主句柄后只提交一次后端卸载；期间新增的卸载完成回调会合并等待，不会提前报告完成。

### 查询状态

| API | 说明 |
|---|---|
| `SetBackend` / `ClearBackend` / `GetBackend` | 显式覆盖、恢复 ResKit 默认路由和查询当前后端。 |
| `IsTransitioning` | 是否存在 Loading 或 Unloading Handler。 |
| `GetActiveSceneHandler()` | 当前激活场景 Handler。 |
| `GetActiveScene()` | 当前激活场景句柄。 |
| `GetLoadedScenes()` | 已登记场景列表。 |
| `GetSceneHandler(sceneName)` | 按名称获取 Handler。 |
| `IsSceneLoaded(sceneName)` | 场景是否处于有效加载生命周期。 |
| `GetSceneData<T>()` | 当前激活场景业务数据。 |

`SceneState` 包含 `None`、`Loading`、`Loaded`、`Unloading`、`Unloaded` 和 `Failed`。Provider 返回无效句柄时不会伪装为 Loaded。

## 生命周期与错误边界

- `SceneHandler` 始终由创建它的 Backend 卸载；Provider/Backend 后续切换不改变旧 Handler 的所有权
- 加载尚未完成时的卸载请求会合并，直到可用宿主句柄出现后才提交一次真实卸载
- 默认 Backend 只在第一次真实场景操作时由 ResKit 路由解析；状态查询不应隐式创建 Provider
- 业务场景内容、资源 package 销毁和宿主场景限制仍由项目与匹配 Adapter 负责

## 宿主与工具入口

当前提供 Unity、Godot 与 YooAsset 的独立场景后端实现。SceneKit 不发布 Kit Interaction、CLI action 或 Workbench 页面；这是已确认非目标，需要观察资源状态时使用 ResKit 的工具入口。

## 限制与相关资料

- 不规划 SceneKit Interaction Provider、snapshot/telemetry、CLI action 或 Workbench 页面。
- Runtime SceneKit 不直接写 FileBridge、JSON 或 Shared Memory。
- YooAsset package 可由 `YooAssetInitializer` 初始化并自动接入 ResKit，也可由项目初始化后显式注入；package 销毁始终由项目负责。
