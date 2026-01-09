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
            };
        }
    }
}
#endif
