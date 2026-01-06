# ResKit - YooAsset 集成指南

ResKit 支持 YooAsset 作为资源加载后端，提供高效的资源管理功能。

## 快速开始

### 1. 安装 YooAsset

通过 Package Manager 安装：
```
com.tuyoogame.yooasset
```

### 2. 启用 YooAsset 支持

在 **Project Settings > Player > Other Settings > Scripting Define Symbols** 添加：

```
YOKIFRAME_YOOASSET_SUPPORT
```

### 3. 初始化

```csharp
// 获取 YooAsset 资源包
var package = YooAssets.GetPackage("DefaultPackage");

// 创建 YooAsset 加载器池
var loaderPool = new YooAssetResLoaderPool(package);

// 设置为 ResKit 的加载器池
ResKit.SetLoaderPool(loaderPool);
```

## 运行模式

### Editor 模式（开发阶段）

```csharp
var initParams = new EditorSimulateModeParameters();
initParams.SimulateManifestFilePath = EditorSimulateModeHelper
    .SimulateBuild(EDefaultBuildPipeline.BuiltinBuildPipeline, "DefaultPackage");
await package.InitializeAsync(initParams);
```

### Offline 模式（单机游戏）

```csharp
var initParams = new OfflinePlayModeParameters();
await package.InitializeAsync(initParams);
```

### Host 模式（网络游戏）

```csharp
var initParams = new HostPlayModeParameters();
initParams.BuildinQueryServices = new GameQueryServices();
initParams.RemoteServices = new RemoteServices(hostServerURL, fallbackURL);
await package.InitializeAsync(initParams);
```

## 基本用法

### 同步加载

```csharp
// 使用可寻址路径
var handler = ResKit.LoadAsset<GameObject>("Assets/Prefabs/Player.prefab");
var prefab = handler.Asset;

// 使用完毕后释放
handler.Release();
```

### 异步加载

```csharp
// 回调方式
ResKit.LoadAssetAsync<Sprite>("Assets/Sprites/Icon.png", handler =>
{
    var sprite = handler.Asset;
    // 使用资源...
});

// UniTask 方式
var handler = await ResKit.LoadAssetUniTaskAsync<AudioClip>("Assets/Audio/BGM.mp3");
var clip = handler.Asset;
```

### 资源名定位

如果 YooAsset 开启了资源名定位（Addressable），可以直接使用资源名：

```csharp
// 直接使用资源名（需要在 YooAsset 中配置 Addressable）
var handler = ResKit.LoadAsset<GameObject>("Player");
```

## 资源更新流程

```csharp
// 1. 更新资源版本
var versionOp = package.RequestPackageVersionAsync();
await versionOp;
var version = versionOp.PackageVersion;

// 2. 更新资源清单
var manifestOp = package.UpdatePackageManifestAsync(version);
await manifestOp;

// 3. 下载资源
var downloader = package.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);
if (downloader.TotalDownloadCount > 0)
{
    downloader.BeginDownload();
    await downloader;
}
```

## 架构说明

```
ResKit (门面)
    │
    ├── IResLoaderPool (加载器池接口)
    │       ├── DefaultResLoaderPool (Unity Resources)
    │       └── YooAssetResLoaderPool (YooAsset)
    │
    └── IResLoader (加载器接口)
            ├── DefaultResLoader
            └── YooAssetResLoader
```

## 注意事项

1. **资源路径** - YooAsset 默认使用完整路径，开启 Addressable 后可用资源名
2. **资源释放** - 必须调用 `handler.Release()` 释放资源引用
3. **包管理** - 支持多个 ResourcePackage，按需创建不同的 LoaderPool
4. **热更新** - Host 模式支持资源热更新，需配置远程服务器
