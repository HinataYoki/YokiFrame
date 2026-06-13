#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 资源更新流程文档
    /// </summary>
    internal static class ResKitDocYooAssetUpdate
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "资源更新流程",
                Description = "联机模式下的资源版本检查和下载。YooInit.InitAsync 自动处理更新流程，以下为手动控制版本。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "自动更新（推荐）",
                        Code = @"// YooInit.InitAsync 自动完成：请求版本 → 更新清单 → 下载资源
var config = new YooInitConfig
{
    PlayMode = EPlayMode.HostPlayMode,
    PackageNames = new List<string> { ""DefaultPackage"" }
};
await YooInit.InitAsync(config);
// 资源已就绪，直接使用",
                        Explanation = "HostPlayMode 下 InitAsync 会自动执行完整的热更新流程。无需手动调用版本请求和资源下载。"
                    },
                    new()
                    {
                        Title = "手动更新流程（YooAsset 2.3.x）",
                        Code = @"// YooAsset 2.3.x 手动控制更新
// 1. 请求资源版本
var versionOp = YooAssets.RequestPackageVersionAsync();
await versionOp.ToUniTask();

// 2. 更新资源清单
var manifestOp = YooAssets.UpdatePackageManifestAsync(
    versionOp.PackageVersion);
await manifestOp.ToUniTask();

// 3. 创建下载器
int maxConcurrent = 10;
int retryCount = 3;
var downloader = YooAssets.CreateResourceDownloader(
    maxConcurrent, retryCount);

// 4. 下载资源
downloader.BeginDownload();
await downloader.ToUniTask();

// 5. 检查结果
if (downloader.Status == EOperationStatus.Succeed)
    Debug.Log($""下载完成: {downloader.TotalDownloadCount} 个文件"");",
                        Explanation = "2.3.x 版本手动管理：YooAssets.RequestPackageVersionAsync → UpdatePackageManifestAsync → CreateResourceDownloader → BeginDownload。"
                    },
                    new()
                    {
                        Title = "手动更新流程（YooAsset 3.x）",
                        Code = @"// YooAsset 3.x 手动控制更新
var package = YooInit.DefaultPackage;

// 1. 请求资源版本
var versionOp = package.RequestPackageVersionAsync();
await versionOp.ToUniTask();

// 2. 更新资源清单
var manifestOptions = new LoadPackageManifestOptions(
    versionOp.PackageVersion, timeout: 60);
var manifestOp = package.LoadPackageManifestAsync(manifestOptions);
await manifestOp.ToUniTask();

// 3. 创建下载器
int maxConcurrent = 10;
int retryCount = 3;
var downloader = package.CreateResourceDownloader(
    maxConcurrent, retryCount);

// 4. 开始下载
downloader.BeginDownload();
await downloader.ToUniTask();

// 5. 检查结果
if (downloader.Status == EOperationStatus.Succeed)
    Debug.Log($""下载完成: {downloader.TotalDownloadCount} 个文件"");",
                        Explanation = "3.x 版本手动管理：package.RequestPackageVersionAsync → LoadPackageManifestAsync(LoadPackageManifestOptions) → CreateResourceDownloader → BeginDownload。与 2.3.x 主要区别在清单加载 API。"
                    }
                }
            };
        }
    }
}
#endif
