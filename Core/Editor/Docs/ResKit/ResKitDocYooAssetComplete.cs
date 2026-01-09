#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 完整初始化示例文档
    /// </summary>
    internal static class ResKitDocYooAssetComplete
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "完整初始化示例",
                Description = "根据运行环境自动选择初始化模式的完整示例。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "GameResourceManager",
                        Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
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
        
        ResKit.SetLoaderPool(new YooAssetResLoaderUniTaskPool(mPackage));
        IsInitialized = true;
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
            };
        }
    }
}
#endif
