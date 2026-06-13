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
                Description = "在 Unity 编辑器中使用模拟模式测试，无需实际构建资源包。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "统一初始化（推荐）",
                        Code = @"// Editor 下自动使用模拟模式，无需手写版本代码
var config = new YooInitConfig
{
    PlayMode = EPlayMode.EditorSimulateMode,
    PackageNames = new List<string> { ""DefaultPackage"" }
};
await YooInit.InitAsync(config);

// 正常使用 ResKit
var prefab = ResKit.Load<GameObject>(""Assets/GameRes/Prefabs/Player"");",
                        Explanation = "EPlayMode.EditorSimulateMode 下框架自动处理模拟构建。3.x 使用 EditorSimulateBuildInvoker.Build()，2.3.x 使用 EditorSimulateModeHelper.SimulateBuild()，版本差异由框架内部封装。"
                    },
                    new()
                    {
                        Title = "自定义初始化（2.3.x / 3.x 通用）",
                        Code = @"// 如需自定义编辑器模式的初始化逻辑（两个版本均支持）
// YooAsset 2.3.x:
YooInit.CustomHandler = (packageName, cfg) =>
{
    var simulateParams = new EditorSimulateModeParameters();
    simulateParams.SimulateManifestFilePath =
        EditorSimulateModeHelper.SimulateBuild(packageName);
    return YooAssets.Initialize(simulateParams);
};

// YooAsset 3.x:
YooInit.CustomHandler = (package, cfg) =>
{
    var buildResult = EditorSimulateBuildInvoker.Build(
        package.PackageName, (int)EBundleType.VirtualAssetBundle);
    var fileSystemParams = FileSystemParameters
        .CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
    var options = new EditorSimulateModeOptions
    {
        EditorFileSystemParameters = fileSystemParams
    };
    return package.InitializePackageAsync(options);
};

// 仅在不满足默认行为时才需要设置 CustomHandler
await YooInit.InitAsync(config);",
                        Explanation = "绝大多数场景直接使用 YooInit.InitAsync 即可。仅在不满足默认模拟行为时才需要设置 CustomHandler。"
                    }
                }
            };
        }
    }
}
#endif
