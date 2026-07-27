# ResKit 资源

## 适用场景

ResKit 是跨宿主资源加载与所有权 Kit。它负责资源缓存、独立 handle、异步加载、raw 数据读取和可选场景能力；场景流程由 SceneKit 编排。

## 使用前提

Unity 和 Godot 会提供一个默认资源来源，第一次加载资源时才启用。项目也可以在第一次加载前显式注入自己的资源来源；YooAsset 是可选接入。

## 快速上手

不需要额外初始化就可以直接加载资源：

```csharp
using YokiFrame;

ConfigAsset config = ResKit.Load<ConfigAsset>("Configs/Main");
try
{
    Use(config);
}
finally
{
    ResKit.Release(config);
}
```

需要明确一份独立所有权时使用 handle：

```csharp
using ResHandle<ConfigAsset> handle =
    ResKit.LoadAsset<ConfigAsset>("Configs/Main");

Use(handle.Asset);
```

项目自定义 Provider 必须在第一次资源调用前显式注入：

```csharp
ResKit.SetProvider(new ProjectResourceProvider());
```

显式注入的 Provider 优先于宿主默认资源来源。

## 核心 API

### `IResourceProvider`

| API | 说明 |
|---|---|
| `Load<T>(string path)` | 同步加载引用类型资源；未找到返回 `null`。 |
| `LoadAsync<T>(string path, CancellationToken token)` | 异步加载；安装 UniTask 时编译为 `UniTask<T>`，否则为 `Task<T>`。 |
| `Release(object asset)` | 释放由该 Provider 创建并交给 ResKit 的底层资源。 |
| `ProviderName` | 当前 Provider 的名称。 |

Provider 的 `path` 是宿主定义的 location，不由 ResKit 改写成 `Resources` 路径。Provider 未找到资源时不得把空对象伪装成成功缓存。

### 可选能力

| 类型/API | 说明 |
|---|---|
| `IRawResourceProvider.LoadRaw` / `LoadRawText` | 同步读取 bytes 或文本。 |
| `IRawResourceProvider.LoadRawAsync` / `LoadRawTextAsync` | 异步读取 bytes 或文本。 |
| `IResourceProviderCapabilities.SupportsRawBytes` | 是否支持 raw bytes。 |
| `SupportsRawText` | 是否支持 raw 文本。 |
| `ResKit.GetSceneProvider()` | 获取当前 Provider 的场景能力。 |
| `ResKit.TryGetSceneProvider()` | 只读取已经创建的场景能力；没有时返回空。 |

raw 或 scene 能力不存在时抛出 `NotSupportedException`，不会静默回退到 Unity `Resources.Load`、Godot `ResourceLoader` 或其它 Provider。

### 更换资源来源

| API | 说明 |
|---|---|
| `SetProvider(IResourceProvider provider)` | 显式替换资源来源，并清理旧来源的缓存和进行中的加载。空值抛 `ArgumentNullException`。 |
| `GetProvider()` | 获取当前已使用的资源来源；尚未加载资源时返回 `null`。 |
| `ProviderName` | 在编辑器或工具中查看当前资源来源名称。 |
| `ClearAll()` | 撤销全部缓存和进行中的加载。 |

更换资源来源或调用 `ClearAll` 后，旧异步请求会失效，不能写入新的缓存。已经返回的旧资源仍由原资源来源负责释放。

### 普通、handle 与异步加载

| API | 说明 |
|---|---|
| `Load<T>(string path)` | 获取共享缓存对象；key 是 `typeof(T) + path`。已有同 key 异步加载时会明确要求改用异步 API，不阻塞宿主线程。 |
| `LoadAsset<T>(string path)` | 获取一个独立 `ResHandle<T>` lease。 |
| `LoadAsync<T>(string path, CancellationToken token = default)` | 获取共享缓存对象；相同 key 的并发请求 single-flight。 |
| `LoadAssetAsync<T>(string path, CancellationToken token = default)` | 异步获取独立 handle。返回 `Task` 或 `UniTask` 取决于 `YOKIFRAME_UNITASK_SUPPORT`。 |
| `Release<T>(ResHandle<T> handle)` | 释放一份 handle lease；null 和重复释放无副作用。 |
| `Release(object asset)` | 消费该对象的一份已登记匿名 lease；handle 独占 lease 与未知对象都不受影响。 |
| `ClearAll()` | 立即撤销所有 lease、缓存条目和在途加载。 |

路径不能为空。资源不存在时不会建立空缓存条目。每次 `LoadAsset` 都是独立的所有权凭证，最后一份引用释放后才会释放底层资源。

### `ResHandle<T>`

| API | 说明 |
|---|---|
| `Path` | 当前 lease 路径；释放后为 `null`。 |
| `AssetType` | `typeof(T)`。 |
| `Asset` | 当前资源；释放或 `ClearAll` 后为 `null`。 |
| `IsDone` | 当前 lease 是否仍有已完成资源。 |
| `Release()` / `Dispose()` | 幂等释放一次引用。 |
| `ProviderName` | 创建共享条目的 Provider 名称。 |
| `RefCount` | 当前共享条目总引用数。 |

推荐使用 `using` 管理短生命周期 handle；不要把 handle 跨 Provider 切换长期保存。

### Raw API

| API | 说明 |
|---|---|
| `LoadRaw(string path)` / `LoadRawBytes(string path)` | 同步读取 bytes；后者是语义别名。 |
| `LoadRawText(string path)` | 同步读取文本。 |
| `LoadRawAsync(string path, CancellationToken)` / `LoadRawBytesAsync(...)` | 异步读取 bytes。 |
| `LoadRawTextAsync(string path, CancellationToken)` | 异步读取文本。 |

YooAsset `[2.3.0,4.0.0)` 是可选接入。项目可以自行初始化 `ResourcePackage` 后调用 `ResKit.SetProvider`，也可以使用 `YooAssetInitializer.InitializeAsync` 一步完成初始化和接入。初始化器不会替项目销毁 package，package 的生命周期仍由项目负责。

YooAsset 的初始化选项可在 Unity Inspector 中配置远端、加密和打包参数。资源包列表由 YooAsset 收集器提供；项目仍负责 package 的创建和销毁。

一键初始化示例：

```csharp
using YokiFrame.Unity;
using YooAsset;

await YooAssetInitializer.InitializeAsync(new YooAssetInitializationOptions
{
    EditorPlayMode = EPlayMode.EditorSimulateMode,
    RuntimePlayMode = EPlayMode.OfflinePlayMode,
    EncryptionMode = YooAssetEncryptionMode.XorStream
});

var clip = ResKit.Load<UnityEngine.AudioClip>("Audio/Main");
```

需要自定义 YooAsset 文件系统时，可通过初始化选项提供对应回调。项目重新初始化 package 前，先清理上一次初始化登记；package 的生命周期仍由项目负责。

### 场景资源

ResKit 只提供宿主无关的场景加载能力，场景流程和 Handler 由 [SceneKit](../03-Tool/SceneKit.md) 负责。

| API | 说明 |
|---|---|
| `IResSceneProvider` | 提供场景加载、卸载、激活和进度回调。 |
| `ResSceneHandle` | 表示一个已加载或正在加载的场景。 |
| `ResSceneLoadRequest` | 描述场景名、加载模式、预加载和业务数据。 |

场景加载默认跟随当前 ResKit 资源来源。项目使用独立场景系统时，再按 [SceneKit](../03-Tool/SceneKit.md) 的说明显式设置场景后端。

## 生命周期与错误边界

- 每个等待者拥有自己的取消令牌；取消一个等待者不会取消其它等待者，全部等待者取消后才会请求取消底层加载。
- 更换资源来源和 `ClearAll` 会让旧异步结果失效；旧结果不得写回新的缓存。
- 释放异常会被聚合和报告，不把资源释放错误交给新的 Provider。
- `Load`、raw 读取和场景加载都要求宿主适配已安装；没有可用资源来源时会抛出明确的配置异常。

## 在工具中查看

Workbench 可以查看资源、handle 和卸载历史。工具页面只读，不会替业务清缓存、释放资源或切换 Provider。

## 限制与相关资料

- `Release(object)` 只释放由 ResKit 登记的匿名所有权；handle 独占的资源请调用 handle 自身的 `Release`/`Dispose`
- SceneKit 负责编排场景 Handler；ResKit 只提供资源与可选场景 Provider 契约
- 需要查看运行态时直接打开 Workbench 的 ResKit 页面。
