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
            };
        }
    }
}
#endif
