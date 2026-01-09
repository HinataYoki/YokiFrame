#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 编辑器模式初始化文档
    /// </summary>
    internal static class ResKitDocYooAssetEditor
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
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
            };
        }
    }
}
#endif
