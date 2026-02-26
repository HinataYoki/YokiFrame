#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 完整初始化示例文档 - 自定义模式
    /// </summary>
    internal static partial class ResKitDocYooAssetComplete
    {
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
    }
}
#endif
