#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 完整初始化示例文档 - 拆分为多个子章节
    /// </summary>
    internal static class ResKitDocYooAssetComplete
    {
        /// <summary>
        /// 完整初始化示例 - 概述
        /// </summary>
        internal static DocSection CreateOverviewSection()
        {
            return new DocSection
            {
                Title = "完整初始化示例",
                Description = "使用 YooInit 一键初始化 YooAsset 并自动配置 ResKit/UIKit/SceneKit。\n\n" +
                              "核心特性：\n" +
                              "• API 统一命名：有 UniTask 时返回 UniTask，无 UniTask 时返回 IEnumerator\n" +
                              "• 编辑器/真机模式分离：打包时自动切换，无需手动修改\n" +
                              "• 智能包查找：自动定位资源所在包，无需手动指定\n" +
                              "• 多包支持：统一管理多个资源包，第一个为默认包",
                CodeExamples = new List<CodeExample>()
            };
        }

        /// <summary>
        /// YooInit 基础用法
        /// </summary>
        internal static DocSection CreateBasicSection()
        {
            return new DocSection
            {
                Title = "  YooInit 基础用法",
                Description = "API 名称统一为 InitAsync，根据是否有 UniTask 自动切换返回类型。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础初始化",
                        Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
using UnityEngine;
using YokiFrame;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Collections;
#endif

public class GameLauncher : MonoBehaviour
{
    [SerializeField] private YooInitConfig mConfig;
    
#if YOKIFRAME_UNITASK_SUPPORT
    // ========== UniTask 版本 ==========
    private async void Start()
    {
        // 初始化 YooAsset（返回 UniTask）
        await YooInit.InitAsync(mConfig);
        
        // 配置 UIKit 使用 YooAsset 加载面板
        YooInitUIKitExt.ConfigureUIKit();
        
        // 配置 SceneKit 场景切换时自动释放资源
        YooInitSceneKitExt.ConfigureSceneKit();
        
        Debug.Log(""YooAsset 初始化完成"");
    }
#else
    // ========== 协程版本 ==========
    private IEnumerator Start()
    {
        // 初始化 YooAsset（返回 IEnumerator）
        yield return YooInit.InitAsync(mConfig, () =>
        {
            // 配置 UIKit/SceneKit
            YooInitUIKitExt.ConfigureUIKit();
            YooInitSceneKitExt.ConfigureSceneKit();
            
            Debug.Log(""YooAsset 初始化完成"");
        });
    }
#endif
}
#endif",
                        Explanation = "InitAsync 方法名统一，编译时根据 YOKIFRAME_UNITASK_SUPPORT 宏自动选择实现。"
                    }
                }
            };
        }

        /// <summary>
        /// YooInit 配置详解
        /// </summary>
        internal static DocSection CreateConfigSection()
        {
            return new DocSection
            {
                Title = "  YooInit 配置详解",
                Description = "编辑器/真机模式分离，打包时自动切换。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "YooInitConfig 配置说明",
                        Code = @"var config = new YooInitConfig
{
    // ---------- 加载模式配置 ----------
    // 编辑器模式（仅编辑器下生效）
    // EditorSimulateMode: 模拟模式，无需构建资源包
    EditorPlayMode = EPlayMode.EditorSimulateMode,
    
    // 真机模式（打包后生效）
    // OfflinePlayMode: 离线模式，使用内置资源
    // HostPlayMode: 联机模式，支持热更新
    // WebPlayMode: WebGL 运行模式
    // CustomPlayMode: 自定义运行模式（需设置 YooInit.CustomHandler）
    RuntimePlayMode = EPlayMode.OfflinePlayMode,
    
    // ---------- 资源包配置 ----------
    // 资源包列表（第一个为默认包）
    PackageNames = new List<string>
    {
        ""DefaultPackage"",  // 默认包（用于 ResKit/UIKit）
        ""RawPackage"",      // 原始文件包（FMOD/配置文件等）
        ""DLCPackage""       // DLC 包
    },
    
    // ---------- 加密配置 ----------
    EncryptionType = YooEncryptionType.XorStream,
    XorKeySeed = ""MyCustomSeed_2025""
};

// PlayMode 属性会根据 UNITY_EDITOR 宏自动返回对应模式
// 编辑器下：返回 EditorPlayMode
// 真机下：返回 RuntimePlayMode
// 无需担心打包时忘记切换模式！
Debug.Log($""当前模式: {config.PlayMode}"");",
                        Explanation = "EditorPlayMode 和 RuntimePlayMode 分开配置，PlayMode 属性根据运行环境自动选择。真机模式不包含 EditorSimulateMode。"
                    },
                    new()
                    {
                        Title = "EPlayMode 模式说明",
                        Code = @"// EPlayMode 枚举值说明
public enum EPlayMode
{
    EditorSimulateMode,  // 编辑器模拟模式（仅编辑器可用）
    OfflinePlayMode,     // 离线运行模式（使用内置资源）
    HostPlayMode,        // 联机运行模式（支持热更新）
    WebPlayMode,         // WebGL 运行模式
    CustomPlayMode       // 自定义运行模式
}

// 真机下初始化模式可选值：
// - OfflinePlayMode: 单机游戏，资源打包在安装包内
// - HostPlayMode: 需要热更新，从 CDN 下载资源
// - WebPlayMode: WebGL 平台专用
// - CustomPlayMode: 完全自定义初始化逻辑",
                        Explanation = "真机模式下不能选择 EditorSimulateMode，Inspector 会自动过滤。"
                    }
                }
            };
        }

        /// <summary>
        /// YooInit 多包与智能查找
        /// </summary>
        internal static DocSection CreatePackageSection()
        {
            return new DocSection
            {
                Title = "  YooInit 多包与智能查找",
                Description = "自动定位资源所在包，无需手动指定。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "多包配置与智能查找",
                        Code = @"// ---------- 包访问方式 ----------
var defaultPkg = YooInit.DefaultPackage;           // 默认包（第一个）
var allPkgs = YooInit.Packages;                    // 所有包字典
var rawPkg = YooInit.GetPackage(""RawPackage"");   // 按名称获取

// 尝试获取（安全方式）
if (YooInit.TryGetPackage(""DLCPackage"", out var dlcPkg))
{
    Debug.Log(""DLC 包已加载"");
}

// ---------- 智能查找 ----------
// 自动遍历所有包，返回第一个包含该路径的包
var pkg = YooInit.FindPackageForPath(""Assets/Audio/BGM.bank"");

// 尝试查找（安全方式）
if (YooInit.TryFindPackageForPath(""Assets/Audio/BGM.bank"", out var audioPkg))
{
    Debug.Log($""音频资源在包: {audioPkg.PackageName}"");
}

// ---------- 原始文件加载（自动查找包）----------
#if YOKIFRAME_UNITASK_SUPPORT
var handle = await YooInit.LoadRawAsync(""Assets/Audio/BGM.bank"");
var data = handle.GetRawFileData();
handle.Release();
#else
yield return YooInit.LoadRawAsync(""Assets/Audio/BGM.bank"", handle =>
{
    var data = handle.GetRawFileData();
    handle.Release();
});
#endif

// ---------- 同步加载（两个版本通用）----------
byte[] data = YooInit.LoadRawFileData(""Assets/Audio/BGM.bank"");
string text = YooInit.LoadRawFileText(""Assets/Config/settings.json"");

// ---------- 按标签获取资源信息 ----------
var infos = YooInit.GetAssetInfosByTag(""Audio"");
foreach (var info in infos)
{
    Debug.Log($""资源: {info.AssetPath}"");
}",
                        Explanation = "FindPackageForPath 会遍历所有包，返回第一个包含该路径的包。LoadRawAsync 内部调用此方法。"
                    }
                }
            };
        }

        /// <summary>
        /// YooInit 加密配置
        /// </summary>
        internal static DocSection CreateEncryptionSection()
        {
            return new DocSection
            {
                Title = "  YooInit 加密配置",
                Description = "Inspector 会根据加密类型动态显示对应配置项。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "内置加密类型",
                        Code = @"// ---------- XOR 流式加密（推荐）----------
// 性能好，安全性适中
// 密钥种子通过 SHA256 生成 32 字节密钥
var xorConfig = new YooInitConfig
{
    EncryptionType = YooEncryptionType.XorStream,
    XorKeySeed = ""MySecretSeed_2025!@#$""
};

// ---------- 文件偏移 ----------
// 仅防止直接打开，无实际加密
// 适合简单防护场景
var offsetConfig = new YooInitConfig
{
    EncryptionType = YooEncryptionType.FileOffset,
    FileOffset = 64  // 可选：16/32/64/128/256/512/1024（2 的幂）
};

// ---------- AES 加密 ----------
// 安全性高，但性能开销大
// 密码和盐值通过 PBKDF2 派生密钥
var aesConfig = new YooInitConfig
{
    EncryptionType = YooEncryptionType.Aes,
    AesPassword = ""MySecretPassword"",
    AesSalt = ""MySalt88""  // 至少 8 字节
};

// ---------- 获取加密/解密服务 ----------
IEncryptionServices encryptor = config.CreateEncryptionServices();
IDecryptionServices decryptor = config.CreateDecryptionServices();",
                        Explanation = "Inspector 使用 UIToolkit 绘制，FileOffset 使用下拉选择确保值为 2 的幂。"
                    },
                    new()
                    {
                        Title = "自定义加密",
                        Code = @"// ============================================================
// 自定义加密方案
// 通过静态委托注册自定义加密/解密服务
// ============================================================

// ---------- 1. 配置使用自定义加密类型 ----------
var config = new YooInitConfig
{
    EncryptionType = YooEncryptionType.Custom
};

// ---------- 2. 注册自定义加密服务工厂（构建时使用）----------
YooInitConfig.CustomEncryptionFactory = cfg => new MyCustomEncryption();

// ---------- 3. 注册自定义解密服务工厂（运行时使用）----------
YooInitConfig.CustomDecryptionFactory = cfg => new MyCustomDecryption();

// ---------- 4. 初始化 YooAsset ----------
await YooInit.InitAsync(config);",
                        Explanation = "自定义加密需要在调用 InitAsync 或 CreateEncryptionServices/CreateDecryptionServices 前注册工厂委托。"
                    },
                    new()
                    {
                        Title = "自定义加密类实现",
                        Code = @"using System.IO;
using YooAsset;

/// <summary>
/// 自定义加密服务（构建时使用）
/// </summary>
public class MyCustomEncryption : IEncryptionServices
{
    /// <summary>
    /// 加密文件
    /// </summary>
    public EncryptResult Encrypt(EncryptFileInfo fileInfo)
    {
        // 读取原始文件数据
        var fileData = File.ReadAllBytes(fileInfo.FilePath);
        
        // 执行自定义加密算法
        var encryptedData = MyEncryptAlgorithm(fileData);
        
        // 返回加密结果
        return new EncryptResult
        {
            Encrypted = true,
            EncryptedData = encryptedData
        };
    }
    
    private byte[] MyEncryptAlgorithm(byte[] data)
    {
        // 实现自定义加密逻辑
        // 示例：简单的字节翻转
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ 0xFF);
        }
        return result;
    }
}

/// <summary>
/// 自定义解密服务（运行时使用）
/// </summary>
public class MyCustomDecryption : IDecryptionServices
{
    /// <summary>
    /// 获取 Bundle 文件的加载方法
    /// </summary>
    public AssetBundle LoadAssetBundle(DecryptFileInfo fileInfo, out Stream managedStream)
    {
        // 读取加密文件
        var encryptedData = File.ReadAllBytes(fileInfo.FileLoadPath);
        
        // 执行解密
        var decryptedData = MyDecryptAlgorithm(encryptedData);
        
        // 从内存加载 AssetBundle
        managedStream = null;
        return AssetBundle.LoadFromMemory(decryptedData);
    }
    
    /// <summary>
    /// 异步获取 Bundle 文件的加载方法
    /// </summary>
    public AssetBundleCreateRequest LoadAssetBundleAsync(DecryptFileInfo fileInfo, out Stream managedStream)
    {
        var encryptedData = File.ReadAllBytes(fileInfo.FileLoadPath);
        var decryptedData = MyDecryptAlgorithm(encryptedData);
        
        managedStream = null;
        return AssetBundle.LoadFromMemoryAsync(decryptedData);
    }
    
    /// <summary>
    /// 获取原始文件的字节数据
    /// </summary>
    public byte[] ReadFileData(DecryptFileInfo fileInfo)
    {
        var encryptedData = File.ReadAllBytes(fileInfo.FileLoadPath);
        return MyDecryptAlgorithm(encryptedData);
    }
    
    /// <summary>
    /// 获取原始文件的文本数据
    /// </summary>
    public string ReadFileText(DecryptFileInfo fileInfo)
    {
        var data = ReadFileData(fileInfo);
        return System.Text.Encoding.UTF8.GetString(data);
    }
    
    private byte[] MyDecryptAlgorithm(byte[] data)
    {
        // 实现自定义解密逻辑（与加密算法对应）
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ 0xFF);
        }
        return result;
    }
}",
                        Explanation = "IEncryptionServices 用于构建时加密资源，IDecryptionServices 用于运行时解密资源。两者的算法必须对应。"
                    },
                    new()
                    {
                        Title = "自定义加密完整流程",
                        Code = @"// ============================================================
// 自定义加密完整流程示例
// ============================================================

public class GameBoot : MonoBehaviour
{
    [SerializeField] private YooInitConfig mConfig;
    
    private async void Start()
    {
        // 1. 注册自定义解密服务（运行时只需要解密）
        YooInitConfig.CustomDecryptionFactory = cfg => new MyCustomDecryption();
        
        // 2. 初始化 YooAsset
        await YooInit.InitAsync(mConfig);
        
        // 3. 正常使用资源加载
        YooInitUIKitExt.ConfigureUIKit();
        YooInitSceneKitExt.ConfigureSceneKit();
    }
}

// ============================================================
// 构建管线中使用自定义加密
// ============================================================
#if UNITY_EDITOR
public static class BuildPipelineHelper
{
    public static void BuildWithCustomEncryption()
    {
        var config = new YooInitConfig
        {
            EncryptionType = YooEncryptionType.Custom
        };
        
        // 注册自定义加密服务
        YooInitConfig.CustomEncryptionFactory = cfg => new MyCustomEncryption();
        
        // 获取加密服务用于构建
        var encryptionServices = config.CreateEncryptionServices();
        
        // 在 YooAsset 构建参数中使用
        var buildParams = new BuildParameters
        {
            // ... 其他参数
            EncryptionServices = encryptionServices
        };
    }
}
#endif",
                        Explanation = "运行时只需注册 CustomDecryptionFactory，构建时需要注册 CustomEncryptionFactory。"
                    }
                }
            };
        }

        /// <summary>
        /// YooInit 资源管理
        /// </summary>
        internal static DocSection CreateResourceSection()
        {
            return new DocSection
            {
                Title = "  YooInit 资源管理",
                Description = "资源卸载、路径检查、状态查询。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "资源管理",
                        Code = @"// ---------- 卸载未使用资源 ----------
// 遍历所有包释放未使用资源
#if YOKIFRAME_UNITASK_SUPPORT
await YooInit.UnloadUnusedAssetsAsync();
#else
yield return YooInit.UnloadUnusedAssetsAsync();
#endif

// ---------- 路径有效性检查 ----------
// 检查指定包
bool valid = YooInit.CheckPathValid(""Assets/Prefabs/Player.prefab"", specificPkg);

// 遍历所有包检查
bool validAny = YooInit.CheckPathValid(""Assets/Prefabs/Player.prefab"");

// ---------- 状态查询 ----------
bool initialized = YooInit.Initialized;
int packageCount = YooInit.Packages.Count;

// ---------- 销毁并重置 ----------
// 清理 ResKit、释放所有包、重置状态
YooInit.Dispose();",
                        Explanation = "Dispose 会清理 ResKit 加载器、释放所有包引用、重置初始化状态。"
                    }
                }
            };
        }

        /// <summary>
        /// YooInit 完整启动流程
        /// </summary>
        internal static DocSection CreateBootSection()
        {
            return new DocSection
            {
                Title = "  YooInit 完整启动流程",
                Description = "推荐的启动流程示例。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "完整启动流程示例",
                        Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
using UnityEngine;
using YokiFrame;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

public class Boot : MonoBehaviour
{
    [SerializeField] private YooInitConfig mYooConfig;
    
#if YOKIFRAME_UNITASK_SUPPORT
    private async void Start()
    {
        // 1. 初始化 YooAsset
        await YooInit.InitAsync(mYooConfig);
        
        // 2. 配置 UIKit
        YooInitUIKitExt.ConfigureUIKit();
        
        // 3. 配置 SceneKit
        YooInitSceneKitExt.ConfigureSceneKit();
        
        // 4. 初始化其他系统（如 FMOD）
        await InitFMOD();
        
        // 5. 加载主场景
        await SceneSystem.AsyncToNextSceneAwait(1001);
    }
    
    private async UniTask InitFMOD()
    {
        // 使用智能查找加载 FMOD Bank
        var handle = await YooInit.LoadRawAsync(""Assets/Audio/Master.bank"");
        FMODUnity.RuntimeManager.LoadBank(handle.GetRawFileData());
        handle.Release();
    }
#endif
}
#endif",
                        Explanation = "推荐的启动流程：YooInit → UIKit → SceneKit → 其他系统 → 主场景。"
                    }
                }
            };
        }

        /// <summary>
        /// YooInit 自定义模式
        /// </summary>
        internal static DocSection CreateCustomModeSection()
        {
            return new DocSection
            {
                Title = "  YooInit 自定义模式",
                Description = "HostPlayMode/WebPlayMode 需要用户配置远程服务。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "自定义初始化模式（HostPlayMode）",
                        Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
using UnityEngine;
using YokiFrame;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

public class HotUpdateBoot : MonoBehaviour
{
    [SerializeField] private YooInitConfig mConfig;
    [SerializeField] private string mCdnUrl = ""http://cdn.example.com"";
    [SerializeField] private string mFallbackUrl = ""http://cdn-fallback.example.com"";
    
#if YOKIFRAME_UNITASK_SUPPORT
    private async void Start()
    {
        // ---------- 设置 HostModeHandler 委托 ----------
        YooInit.HostModeHandler = CreateHostModeOperation;
        
        // 初始化（RuntimePlayMode 设为 HostPlayMode）
        await YooInit.InitAsync(mConfig);
        
        // 后续配置...
        YooInitUIKitExt.ConfigureUIKit();
    }
    
    private InitializationOperation CreateHostModeOperation(
        ResourcePackage package, YooInitConfig config)
    {
        var remoteServices = new RemoteServices(mCdnUrl, mFallbackUrl);
        
        var cacheParams = FileSystemParameters.CreateDefaultCacheFileSystemParameters(
            remoteServices, 
            config.CreateDecryptionServices());
        
        var buildinParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(
            config.CreateDecryptionServices());
        
        return package.InitializeAsync(new HostPlayModeParameters
        {
            BuildinFileSystemParameters = buildinParams,
            CacheFileSystemParameters = cacheParams
        });
    }
#endif
}

public class RemoteServices : IRemoteServices
{
    private readonly string mDefaultUrl;
    private readonly string mFallbackUrl;
    
    public RemoteServices(string defaultUrl, string fallbackUrl)
    {
        mDefaultUrl = defaultUrl;
        mFallbackUrl = fallbackUrl;
    }
    
    public string GetRemoteMainURL(string fileName) => $""{mDefaultUrl}/{fileName}"";
    public string GetRemoteFallbackURL(string fileName) => $""{mFallbackUrl}/{fileName}"";
}
#endif",
                        Explanation = "HostPlayMode 需要在调用 InitAsync 前设置 YooInit.HostModeHandler 委托。"
                    },
                    new()
                    {
                        Title = "通用自定义处理器",
                        Code = @"// 设置通用处理器后，将覆盖所有模式的默认实现
YooInit.CustomHandler = (package, config) =>
{
    // 根据包名或模式自定义初始化逻辑
    if (package.PackageName == ""DLCPackage"")
    {
        // DLC 包使用特殊的远程服务
        var remoteServices = new DLCRemoteServices();
        var cacheParams = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices);
        return package.InitializeAsync(new HostPlayModeParameters
        {
            CacheFileSystemParameters = cacheParams
        });
    }
    
    // 其他包使用默认离线模式
    var buildinParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(
        config.CreateDecryptionServices());
    return package.InitializeAsync(new OfflinePlayModeParameters
    {
        BuildinFileSystemParameters = buildinParams
    });
};

// 初始化
await YooInit.InitAsync(config);

// 重置所有委托
YooInit.Reset();",
                        Explanation = "CustomHandler 优先级最高，设置后将覆盖所有模式的默认实现。Reset() 会清理所有委托。"
                    },
                    new()
                    {
                        Title = "CustomPlayMode 自定义运行模式",
                        Code = @"// ============================================================
// CustomPlayMode 自定义运行模式
// 当 RuntimePlayMode 设为 CustomPlayMode 时，必须设置 CustomHandler
// ============================================================

var config = new YooInitConfig
{
    EditorPlayMode = EPlayMode.EditorSimulateMode,
    RuntimePlayMode = EPlayMode.CustomPlayMode  // 使用自定义运行模式
};

// 设置自定义处理器（必须在 InitAsync 前设置）
YooInit.CustomHandler = (package, cfg) =>
{
    // 完全自定义的初始化逻辑
    // 例如：根据平台选择不同的初始化方式
    
#if UNITY_ANDROID
    // Android 平台使用 OBB 扩展文件
    var obbParams = CreateObbFileSystemParameters();
    return package.InitializeAsync(new OfflinePlayModeParameters
    {
        BuildinFileSystemParameters = obbParams
    });
#elif UNITY_IOS
    // iOS 平台使用标准内置资源
    var buildinParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(
        cfg.CreateDecryptionServices());
    return package.InitializeAsync(new OfflinePlayModeParameters
    {
        BuildinFileSystemParameters = buildinParams
    });
#else
    // 其他平台
    var defaultParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(
        cfg.CreateDecryptionServices());
    return package.InitializeAsync(new OfflinePlayModeParameters
    {
        BuildinFileSystemParameters = defaultParams
    });
#endif
};

// 初始化
await YooInit.InitAsync(config);",
                        Explanation = "CustomPlayMode 适合需要完全自定义初始化逻辑的场景，如多平台差异化处理。"
                    }
                }
            };
        }

        /// <summary>
        /// 获取所有子章节（兼容旧接口）
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                CreateOverviewSection(),
                CreateBasicSection(),
                CreateConfigSection(),
                CreatePackageSection(),
                CreateEncryptionSection(),
                CreateResourceSection(),
                CreateBootSection(),
                CreateCustomModeSection()
            };
        }

        /// <summary>
        /// 兼容旧接口 - 返回概述章节
        /// </summary>
        internal static DocSection CreateSection() => CreateOverviewSection();
    }
}
#endif
