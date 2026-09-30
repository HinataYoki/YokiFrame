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

初始化多个 package 时，ResKit 仍只安装一个 Provider。普通路径按 `PackageNames` 的顺序探测，第一个清单包含该 location 的包负责加载；都没有时才由第一包返回失败。同名 location 不会继续向后查找，需要覆盖旧资源时必须把新包排在前面。

`YooAssetInitializer.InitializeAsync` 当前对 `PackageNames` 中的所有 package 使用同一套运行模式、远端地址和联网回退策略。这覆盖了主包与 DLC 都使用 Host/CDN、都使用 Web 远端按需加载，以及“部分 package 热更、部分 package 只随包体发布”的常见方案。ResKit 全局只安装一个 Provider，但 `YooAssetResourceProvider` 可以在这个 Provider 内部代理多个 package；这里的多包指 Provider 内部代理多个 package，不是安装多个 ResKit Provider。当前不提供 package 级独立策略；混合热更包与不热更包时，统一使用 HostPlayMode + `RemoteThenOffline`，没有对应远端版本的 package 会回退到包体内置资源。单机 `OfflinePlayMode`/`CustomPlayMode` 不执行远端更新，也不会显示联网处理配置。

需要固定某个包时，在路径前加 `package:{包名}/`，例如 `package:DLC/Prefabs/Enemy`。显式包不存在或不包含该 location 时直接失败，不会改走自动探测。显式路径和普通路径是不同缓存键。包内依赖不会跨包补齐，场景重名时也应显式指定包。

YooAsset 的初始化选项可在 Unity Inspector 中配置运行模式、远端、加密和联网更新策略。资源包列表由 YooAsset 收集器提供；项目仍负责 package 的创建和销毁。

场景直接使用时，挂载 `YooAssetInitializationBehaviour`，配置 Options 并启用“Start 时初始化”。该组件提供 UI Toolkit 卡片式配置、构建和初始化操作。自定义 MonoBehaviour 也可以声明 `public YooAssetInitializationOptions options = new();`；该类型的 Drawer 使用 UI Toolkit，资源包名称只读并同步收集器。如果项目安装了 Tri Inspector，它会接管普通 MonoBehaviour 的 Inspector，默认通过 IMGUI 调用 Unity Drawer，导致显示 `No GUI Implemented`。在 options 字段上添加 `[TriInspector.DrawWithUnity(WithUiToolkit = true)]`，即可让 Tri Inspector 调用已有的 UI Toolkit Drawer。只有编辑器或 Player 至少一个运行模式为 `HostPlayMode/WebPlayMode` 时，基础配置才显示联网策略、清单和启动下载参数；两个模式都是离线/自定义模式时这些字段会隐藏，切换模式后会自动刷新。仅声明字段不会执行初始化，必须等待 `await YooAssetInitializer.InitializeAsync(options, cancellationToken);` 成功后再加载资源，取消令牌应绑定组件生命周期。

编辑器本地开发使用 `EditorPlayMode = EditorSimulateMode`。测试联网流程时将 **EditorPlayMode** 也设为 `HostPlayMode`；只修改 `RuntimePlayMode` 不影响编辑器运行。Player 联网通常设置 `RuntimePlayMode = HostPlayMode`，按需求选择 `RemoteOnly`（失败即停止）或 `RemoteThenCached`（缓存优先回退），并填写实际 CDN 地址。首次断网启动要回退包体资源时，构建时必须已将对应内置资源复制到包体。

`YooAssetInitializationStrategy` 有四种策略：

- `ManifestOnly`：只初始化并加载当前模式对应的清单，不自动下载整包；适合先检查更新，再由项目自行控制下载。
- `RemoteOnly`：必须访问远端；Host 按“请求远端版本 -> 加载清单 -> 下载全部缺失资源”执行，任一步失败都抛出异常；Web 只检查远端版本和清单，资源由 YooAsset 在使用时按需请求，失败不切换到旧缓存或包体内置版本。
- `RemoteThenCached`：按远端流程执行；失败后加载上一次完整下载成功的版本，并用下载器确认本地资源数量为零。没有可用缓存时会销毁联网包并尝试包体内置版本。
- `RemoteThenOffline`：按远端流程执行；任一步失败都等待销毁联网 package、移除同名注册，再以 `OfflinePlayMode` 加载包体内置资源。

这些策略只有在实际 `HostPlayMode`/`WebPlayMode` 下执行远端流程；编辑器模拟、本地离线和自定义模式沿用各自的初始化回调，不会因为 Player 配置了联网策略而失败。`WebPlayMode` 仅允许 `RemoteOnly`。

Host 模式的弱网策略会自动配置 YooAsset 官方建议的内置清单复制和 `InstallCleanupMode`。如果项目给沙盒文件系统指定了自定义根目录，同时填写 `CopyBuiltinPackageManifestDestRoot`，保证内置清单复制到同一清单目录。Host 版本只在清单成功且 `ResourceDownloaderOperation` 完整成功后写入 PlayerPrefs；因此远端版本文件可访问但资源下载不完整时不会污染离线回退版本。`WebPlayMode` 只支持 `RemoteOnly`，因为 Web 网络文件系统没有本地 package 回退语义，也不支持 Host 的下载无进度超时参数；Web 模式不会预下载全部资源或记录“完整下载版本”。

`DownloadMaximumConcurrency`、`DownloadRetryCount`、`DownloadMaxRequestPerFrame` 和 `DownloadNoProgressTimeoutSeconds` 分别控制 Host 启动下载并发、单文件重试、每帧请求节流和下载无新数据时的等待时间；Web 模式仍由 YooAsset 的网络文件系统按需加载，Host 专用的复制内置清单、缓存清理和下载参数不会参与 Web 初始化。主备服务器地址由 `IRemoteService`/`IRemoteServices` 负责逐文件选择。需要鉴权、签名或平台专属网络请求时，使用 `HostInitializationHandler` 或 `WebInitializationHandler` 接管文件系统参数。

一键初始化示例：

```csharp
using YokiFrame.Unity;
using YooAsset;

await YooAssetInitializer.InitializeAsync(new YooAssetInitializationOptions
{
    EditorPlayMode = EPlayMode.EditorSimulateMode,
    RuntimePlayMode = EPlayMode.HostPlayMode,
    InitializationStrategy = YooAssetInitializationStrategy.RemoteThenOffline,
    DefaultHostServer = "https://cdn.example.com/game",
    FallbackHostServer = "https://cdn-backup.example.com/game",
    EncryptionMode = YooAssetEncryptionMode.XorStream
});

var clip = ResKit.Load<UnityEngine.AudioClip>("Audio/Main");
```

需要自定义 YooAsset 文件系统时，可通过 `HostInitializationHandler` 或 `WebInitializationHandler` 提供完整初始化回调；如果回调使用自定义沙盒根目录，也要把 `CopyBuiltinPackageManifestDestRoot` 传给内置文件系统参数。项目重新初始化 package 前，先清理上一次初始化登记；package 的生命周期仍由项目负责。弱网回退路径会自行等待 `DestroyPackageAsync` 完成后再调用 `YooAssets.RemovePackage`，不会在同名 package 仍存活时重复创建。

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
