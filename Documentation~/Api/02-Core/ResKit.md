# ResKit

> 面向读者：需要跨宿主加载资源、管理 lease 或提供场景资源后端的 Runtime 开发者
>
> 主要入口：`ResKit`、`IResourceProvider`、`IResSceneProvider`
>
> 运行边界：跨宿主 Runtime；资源观测和历史只在 Editor/Tools 编译
>
> 状态来源：`Documentation~/Api/00-GettingStarted/Kit_Status.md`

## 适用场景

ResKit 是跨宿主资源加载与所有权 Kit。它负责 Provider、按 `Type + path` 缓存、独立 lease、single-flight 异步加载、raw 数据读取和可选场景 Provider 契约。它不负责 SceneKit 的场景 Handler、场景切换编排或 Instantiate，也不把具体 Unity/Godot 资源 API 放进 Core。

## 入口与当前状态

| 项目 | 当前值 |
|---|---|
| Runtime | 已实现，位于 `Core/Runtime/ResKit` |
| 程序集 | Core Runtime 编入 `YokiFrame`，Provider/场景契约保持纯 C# |
| Interaction | 已实现，Provider 位于 `Core/Editor/ResKit` |
| Workbench | 已完成；提供资源主列表、Lease 来源详情和卸载历史 |
| 状态入口 | `ResKit/state` |
| 宿主接入 | Unity/Godot Adapter 惰性注册默认 Provider；YooAsset 是独立可选 Integration |

## 快速上手

宿主 Adapter 会注册默认 Provider，但不会提前创建。第一次真正的普通、异步、raw 或场景资源调用才创建后端：

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

显式 Provider 优先于默认工厂；框架不维护 `Default`/`Explicit` 来源状态。

## 核心 API

### `IResourceProvider`

| API | 说明 |
|---|---|
| `Load<T>(string path)` | 同步加载引用类型资源；未找到返回 `null`。 |
| `LoadAsync<T>(string path, CancellationToken token)` | 异步加载；安装 UniTask 时编译为 `UniTask<T>`，否则为 `Task<T>`。 |
| `Release(object asset)` | 释放由该 Provider 创建并交给 ResKit 的底层资源。 |
| `ProviderName` | Editor/Tools 中的稳定诊断名称。 |

Provider 的 `path` 是宿主定义的 location，不由 ResKit 改写成 `Resources` 路径。Provider 未找到资源时不得把空对象伪装成成功缓存。

### 可选能力

| 类型/API | 说明 |
|---|---|
| `IRawResourceProvider.LoadRaw` / `LoadRawText` | 同步读取 bytes 或文本。 |
| `IRawResourceProvider.LoadRawAsync` / `LoadRawTextAsync` | 异步读取 bytes 或文本。 |
| `IResourceProviderCapabilities.SupportsRawBytes` | 是否支持 raw bytes。 |
| `SupportsRawText` | 是否支持 raw 文本。 |
| `ResKit.GetSceneProvider()` | 获取当前 Provider 的场景能力；为空时可触发默认 Provider 创建。 |
| `ResKit.TryGetSceneProvider()` | 只读取当前已创建 Provider 的场景能力，不触发默认创建。 |

raw 或 scene 能力不存在时抛出 `NotSupportedException`，不会静默回退到 Unity `Resources.Load`、Godot `ResourceLoader` 或其它 Provider。

### Provider 管理

| API | 说明 |
|---|---|
| `SetProvider(IResourceProvider provider)` | 显式替换 Provider；切换 generation，撤销旧缓存和在途加载，并由原 Provider 释放旧资源。空值抛 `ArgumentNullException`。 |
| `GetProvider()` | 获取当前 Provider；不会触发默认工厂，尚未创建时返回 `null`。 |
| `ProviderName` | Editor/Tools 读取当前名称；未创建时为 `None`。 |
| `ClearAll()` | 撤销全部缓存和在途加载，但保留宿主默认 Provider 工厂。 |

Provider 切换或 `ClearAll` 后，旧异步结果以 stale 失败结束，不能写回新的缓存代次。Provider 忽略取消而晚到的资源仍由创建它的旧 Provider 释放。

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

路径不能为空。Provider 返回 `null` 时不建立空缓存条目。每次 `LoadAsset` 都是独立 lease，只有最后一个引用释放时才调用原 Provider 的 `Release`。

### `ResHandle<T>`

| API | 说明 |
|---|---|
| `Path` | 当前 lease 路径；释放后为 `null`。 |
| `AssetType` | `typeof(T)`。 |
| `Asset` | 当前资源；释放或 `ClearAll` 后为 `null`。 |
| `IsDone` | 当前 lease 是否仍有已完成资源。 |
| `Release()` / `Dispose()` | 幂等释放一次引用。 |
| `ProviderName` | 创建共享条目的 Provider 名称。 |
| `Source` / `SourceFile` / `SourceLine` | Editor/Tools 的可选加载来源；跟踪关闭时可能为空。 |
| `RefCount` | Editor/Tools 当前共享条目总引用数。 |

推荐使用 `using` 管理短生命周期 handle；不要把 handle 跨 Provider 切换长期保存。

### Raw API

| API | 说明 |
|---|---|
| `LoadRaw(string path)` / `LoadRawBytes(string path)` | 同步读取 bytes；后者是语义别名。 |
| `LoadRawText(string path)` | 同步读取文本。 |
| `LoadRawAsync(string path, CancellationToken)` / `LoadRawBytesAsync(...)` | 异步读取 bytes。 |
| `LoadRawTextAsync(string path, CancellationToken)` | 异步读取文本。 |

YooAsset `[2.3.0,4.0.0)` 是可选 Integration。项目可以先自行初始化 `ResourcePackage`，再通过 `ResKit.SetProvider` 或 `YooAssetInitializer.InstallProvider` 注入；也可以直接调用 `YooAssetInitializer.InitializeAsync(new YooAssetInitializationOptions())`，由 Integration 初始化 package 并安装 Provider。初始化器不销毁 package，项目仍负责 package 生命周期。V2/V3 均只通过 ResKit 公开 raw bytes/text，不公开宿主真实文件路径。

`YooAssetInitializationOptions` 的 UI Toolkit Drawer 由 InspectorKit 组合为基础、远端、加密和打包卡片。Unity Editor 只在 TypeCache 扫描到同一方案的构建加密与运行时解密实现时显示该方案及其类名；缺少任一侧时不会显示。内置的常用成对实现为 XOR 流式、文件偏移和 AES-CBC：V2 分别公开 `YooAssetXorStreamEncryptionService` / `YooAssetXorStreamDecryptionService`、`YooAssetFileOffsetEncryptionService` / `YooAssetFileOffsetDecryptionService`、`YooAssetAesEncryptionService` / `YooAssetAesDecryptionService`；V3 对应公开 `IBundleEncryptor` 与 `IBundleDecryptor` 实现。扫描仅影响 Editor 显示，Player 仍由 `EncryptionMode` 和密钥参数直接创建服务，不保存类名或反射实例化。资源包列表以 YooAsset 收集器为唯一 Editor 数据源，Inspector 自动同步为只读 `PackageNames` 快照供 Player 初始化使用，不再由用户手动增删。

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

Host/Web 模式未注册自定义回调时，Integration 会使用 `DefaultHostServer` / `FallbackHostServer` 创建 V2/V3 对等的内置、缓存或 Web 网络文件系统，并把当前解密器应用到每个文件系统。项目需要自定义文件系统时，设置 `HostInitializationHandler` 或 `WebInitializationHandler` 会完全覆盖这套默认配置。需要在项目销毁 package 后重新初始化时，先调用 `YooAssetInitializer.ResetRegistration()`；该方法只清理初始化器登记状态，不销毁 package，也不替换当前 Provider。

### 场景 Provider 契约

ResKit 只提供宿主无关的场景能力边界，SceneKit 负责更高层的 Handler 和编排。`IResSceneProvider` 包含：

| API | 说明 |
|---|---|
| `SceneBackendName` | 场景后端名称。 |
| `ActiveScene` / `GetActiveScene()` | 读取当前激活场景；`GetActiveScene()` 为默认接口实现，等价于 `ActiveScene`，实现方只需提供属性。 |
| `LoadSceneAsync(ResSceneLoadRequest, onComplete, onProgress, onSuspended)` | 加载场景并报告结果、进度和挂起通知。 |
| `UnloadSceneAsync(ResSceneHandle, onComplete)` | 异步卸载场景。 |
| `SetActiveScene(ResSceneHandle)` | 设置当前激活场景。 |
| `UnloadUnusedAssets(Action)` | 请求宿主清理未使用资源。 |

相关值类型和操作契约：

- `ResSceneHandle`：`SceneName`、`BuildIndex`、`IsValid`，支持相等比较。
- `ResSceneLoadRequest`：`SceneName`、`BuildIndex`、`Mode`、`SuspendAtProgress`、`Data`、`IsPreload`。
- `ResSceneLoadResult`：`Scene` 和 `Succeeded`。
- `IResSceneLoadOperation`：`Progress`、`IsSuspended`、`SuspendLoad()`、`ResumeLoad()`、`Recycle()`。
- `IResSceneData`：传递业务数据的标记接口。

SceneKit 默认跟随当前 ResKit Provider 的 `IResSceneProvider`。切换 YooAsset 等 Provider 时只调用 `ResKit.SetProvider`；只有项目自有独立场景系统才为 SceneKit 显式设置独立 backend。

## 宿主与工具入口

工具构建额外提供 `EnableLoadLocationTracking`、`DiagnosticVersion`、`LoadedCount`、`InFlightCount`、`TotalRefCount`、`UnloadHistoryCount`、`GetLoadedAssets(List<ResDebugInfo>)`、`GetUnloadHistory(List<ResUnloadRecord>)` 和 `ClearUnloadHistory()`。来源跟踪默认关闭，只对新建 lease 生效，开启后会产生堆栈成本。`ResKit/state` 为每个已发布资源最多携带一条来源预览和完整来源总数，避免短生命周期资源必须等到第二次查询才出现诊断位置；完整来源仍由显式详情命令按固定上限读取。

当前 Interaction action：

```powershell
yoki telemetry read --engine <engineId> --kit ResKit --name state --project <projectRoot>
yoki command send --engine <engineId> --kit ResKit --action list_resources --payload '{"offset":0,"limit":50}' --project <projectRoot>
```

只读 action 还包括 `stats`、`get_workbench_snapshot`、`get_resource_detail`、`diagnose_resource` 和 `get_unload_history`；维护 action 为 `set_tracking`、`clear_history`。列表分页有界，支持 `expectedVersion` 检测状态变化；资源详情和诊断在单次原子快照中复制来源，不再对同一命令执行二次状态捕获。当前命令不提供远程清缓存、释放资源或切换 Provider。

## 生命周期与错误边界

- 每个等待者拥有自己的取消令牌；取消一个等待者不会取消其它等待者，全部等待者取消后才会请求取消底层加载。
- Provider 切换和 `ClearAll` 会让旧结果 stale；旧结果不得写回新 Provider。
- 释放异常会被聚合和报告，不把资源释放错误交给新的 Provider。
- `Load`、raw 读取和 scene Provider 都要求宿主 Adapter 或项目 Provider 已安装；没有 Provider 时会抛明确的配置异常。

## 限制与相关资料

- `Release(object)` 只消费已登记匿名 lease；handle 独占 lease 与未知对象不会转发给当前 Provider
- SceneKit 负责编排场景 Handler；ResKit 只提供资源与可选场景 Provider 契约
- Interaction 不提供远程清缓存、释放资源或切换 Provider，相关读取入口见 [Workbench、CLI 与 Installer](../../Guides/Tooling.md)
