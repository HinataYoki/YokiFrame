# ResKit 资源

## 什么时候先看这里

| 问题 | 看本页 |
|---|---|
| `ResKit provider is not configured` | 配置 Provider。 |
| 想从 Unity Resources 切到 YooAsset | 替换 Provider。 |
| 资源引用计数不归零 | 检查 `LoadAsset<T>()` 句柄释放。 |
| raw 文件读取失败 | 确认 Provider 实现 `IRawResourceProvider`。 |
| SceneKit / UIKit 加载路径混乱 | 确认它们是否跟随 ResKit Provider。 |

## 配置 Provider

Unity 默认 Provider：

```csharp
using YokiFrame;
using YokiFrame.Unity;

ResKit.SetProvider(new UnityResourceProvider());
```

统一初始化也会安装默认后端：

```csharp
YokiFrameKit.Initialize(YokiFrameEngine.Unity);
```

YooAsset 项目仍然只替换 ResKit Provider：

```csharp
#if YOKIFRAME_YOOASSET_SUPPORT
ResKit.SetProvider(new YooAssetResourceProvider());
#endif
```

自定义资源系统实现 `IResourceProvider` 后注入：

```csharp
ResKit.SetProvider(new ProjectResourceProvider());
```

`SetProvider()` 会清空当前缓存。不要在运行中随意切换 Provider。

## 加载资源

只拿资源对象：

```csharp
var icon = ResKit.Load<Sprite>("Sprites/Icon");
```

需要明确生命周期：

```csharp
var handle = ResKit.LoadAsset<Sprite>("Sprites/Icon");
try
{
    Use(handle.Asset);
}
finally
{
    handle.Release();
}
```

相同 `path + T` 会复用缓存并增加引用计数。句柄释放到 `RefCount == 0` 时，ResKit 移除缓存并调用 Provider 的 `Release(asset)`。

## 异步与 raw

异步 API 默认返回 `Task<T>`；启用 `YOKIFRAME_UNITASK_SUPPORT` 后返回 `UniTask<T>`。

```csharp
var handle = await ResKit.LoadAssetAsync<MyConfig>("Configs/GameConfig", token);
```

raw 文件：

```csharp
var bytes = ResKit.LoadRaw("Configs/GameConfig");
var text = ResKit.LoadRawText("Configs/GameConfig");
```

Provider 不实现 `IRawResourceProvider` 时 raw 读取会失败。

## SceneKit 和 UIKit

内置 `UnityResourceProvider` 和 `YooAssetResourceProvider` 同时提供 asset、raw 和 scene 能力。切换 Provider 后：

| 模块 | 默认行为 |
|---|---|
| SceneKit | 跟随当前 ResKit Provider 的场景后端。 |
| UIKit | 默认加载器通过 `ResKit.LoadAsset<GameObject>()` 加载面板。 |
| TableKit 生成代码 | 默认通过 `ResKit.LoadRaw()` / `LoadRawText()` 读取配置表。 |

YooAsset 面板如果使用类型名作为 location：

```csharp
ResKit.SetProvider(new YooAssetResourceProvider());
UIKit.GetPanelLoader().UseAddressableLocation = true;
```

## 诊断 API

```csharp
Debug.Log(ResKit.ProviderName);
Debug.Log(ResKit.LoadedCount);
Debug.Log(ResKit.TotalRefCount);

var loaded = new List<ResDebugInfo>();
ResKit.GetLoadedAssets(loaded);
```

需要定位加载来源时临时开启：

```csharp
ResKit.EnableLoadLocationTracking = true;
```

已缓存资源不会补录位置，需要释放后重新加载。

## 工作台诊断

ResKit 页面用于查看 Provider、已加载资源、引用计数、卸载历史和加载来源。

| 在工作台里看什么 | 用途 |
|---|---|
| Provider | 确认当前使用 Resources、YooAsset 还是项目自定义后端。 |
| 已加载资源列表 | 找到未释放或重复加载的资源。 |
| RefCount | 判断句柄是否都正确 Release。 |
| 卸载历史 | 查看资源是否按预期释放。 |
| 加载来源 | 定位是谁加载了长期持有的资源。 |

资源不释放时，先按 RefCount 排序找高引用资源，再打开加载来源；加载来源为空时，先启用加载位置采集并重新触发加载。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| Provider 未配置 | 初始化宿主或手动 `ResKit.SetProvider(...)`。 |
| 资源不释放 | 所有 `LoadAsset<T>()` 句柄都要 `Release()`。 |
| raw 不支持 | Provider 需要实现 `IRawResourceProvider`。 |
| YooAsset UI 找不到面板 | 检查面板路径模式或开启 `UseAddressableLocation`。 |
| 加载位置为空 | 先开启 `EnableLoadLocationTracking`，再重新触发加载。 |
