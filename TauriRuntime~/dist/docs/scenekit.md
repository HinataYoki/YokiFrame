# SceneKit 场景

SceneKit 是跨引擎场景管理门面。业务代码在 `YokiFrame` 命名空间中调用 `SceneKit` 静态入口，Unity 的 `SceneManager`、Godot 的场景树或项目自定义加载器都应隐藏在 `ISceneBackend` 后端里

## 核心类型

| 类型 | 作用 |
|------|------|
| `SceneKit` | 静态统一入口，负责加载、预加载、激活、卸载和诊断快照。 |
| `ISceneBackend` | 场景后端接口，由 Unity/Godot Adapter 或项目自定义实现。 |
| `SceneHandler` | 单个场景加载过程和运行状态句柄。 |
| `SceneLoadRequest` | 后端加载请求，包含场景名、BuildIndex、模式、暂停进度和数据。 |
| `SceneHandle` | 引擎无关的场景句柄。 |
| `ISceneData` | 场景切换携带的纯 C# 数据接口。 |

## 状态与查询

| 属性/方法 | 说明 |
|-----------|------|
| `IsTransitioning` | 是否正在切换场景。 |
| `GetActiveSceneHandler()` | 获取当前活跃场景的 Handler。 |
| `GetActiveScene()` | 获取当前活跃场景的 Handle。 |
| `GetLoadedScenes()` | 获取所有已加载场景的 Handler 列表。 |
| `IsSceneLoaded(sceneName)` | 检查场景是否已加载。 |
| `GetSceneHandler(sceneName)` | 获取指定场景的 Handler。 |
| `GetSceneData<T>()` | 获取当前活跃场景的数据。 |
| `GetSceneData<T>(sceneName)` | 获取指定场景的数据。 |

## 设置后端

Unity 项目由 `UnitySceneKitInstaller` 或 `UnityBootstrap` 注入后端：

```csharp
using YokiFrame.Unity;

UnitySceneKitInstaller.Install();
```

Godot 项目由 `GodotSceneKitInstaller` 或 `GodotBootstrap` 注入后端。业务侧仍只依赖统一静态入口：

```csharp
using YokiFrame;

SceneKit.LoadSceneAsync("Gameplay", SceneLoadMode.Single);
```

## 加载与预加载

```csharp
SceneKit.LoadSceneAsync(
    "Gameplay",
    SceneLoadMode.Single,
    handler => { /* 加载完成 */ },
    progress => { /* 0..1 */ },
    suspendAtProgress: 1f,
    data: new GameplaySceneData());
```

预加载会以 Additive 模式创建场景句柄，可在指定进度暂停，后续显式激活：

```csharp
var handler = SceneKit.PreloadSceneAsync("Battle", suspendAtProgress: 0.9f);
SceneKit.ActivatePreloadedScene(handler);
```

卸载和资源回收：

```csharp
SceneKit.UnloadSceneAsync("Battle");
SceneKit.UnloadUnusedAssets();
```

### 暂停与恢复加载

```csharp
var handler = SceneKit.PreloadSceneAsync("Battle", suspendAtProgress: 0.9f);
// 暂停加载
SceneKit.SuspendLoad(handler);
// 恢复加载
SceneKit.ResumeLoad(handler);
```

### 清空场景

```csharp
SceneKit.ClearAllScenes(preserveActive: true, onComplete: () =>
{
    Debug.Log("All scenes cleared except active");
});
```

### 重置

```csharp
SceneKit.Reset();
```

`Reset()` 会清除后端、所有场景 Handler 和诊断数据。

## Tauri 工作台

SceneKit 页面读取顺序为：

1. `read_telemetry("SceneKit", "state")`
2. `read_snapshot("SceneKit", "state")`
3. `send_command("SceneKit", "get_workbench_snapshot")`

Unity `KitStateSnapshotPublisher` 和 Godot `GodotKitStateSnapshotPublisher` 都通过可选 handler 发布 `SceneKit/state`。页面只在缺少 telemetry/snapshot、用户点击刷新或执行卸载时走命令桥，避免用高频命令轮询场景状态。

## AI 查询建议

AI 默认先读：

```text
.yokiframe/engines/<engineId>/snapshots/SceneKit/state.json
```

snapshot 缺失、过期或需要显式维护动作时，再发送 `SceneKit/get_workbench_snapshot`、`SceneKit/stats`、`SceneKit/list_scenes` 或 `SceneKit/unload_scene`。`unload_scene` 是状态修改命令，只在用户明确要求卸载时使用。

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| Tauri 页面没有场景 | 确认引擎在线、后端已安装，并检查 `SceneKit/state` snapshot。 |
| 后端显示 None | 启动时尚未调用 `SceneKit.SetBackend()`，需要由 Unity/Godot Adapter 安装。 |
| 卸载没有生效 | 检查场景名是否匹配当前 `SceneHandler.SceneName`，再查看命令响应的 `error.code`。 |
| Unity/Godot 加载差异 | 差异应放在 `ISceneBackend` 实现中，业务仍使用 `SceneKit.LoadSceneAsync()`。 |
