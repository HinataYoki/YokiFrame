#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 完整初始化示例文档 - 配置详解
    /// </summary>
    internal static partial class ResKitDocYooAssetComplete
    {
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
    }
}
#endif
