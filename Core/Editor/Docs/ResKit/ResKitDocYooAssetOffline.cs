#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 单机模式初始化文档
    /// </summary>
    internal static class ResKitDocYooAssetOffline
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
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
            };
        }
    }
}
#endif
