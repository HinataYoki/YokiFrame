#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    // ResKit、LogKit、CodeGenKit 文档
    public partial class DocumentationToolPage
    {
        private DocModule CreateResKitDoc()
        {
            return new DocModule
            {
                Name = "ResKit",
                Icon = KitIcons.RESKIT,
                Category = "CORE KIT",
                Description = "资源管理工具，提供同步/异步加载、引用计数、资源缓存等功能。支持 UniTask 异步和自定义加载器扩展。",
                Keywords = new List<string> { "资源加载", "引用计数", "异步", "缓存" },
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "同步加载",
                        Description = "同步加载资源，适合小资源或加载界面。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "基本加载",
                                Code = @"// 加载资源
var prefab = ResKit.Load<GameObject>(""Prefabs/Player"");
var sprite = ResKit.Load<Sprite>(""Sprites/Icon"");
var clip = ResKit.Load<AudioClip>(""Audio/BGM"");

// 加载并实例化
var player = ResKit.Instantiate(""Prefabs/Player"", parent);

// 获取句柄（需要手动管理引用计数）
var handler = ResKit.LoadAsset<GameObject>(""Prefabs/Enemy"");
handler.Retain();  // 增加引用
handler.Release(); // 减少引用，引用为0时自动卸载"
                            }
                        }
                    },
                    new()
                    {
                        Title = "异步加载",
                        Description = "异步加载资源，避免阻塞主线程。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "回调方式",
                                Code = @"// 异步加载
ResKit.LoadAsync<GameObject>(""Prefabs/Boss"", prefab =>
{
    if (prefab != null)
    {
        Instantiate(prefab, spawnPoint);
    }
});

// 异步实例化
ResKit.InstantiateAsync(""Prefabs/Effect"", effect =>
{
    effect.transform.position = targetPos;
}, parent);"
                            },
                            new()
                            {
                                Title = "UniTask 方式",
                                Code = @"#if YOKIFRAME_UNITASK_SUPPORT
// 使用 UniTask 异步加载
var prefab = await ResKit.LoadUniTaskAsync<GameObject>(""Prefabs/Boss"");
var instance = Instantiate(prefab);

// 支持取消
var cts = new CancellationTokenSource();
try
{
    var sprite = await ResKit.LoadUniTaskAsync<Sprite>(""Sprites/Icon"", cts.Token);
}
catch (OperationCanceledException)
{
    Debug.Log(""加载已取消"");
}

// 异步实例化
var player = await ResKit.InstantiateUniTaskAsync(""Prefabs/Player"", parent);
#endif",
                                Explanation = "需要定义 YOKIFRAME_UNITASK_SUPPORT 宏启用 UniTask 支持。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "自定义加载器",
                        Description = "通过实现 IResLoaderPool 接口扩展加载方式，支持 YooAsset、Addressables 等。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "设置自定义加载池",
                                Code = @"// 切换到自定义加载池
ResKit.SetLoaderPool(new CustomLoaderPool());

// 获取当前加载池
var pool = ResKit.GetLoaderPool();

// 清理所有缓存
ResKit.ClearAll();"
                            }
                        }
                    },
                    new()
                    {
                        Title = "原始文件加载",
                        Description = "加载非 Unity 资源的原始文件（如 JSON、XML、二进制数据等）。默认使用 Resources/TextAsset 实现，支持 YooAsset 扩展。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "同步加载原始文件",
                                Code = @"// 加载文本文件（Resources 方式需放在 Resources 文件夹下）
string jsonText = ResKit.LoadRawFileText(""Config/settings"");
string xmlText = ResKit.LoadRawFileText(""Data/items"");

// 加载二进制数据
byte[] data = ResKit.LoadRawFileData(""Binary/model"");

// 获取原始文件路径（YooAsset 支持，Resources 返回 null）
string filePath = ResKit.GetRawFilePath(""Config/settings"");
if (filePath != null)
{
    // 可以直接使用文件路径进行 IO 操作
    using var stream = File.OpenRead(filePath);
}",
                                Explanation = "Resources 方式要求文件以 .txt/.bytes 等扩展名存储在 Resources 文件夹下。"
                            },
                            new()
                            {
                                Title = "异步加载原始文件",
                                Code = @"// 回调方式
ResKit.LoadRawFileTextAsync(""Config/settings"", text =>
{
    if (text != null)
    {
        var config = JsonUtility.FromJson<GameConfig>(text);
    }
});

ResKit.LoadRawFileDataAsync(""Binary/model"", data =>
{
    if (data != null)
    {
        ProcessBinaryData(data);
    }
});

#if YOKIFRAME_UNITASK_SUPPORT
// UniTask 方式（推荐）
var jsonText = await ResKit.LoadRawFileTextUniTaskAsync(""Config/settings"");
var config = JsonUtility.FromJson<GameConfig>(jsonText);

// 支持取消
var cts = new CancellationTokenSource();
var data = await ResKit.LoadRawFileDataUniTaskAsync(""Binary/model"", cts.Token);
#endif"
                            },
                            new()
                            {
                                Title = "自定义原始文件加载池",
                                Code = @"// 切换到自定义原始文件加载池
ResKit.SetRawFileLoaderPool(new CustomRawFileLoaderPool());

// 获取当前原始文件加载池
var pool = ResKit.GetRawFileLoaderPool();

#if YOKIFRAME_YOOASSET_SUPPORT
// 使用 YooAsset 原始文件加载池
var package = YooAssets.GetPackage(""DefaultPackage"");
ResKit.SetRawFileLoaderPool(new YooAssetRawFileLoaderUniTaskPool(package));

// YooAsset 方式加载原始文件
var jsonText = ResKit.LoadRawFileText(""Assets/GameRes/Config/settings.json"");
var filePath = ResKit.GetRawFilePath(""Assets/GameRes/Config/settings.json"");
#endif",
                                Explanation = "YooAsset 的原始文件加载支持获取实际文件路径，适合需要直接文件访问的场景。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "原始文件加载器接口",
                        Description = "实现 IRawFileLoader 和 IRawFileLoaderPool 接口可自定义原始文件加载方式。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "接口定义",
                                Code = @"// 原始文件加载器接口
public interface IRawFileLoader
{
    string LoadRawFileText(string path);
    byte[] LoadRawFileData(string path);
    void LoadRawFileTextAsync(string path, Action<string> onComplete);
    void LoadRawFileDataAsync(string path, Action<byte[]> onComplete);
    string GetRawFilePath(string path);
    void UnloadAndRecycle();
}

// 原始文件加载池接口
public interface IRawFileLoaderPool
{
    IRawFileLoader Allocate();
    void Recycle(IRawFileLoader loader);
}

#if YOKIFRAME_UNITASK_SUPPORT
// UniTask 扩展接口
public interface IRawFileLoaderUniTask : IRawFileLoader
{
    UniTask<string> LoadRawFileTextUniTaskAsync(string path, CancellationToken ct = default);
    UniTask<byte[]> LoadRawFileDataUniTaskAsync(string path, CancellationToken ct = default);
}
#endif",
                                Explanation = "通过实现这些接口，可以支持 Addressables、自定义文件系统等加载方式。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "YooAsset 集成概述",
                        Description = "YokiFrame 内置 YooAsset 支持，安装 YooAsset 包后自动启用 YOKIFRAME_YOOASSET_SUPPORT 宏。YooAsset 是一个功能强大的资源管理系统，支持资源热更新、分包下载等功能。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "架构说明",
                                Code = @"// YokiFrame 提供的 YooAsset 加载器类型：
// 
// 1. YooAssetResLoader        - 基础加载器，实现 IResLoader 接口
// 2. YooAssetResLoaderPool    - 基础加载池，管理 YooAssetResLoader
// 3. YooAssetResLoaderUniTask - UniTask 加载器，实现 IResLoaderUniTask 接口
// 4. YooAssetResLoaderUniTaskPool - UniTask 加载池（推荐）
//
// 使用流程：
// 1. 初始化 YooAsset 资源包
// 2. 创建对应的加载池
// 3. 调用 ResKit.SetLoaderPool() 切换加载池
// 4. 使用 ResKit API 加载资源（API 不变）",
                                Explanation = "ResKit 通过策略模式支持多种资源加载方式，切换加载池后 API 保持一致。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "编辑器模式初始化",
                        Description = "在编辑器中使用模拟模式，无需构建资源包即可测试。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "编辑器模拟模式",
                                Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
using YooAsset;

public class GameLauncher
{
    public async UniTask InitializeAsync()
    {
        // 1. 创建资源包
        var package = YooAssets.CreatePackage(""DefaultPackage"");
        YooAssets.SetDefaultPackage(package);

#if UNITY_EDITOR
        // 2. 编辑器模式：使用模拟构建
        var initParams = new EditorSimulateModeParameters();
        initParams.SimulateManifestFilePath = EditorSimulateModeHelper
            .SimulateBuild(EDefaultBuildPipeline.BuiltinBuildPipeline, ""DefaultPackage"");
        
        var initOp = package.InitializeAsync(initParams);
        await initOp.ToUniTask();
        
        if (initOp.Status != EOperationStatus.Succeed)
        {
            Debug.LogError($""YooAsset 初始化失败: {initOp.Error}"");
            return;
        }
#endif

        // 3. 切换 ResKit 加载池
        ResKit.SetLoaderPool(new YooAssetResLoaderUniTaskPool(package));
        
        Debug.Log(""YooAsset 初始化完成"");
    }
}
#endif",
                                Explanation = "编辑器模式下使用 EditorSimulateModeHelper.SimulateBuild 模拟资源包，无需实际构建。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "单机模式初始化",
                        Description = "单机游戏使用内置资源包，资源打包在安装包内。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "单机模式（OfflinePlayMode）",
                                Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
public async UniTask InitializeOfflineModeAsync()
{
    var package = YooAssets.CreatePackage(""DefaultPackage"");
    YooAssets.SetDefaultPackage(package);

    // 单机模式参数
    var initParams = new OfflinePlayModeParameters();
    initParams.BuildinFileSystemParameters = FileSystemParameters
        .CreateDefaultBuildinFileSystemParameters();

    var initOp = package.InitializeAsync(initParams);
    await initOp.ToUniTask();

    if (initOp.Status == EOperationStatus.Succeed)
    {
        ResKit.SetLoaderPool(new YooAssetResLoaderUniTaskPool(package));
        Debug.Log(""单机模式初始化成功"");
    }
}
#endif",
                                Explanation = "单机模式适合不需要热更新的游戏，资源全部打包在安装包内。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "联机模式初始化",
                        Description = "支持热更新的联机模式，可从服务器下载更新资源。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "联机模式（HostPlayMode）",
                                Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
public async UniTask InitializeHostModeAsync()
{
    var package = YooAssets.CreatePackage(""DefaultPackage"");
    YooAssets.SetDefaultPackage(package);

    // 联机模式参数
    var initParams = new HostPlayModeParameters();
    
    // 内置文件系统（StreamingAssets）
    initParams.BuildinFileSystemParameters = FileSystemParameters
        .CreateDefaultBuildinFileSystemParameters();
    
    // 缓存文件系统（下载的资源）
    initParams.CacheFileSystemParameters = FileSystemParameters
        .CreateDefaultCacheFileSystemParameters(new RemoteServices());

    var initOp = package.InitializeAsync(initParams);
    await initOp.ToUniTask();

    if (initOp.Status == EOperationStatus.Succeed)
    {
        // 更新资源版本
        await UpdatePackageVersionAsync(package);
        // 下载资源
        await DownloadPackageAsync(package);
        
        ResKit.SetLoaderPool(new YooAssetResLoaderUniTaskPool(package));
    }
}

// 远程服务配置
private class RemoteServices : IRemoteServices
{
    public string GetRemoteMainURL(string fileName)
    {
        return $""https://cdn.example.com/bundles/{fileName}"";
    }
    public string GetRemoteFallbackURL(string fileName)
    {
        return $""https://cdn-backup.example.com/bundles/{fileName}"";
    }
}
#endif",
                                Explanation = "联机模式支持资源热更新，需要配置远程服务器地址。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "资源更新流程",
                        Description = "联机模式下的资源版本检查和下载流程。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "版本更新和下载",
                                Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
// 更新资源版本
private async UniTask UpdatePackageVersionAsync(ResourcePackage package)
{
    var versionOp = package.RequestPackageVersionAsync();
    await versionOp.ToUniTask();
    
    if (versionOp.Status != EOperationStatus.Succeed)
    {
        Debug.LogError($""获取版本失败: {versionOp.Error}"");
        return;
    }
    
    var manifestOp = package.UpdatePackageManifestAsync(versionOp.PackageVersion);
    await manifestOp.ToUniTask();
    
    if (manifestOp.Status != EOperationStatus.Succeed)
    {
        Debug.LogError($""更新清单失败: {manifestOp.Error}"");
    }
}

// 下载资源
private async UniTask DownloadPackageAsync(ResourcePackage package)
{
    // 创建下载器
    int downloadingMaxNum = 10;  // 最大并发数
    int failedTryAgain = 3;      // 失败重试次数
    var downloader = package.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);
    
    if (downloader.TotalDownloadCount == 0)
    {
        Debug.Log(""没有需要下载的资源"");
        return;
    }
    
    // 显示下载信息
    Debug.Log($""需要下载 {downloader.TotalDownloadCount} 个文件，"" +
              $""总大小: {downloader.TotalDownloadBytes / 1024 / 1024:F2} MB"");
    
    // 开始下载
    downloader.BeginDownload();
    await downloader.ToUniTask();
    
    if (downloader.Status == EOperationStatus.Succeed)
    {
        Debug.Log(""资源下载完成"");
    }
    else
    {
        Debug.LogError($""资源下载失败: {downloader.Error}"");
    }
}
#endif",
                                Explanation = "热更新流程：请求版本 → 更新清单 → 下载资源 → 完成。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "使用 ResKit 加载资源",
                        Description = "切换加载池后，使用 ResKit 的统一 API 加载资源。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "加载资源示例",
                                Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
// 同步加载（YooAsset 使用完整路径）
var prefab = ResKit.Load<GameObject>(""Assets/GameRes/Prefabs/Player.prefab"");
var sprite = ResKit.Load<Sprite>(""Assets/GameRes/Sprites/Icon.png"");
var clip = ResKit.Load<AudioClip>(""Assets/GameRes/Audio/BGM.mp3"");

// 同步实例化
var player = ResKit.Instantiate(""Assets/GameRes/Prefabs/Player.prefab"", parent);

// 异步加载（回调方式）
ResKit.LoadAsync<GameObject>(""Assets/GameRes/Prefabs/Boss.prefab"", prefab =>
{
    if (prefab != null)
    {
        Object.Instantiate(prefab);
    }
});

// 异步加载（UniTask 方式，推荐）
var enemy = await ResKit.LoadUniTaskAsync<GameObject>(""Assets/GameRes/Prefabs/Enemy.prefab"");
var instance = Object.Instantiate(enemy);

// 异步实例化
var effect = await ResKit.InstantiateUniTaskAsync(""Assets/GameRes/Prefabs/Effect.prefab"", parent);
#endif",
                                Explanation = "YooAsset 默认使用完整的资源路径（Assets/...），建议将游戏资源放在统一目录如 Assets/GameRes/。"
                            },
                            new()
                            {
                                Title = "可寻址资源定位（Addressable）",
                                Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
// 在 YooAsset 构建时开启「可寻址资源定位」后，可以直接使用资源名加载
// 无需完整路径，YooAsset 会自动根据 Manifest 映射找到资源

// 使用资源名加载（开启可寻址后）
var prefab = ResKit.Load<GameObject>(""Player"");
var sprite = ResKit.Load<Sprite>(""Icon"");
var clip = ResKit.Load<AudioClip>(""BGM"");

// 异步加载
var boss = await ResKit.LoadUniTaskAsync<GameObject>(""Boss"");

// 两种方式都可以（开启可寻址后）
var player1 = ResKit.Load<GameObject>(""Player"");                        // 资源名
var player2 = ResKit.Load<GameObject>(""Assets/Prefabs/Player.prefab""); // 完整路径
#endif",
                                Explanation = "开启可寻址后，资源名必须唯一。建议使用有意义的命名规范，如 UI_MainMenu、Prefab_Player 等。"
                            },
                            new()
                            {
                                Title = "资源句柄管理",
                                Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
// 获取资源句柄（需要手动管理引用计数）
var handler = ResKit.LoadAsset<GameObject>(""Item"");

// 使用资源
var item = Object.Instantiate(handler.Asset as GameObject);

// 增加引用（如果需要长期持有）
handler.Retain();

// 释放引用（引用为0时自动卸载）
handler.Release();

// 清理所有缓存
ResKit.ClearAll();
#endif",
                                Explanation = "ResKit 使用引用计数管理资源生命周期，确保不再使用时调用 Release()。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "完整初始化示例",
                        Description = "根据运行环境自动选择初始化模式的完整示例。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "GameResourceManager",
                                Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;
using YokiFrame;

/// <summary>
/// 游戏资源管理器 - 封装 YooAsset 初始化和 ResKit 集成
/// </summary>
public class GameResourceManager
{
    private const string PACKAGE_NAME = ""DefaultPackage"";
    private ResourcePackage mPackage;
    
    public bool IsInitialized { get; private set; }
    
    public async UniTask InitializeAsync()
    {
        if (IsInitialized) return;
        
        mPackage = YooAssets.CreatePackage(PACKAGE_NAME);
        YooAssets.SetDefaultPackage(mPackage);
        
#if UNITY_EDITOR
        await InitEditorModeAsync();
#else
        await InitRuntimeModeAsync();
#endif
        
        // 切换 ResKit 加载池
        ResKit.SetLoaderPool(new YooAssetResLoaderUniTaskPool(mPackage));
        IsInitialized = true;
        
        Debug.Log(""[GameResourceManager] 初始化完成"");
    }
    
#if UNITY_EDITOR
    private async UniTask InitEditorModeAsync()
    {
        var initParams = new EditorSimulateModeParameters();
        initParams.SimulateManifestFilePath = EditorSimulateModeHelper
            .SimulateBuild(EDefaultBuildPipeline.BuiltinBuildPipeline, PACKAGE_NAME);
        
        var op = mPackage.InitializeAsync(initParams);
        await op.ToUniTask();
    }
#endif
    
    private async UniTask InitRuntimeModeAsync()
    {
        // 根据需求选择单机或联机模式
        var initParams = new OfflinePlayModeParameters();
        initParams.BuildinFileSystemParameters = FileSystemParameters
            .CreateDefaultBuildinFileSystemParameters();
        
        var op = mPackage.InitializeAsync(initParams);
        await op.ToUniTask();
    }
    
    public void Dispose()
    {
        ResKit.ClearAll();
        IsInitialized = false;
    }
}
#endif",
                                Explanation = "建议封装一个资源管理器类，统一处理初始化逻辑，便于维护和扩展。"
                            }
                        }
                    }
                }
            };
        }
        
        private DocModule CreateLogKitDoc()
        {
            return new DocModule
            {
                Name = "KitLogger",
                Icon = KitIcons.KITLOGGER,
                Category = "CORE KIT",
                Description = "高性能日志系统，支持日志级别控制、文件写入、加密存储、IMGUI 运行时显示。后台线程异步写入，不阻塞主线程。",
                Keywords = new List<string> { "日志", "调试", "文件写入", "异步" },
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "基本使用",
                        Description = "提供 Log、Warning、Error、Exception 四个级别的日志输出。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "输出日志",
                                Code = @"// 普通日志
KitLogger.Log(""游戏启动"");
KitLogger.Log($""玩家等级: {level}"");

// 警告
KitLogger.Warning(""配置文件缺失，使用默认值"");

// 错误
KitLogger.Error(""网络连接失败"");

// 异常
try
{
    // ...
}
catch (Exception ex)
{
    KitLogger.Exception(ex);
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "IMGUI 日志显示",
                        Description = "在打包后启用 IMGUI 日志窗口，实时查看运行时日志。支持日志过滤、折叠、自动滚动等功能。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "启用 IMGUI",
                                Code = @"// 启用 IMGUI 日志显示
KitLogger.EnableIMGUI();

// 指定最大日志条数
KitLogger.EnableIMGUI(maxLogCount: 500);

// 禁用 IMGUI
KitLogger.DisableIMGUI();

// 获取实例进行配置
var imgui = KitLogger.EnableIMGUI();
imgui.ShowTimestamp = true;    // 显示时间戳
imgui.AutoScroll = true;       // 自动滚动
imgui.WindowAlpha = 0.9f;      // 窗口透明度
imgui.Filter = KitLoggerIMGUI.LogTypeFilter.All; // 日志过滤"
                            }
                        }
                    },
                    new()
                    {
                        Title = "IMGUI 操作方式",
                        Description = "IMGUI 日志窗口支持多种交互方式，适配 PC 和移动端。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "交互操作",
                                Code = @"// === PC 端 ===
// 按 ` 键（数字1左边）切换窗口显示/隐藏

// === 移动端 ===
// 三指同时触摸切换窗口显示/隐藏

// === 窗口内操作 ===
// Clear      - 清空所有日志
// Collapse   - 合并重复日志
// AutoScroll - 自动滚动到最新日志
// Time       - 显示/隐藏时间戳
// Log/Warn/Error - 过滤日志类型
// X          - 关闭窗口

// === 自定义触发方式 ===
var imgui = KitLoggerIMGUI.Instance;
imgui.ToggleKey = KeyCode.F12;      // 修改触发按键
imgui.ToggleTouchCount = 4;         // 修改触发手指数"
                            }
                        }
                    },
                    new()
                    {
                        Title = "日志配置",
                        Description = "配置日志级别、文件写入、加密等选项。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "配置选项",
                                Code = @"// 设置日志级别
KitLogger.Level = KitLogger.LogLevel.All;     // 输出所有日志
KitLogger.Level = KitLogger.LogLevel.Warning; // 只输出 Warning 和 Error
KitLogger.Level = KitLogger.LogLevel.Error;   // 只输出 Error
KitLogger.Level = KitLogger.LogLevel.None;    // 关闭所有日志

// 启用文件写入（自动异步写入）
KitLogger.AutoEnableWriteLogToFile = true;

// 启用加密（保护敏感信息）
KitLogger.EnableEncryption = true;

// 编辑器中保存日志
KitLogger.SaveLogInEditor = true;

// 配置限制
KitLogger.MaxQueueSize = 20000;      // 最大队列大小
KitLogger.MaxSameLogCount = 50;      // 相同日志最大重复次数
KitLogger.MaxRetentionDays = 10;     // 日志保留天数
KitLogger.MaxFileBytes = 50 * 1024 * 1024; // 单文件最大 50MB"
                            }
                        }
                    },
                    new()
                    {
                        Title = "编辑器工具",
                        Description = "编辑器菜单提供日志目录打开和解密功能。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "菜单位置",
                                Code = @"// 菜单路径
// YokiFrame > KitLogger > 打开日志目录
// YokiFrame > KitLogger > 解密日志文件

// 日志文件位置
// Application.persistentDataPath/LogFiles/editor.log (编辑器)
// Application.persistentDataPath/LogFiles/player.log (运行时)"
                            }
                        }
                    },
                    new()
                    {
                        Title = "最佳实践",
                        Description = "推荐的 KitLogger 使用方式。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "初始化示例",
                                Code = @"public class GameLauncher : MonoBehaviour
{
    void Awake()
    {
        // 配置日志系统
        KitLogger.Level = KitLogger.LogLevel.All;
        KitLogger.EnableEncryption = true;
        
        // 仅在开发/测试版本启用 IMGUI
        #if DEVELOPMENT_BUILD || UNITY_EDITOR
        KitLogger.EnableIMGUI(300);
        #endif
        
        KitLogger.Log(""游戏启动"");
    }
}

// 使用条件编译控制日志级别
#if UNITY_EDITOR
    KitLogger.Level = KitLogger.LogLevel.All;
#elif DEVELOPMENT_BUILD
    KitLogger.Level = KitLogger.LogLevel.Warning;
#else
    KitLogger.Level = KitLogger.LogLevel.Error;
#endif"
                            }
                        }
                    }
                }
            };
        }
        
        private DocModule CreateCodeGenKitDoc()
        {
            return new DocModule
            {
                Name = "CodeGenKit",
                Icon = KitIcons.CODEGEN,
                Category = "CORE KIT",
                Description = "代码生成工具，提供结构化的代码生成 API。支持命名空间、类、方法等代码结构的生成。UIKit 的代码生成基于此实现。",
                Keywords = new List<string> { "代码生成", "自动化", "模板" },
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "核心概念",
                        Description = "CodeGenKit 使用 ICode 和 ICodeScope 接口构建代码树，最终通过 ICodeWriteKit 输出。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "核心接口",
                                Code = @"// ICode - 代码片段接口
public interface ICode
{
    void Gen(ICodeWriteKit writer);
}

// ICodeScope - 代码作用域接口（包含子代码）
public interface ICodeScope : ICode
{
    List<ICode> Codes { get; set; }
}

// ICodeWriteKit - 代码写入器接口
public interface ICodeWriteKit : IDisposable
{
    int IndentCount { get; set; }
    void WriteFormatLine(string format, params object[] args);
    void WriteLine(string code = null);
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "生成代码",
                        Description = "使用 RootCode 作为根节点，通过链式调用构建代码结构。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "生成类代码",
                                Code = @"var root = new RootCode();

// 添加 using
root.Codes.Add(new UsingCode(""System""));
root.Codes.Add(new UsingCode(""UnityEngine""));
root.Codes.Add(new EmptyLineCode());

// 添加命名空间
root.Namespace(""MyGame"", ns =>
{
    // 添加类
    ns.Class(""PlayerController"", ""MonoBehaviour"", 
        isPartial: true, isStatic: false, cls =>
    {
        // 添加字段
        cls.Codes.Add(new CustomCode(""public float Speed = 5f;""));
        cls.Codes.Add(new CustomCode(""public int Health = 100;""));
    });
});

// 输出到文件
using var writer = new FileCodeWriteKit(filePath);
root.Gen(writer);"
                            },
                            new()
                            {
                                Title = "生成的代码示例",
                                Code = @"using System;
using UnityEngine;

namespace MyGame
{
    public partial class PlayerController : MonoBehaviour
    {
        public float Speed = 5f;
        public int Health = 100;
    }
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "内置代码类型",
                        Description = "CodeGenKit 提供多种内置的代码类型。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "代码类型列表",
                                Code = @"// 基础代码
new UsingCode(""System"");           // using System;
new EmptyLineCode();                 // 空行
new OpenBraceCode();                 // {
new CloseBraceCode();                // }
new CustomCode(""// 注释"");         // 自定义代码

// 作用域代码
new NamespaceCodeScope(""MyGame"");  // namespace MyGame { }
new ClassCodeScope(""MyClass"", ""BaseClass"", isPartial, isStatic);
new CustomCodeScope(""if (condition)""); // 自定义作用域"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
