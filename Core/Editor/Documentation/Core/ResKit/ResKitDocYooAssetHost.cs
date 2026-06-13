#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 联机模式初始化文档
    /// </summary>
    internal static class ResKitDocYooAssetHost
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "联机模式初始化",
                Description = "支持热更新的联机模式（HostPlayMode）。推荐使用 YooInit.InitAsync 统一入口，版本差异由框架内部处理。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "统一初始化（推荐，版本无关）",
                        Code = @"// YooInit.InitAsync 自动处理 2.x / 3.x 版本差异
var config = new YooInitConfig
{
    PlayMode = EPlayMode.HostPlayMode,
    PackageNames = new List<string> { ""DefaultPackage"" }
};
await YooInit.InitAsync(config);

// 初始化完成后即可使用 ResKit 加载资源
var prefab = ResKit.Load<GameObject>(""Assets/GameRes/Prefabs/Player"");",
                        Explanation = "EPlayMode.HostPlayMode 会自动处理资源更新和清单下载。使用 YooInitConfig 在 Inspector 中配置即可，无需手写版本差异代码。"
                    },
                    new()
                    {
                        Title = "自定义远程服务（YooAsset 2.3.x）",
                        Code = @"// YooAsset 2.3.x 自定义 HostMode — 实现 IRemoteServices 接口
YooInit.HostModeHandler = (packageName, cfg) =>
{
    var hostParams = new HostPlayModeParameters
    {
        DefaultHostServer = ""https://cdn.example.com/bundles"",
        FallbackHostServer = ""https://cdn-backup.example.com/bundles""
    };
    return YooAssets.Initialize(hostParams);
};

var config = new YooInitConfig
{
    PlayMode = EPlayMode.HostPlayMode,
    PackageNames = new List<string> { ""DefaultPackage"" }
};
await YooInit.InitAsync(config);",
                        Explanation = "2.3.x 版本使用 HostPlayModeParameters + YooAssets.Initialize。设置 HostModeHandler 委托后 InitAsync 会自动调用。"
                    },
                    new()
                    {
                        Title = "自定义远程服务（YooAsset 3.x）",
                        Code = @"// YooAsset 3.x 自定义 HostMode — 实现 IRemoteService 接口
YooInit.HostModeHandler = (package, cfg) =>
{
    IRemoteService remoteService = new MyRemoteService(
        ""https://cdn.example.com/bundles"",
        ""https://cdn-backup.example.com/bundles"");

    var builtinParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
    var cacheParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);
    var options = new HostPlayModeOptions
    {
        BuiltinFileSystemParameters = builtinParams,
        CacheFileSystemParameters = cacheParams
    };
    return package.InitializePackageAsync(options);
};

var config = new YooInitConfig
{
    PlayMode = EPlayMode.HostPlayMode,
    PackageNames = new List<string> { ""DefaultPackage"" }
};
await YooInit.InitAsync(config);",
                        Explanation = "3.x 版本使用 HostPlayModeOptions + ResourcePackage.InitializePackageAsync。委托签名中的 package 参数为 ResourcePackage 类型。"
                    }
                }
            };
        }
    }
}
#endif
