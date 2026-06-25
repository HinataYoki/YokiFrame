# ResKit 资源

ResKit 是运行时统一资源 API。它不直接绑定 Unity `Resources`、YooAsset、Addressables、Godot `ResourceLoader` 或 Godot `FileAccess`，而是通过 `IResourceProvider` / `IRawResourceProvider` 接入具体资源后端。

## 核心类型

| 类型 | 作用 |
|------|------|
| `ResKit` | 静态资源入口，负责缓存、引用计数、卸载历史和调试数据。 |
| `IResourceProvider` | 资源后端接口，提供同步加载、异步加载、实例化和释放。 |
| `IRawResourceProvider` | 原始资源后端接口，提供文本、bytes 和原始文件路径读取。 |
| `ResHandle<T>` | 带引用计数的资源句柄，支持 `Retain()`、`Release()`、`Dispose()`。 |
| `ResDebugInfo` | 当前已加载资源的调试数据。 |
| `ResUnloadRecord` | 引用归零或 `ClearAll()` 后的卸载记录。 |

## 统计属性

ResKit 提供以下只读属性用于诊断：

| 属性 | 说明 |
|------|------|
| `ProviderName` | 当前 Provider 名称。 |
| `LoadedCount` | 当前已加载资源数量。 |
| `TotalRefCount` | 所有资源的引用计数总和。 |
| `UnloadHistoryCount` | 卸载历史记录数量。 |

```csharp
Debug.Log($"Provider: {ResKit.ProviderName}");
Debug.Log($"Loaded: {ResKit.LoadedCount}, Refs: {ResKit.TotalRefCount}");
```

## 设置 Provider

使用 ResKit 前必须先设置 Provider。Unity 默认 Provider 是 `UnityResourceProvider`，基于 `Resources.Load` / `Resources.LoadAsync` / prefab instantiate，raw 读取基于 `TextAsset`。

```csharp
using YokiFrame;
using YokiFrame.Unity;

ResKit.SetProvider(new UnityResourceProvider());
```

安装 YooAsset 后，Unity Editor 会自动启用 `YOKIFRAME_YOOASSET_SUPPORT`。需要切换到 YooAsset 后端时仍然使用同一个 `YokiFrame.ResKit` 入口，只替换 Provider：

```csharp
#if YOKIFRAME_YOOASSET_SUPPORT
ResKit.SetProvider(new YooAssetResourceProvider());
#endif
```

`YooAssetResourceProvider` 同时兼容 YooAsset 2.3.x 和 3.x。2.3.x 使用 YooAsset 默认包静态 API；3.x 可额外传入 `ResourcePackage` 或包名：

```csharp
#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
var package = YooAssets.GetPackage("DefaultPackage");
ResKit.SetProvider(new YooAssetResourceProvider(package));
#endif
```

`UnityBootstrap` 会在初始化时完成这一步。如果项目使用自己的资源系统，实现 `IResourceProvider` 后手动注入：

```csharp
using System.Threading;
using System.Threading.Tasks;
using YokiFrame;

public sealed class ProjectResourceProvider : IResourceProvider
{
    public string ProviderName => "Project.Custom";

    public T Load<T>(string path) where T : class
    {
        return default;
    }

    public Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
    {
        return Task.FromResult(Load<T>(path));
    }

    public IEngineObject Instantiate(string path)
    {
        return null;
    }

    public void Release(object asset)
    {
    }
}

ResKit.SetProvider(new ProjectResourceProvider());
```

如果 `YOKIFRAME_UNITASK_SUPPORT` 已启用，`IResourceProvider.LoadAsync<T>()` 和 raw 异步接口的返回值会从 `Task<T>` 切换为 `UniTask<T>`，自定义 Provider 也需要实现同一套条件编译签名。

`SetProvider()` 会先调用 `ClearAll()`，因此切换 Provider 会清空当前缓存并记录卸载历史。

## 同步加载

`Load<T>()` 返回资源对象：

```csharp
var config = ResKit.Load<MyConfig>("Configs/GameConfig");
var icon = ResKit.Load<Sprite>("Sprites/Icon");
```

需要明确控制生命周期时，使用 `LoadAsset<T>()`：

```csharp
var handle = ResKit.LoadAsset<MyConfig>("Configs/GameConfig");
try
{
    Use(handle.Asset);
}
finally
{
    handle.Release();
}
```

相同 `path + T` 会复用同一个缓存句柄，并增加 `RefCount`。当 `RefCount` 归零时，ResKit 会移除缓存，并调用 Provider 的 `Release(asset)`。

## 异步加载

`YokiFrame.ResKit` 是 Unity 和 Godot 共用的唯一 ResKit 入口。默认异步返回 `Task<T>`；Unity 项目安装 UniTask 后，Editor 会自动启用 `YOKIFRAME_UNITASK_SUPPORT` 宏，同一组 `LoadAsync()` / `LoadAssetAsync()` / `LoadRawAsync()` / `LoadRawTextAsync()` 返回值会直接切换为 `UniTask<T>`。

```csharp
using System.Threading;
using YokiFrame;

using var cts = new CancellationTokenSource();
var handle = await ResKit.LoadAssetAsync<MyConfig>("Configs/GameConfig", cts.Token);
try
{
    Use(handle.Asset);
}
finally
{
    handle.Release();
}
```

## 原始资源加载

Unity 和 Godot 调用侧都通过同一套 ResKit API 读取 raw 文件：

```csharp
var bytes = ResKit.LoadRaw("Configs/GameConfig");
var text = ResKit.LoadRawText("Configs/GameConfig");
var asyncBytes = await ResKit.LoadRawAsync("Configs/GameConfig", cts.Token);
var asyncText = await ResKit.LoadRawTextAsync("Configs/GameConfig", cts.Token);
```

`LoadRaw()` 返回 bytes，`LoadRawText()` 返回文本。1.x 的 `LoadRawFileData()` / `LoadRawFileText()` raw 别名已移除。

默认 Unity Provider 使用 `Resources.Load<TextAsset>()`，路径仍是 Resources 内路径。YooAsset Provider 使用 `LoadRawFile` / `RawFileObject` 兼容 YooAsset 2.3.x 和 3.x，并支持 `GetRawFilePath()`。默认 Godot Provider 使用 `FileAccess`，支持 `res://`、`user://` 等 Godot 文件路径。

自定义 Provider 如果要支持 raw 读取，除 `IResourceProvider` 外还需要实现 `IRawResourceProvider`。否则调用 `LoadRaw()` 会抛出 `NotSupportedException`。

获取原始文件的物理路径（需要 Provider 支持）：

```csharp
string path = ResKit.GetRawFilePath("Configs/GameConfig");
```

## 实例化与释放

```csharp
var obj = ResKit.Instantiate("Prefabs/Player");
```

`Instantiate()` 返回 `IEngineObject`。Unity Provider 会加载 `GameObject` prefab 并实例化；Godot Provider 可以实例化 `PackedScene`。

清空全部缓存：

```csharp
ResKit.ClearAll();
ResKit.ClearUnloadHistory();
```

Unity `Resources` 后端的资源生命周期主要由 Unity 管理；YooAsset Provider 会在 ResKit 引用归零时释放对应 `AssetHandle`。自定义 Addressables Provider 应在 `Release()` 中释放对应句柄。

## 场景后端管理

ResKit 提供场景后端管理接口，供 SceneKit 或项目自定义场景系统使用：

| 属性/方法 | 说明 |
|-----------|------|
| `SceneBackendName` | 当前场景后端名称。 |
| `SetSceneBackend(backend)` | 设置场景后端。 |
| `GetSceneBackend()` | 获取当前场景后端。 |
| `ClearSceneBackend()` | 清除场景后端。 |

## ResHandle 属性

`ResHandle<T>` 除了 `Asset`、`Release()`、`Retain()`、`Dispose()` 外，还提供以下属性：

| 属性 | 说明 |
|------|------|
| `Path` | 资源路径。 |
| `AssetType` | 资源类型。 |
| `ProviderName` | 加载该资源的 Provider 名称。 |
| `RefCount` | 当前引用计数。 |
| `IsDone` | 异步加载是否已完成。 |
| `Source` | 加载调用位置（需开启 `EnableLoadLocationTracking`）。 |
| `SourceFile` | 加载调用文件路径。 |
| `SourceLine` | 加载调用行号。 |

## 加载位置记录

默认不记录调用位置，避免普通加载路径付出堆栈采集成本。需要排查资源生命周期时可以临时打开：

```csharp
ResKit.EnableLoadLocationTracking = true;
```

开启后，新加载资源会记录：

- `Source`
- `SourceFile`
- `SourceLine`

已经缓存的资源不会补录位置，需要释放后重新加载才会记录。

读取当前加载数据：

```csharp
var loaded = new List<ResDebugInfo>();
ResKit.GetLoadedAssets(loaded);

var history = new List<ResUnloadRecord>();
ResKit.GetUnloadHistory(history);
```

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| `ResKit provider is not configured` | 先调用 `ResKit.SetProvider(...)`，或确保 `UnityBootstrap` 已初始化。 |
| `provider does not support raw resources` | 当前 Provider 没有实现 `IRawResourceProvider`，需要换用支持 raw 的后端。 |
| 资源一直不释放 | 确认所有 `LoadAsset<T>()` 返回的 handle 都调用了 `Release()`。 |
| 没有加载位置 | 先打开 `EnableLoadLocationTracking`，再重新触发加载。 |
| `Load<T>()` 不方便释放 | 需要严格生命周期时使用 `LoadAsset<T>()`，不要只拿资源对象。 |
