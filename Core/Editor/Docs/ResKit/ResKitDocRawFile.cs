#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 原始文件加载文档
    /// </summary>
    internal static class ResKitDocRawFile
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "原始文件加载",
                Description = "加载非 Unity 资源的原始文件（如 JSON、XML、二进制数据等）。默认使用 Resources/TextAsset 实现，支持 YooAsset 扩展。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "同步加载原始文件",
                        Code = @"// 加载文本文件（Resources 方式需放在 Resources 文件夹下）
string jsonText = ResKit.LoadRawFileText(""Config/settings"");
string xmlText = ResKit.LoadRawFileText(""Data/items"");

// 加载二进制数据
byte[] data = ResKit.LoadRawFileData(""Binary/model"");

// 获取原始文件路径（YooAsset 支持，Resources 返回 null）
string filePath = ResKit.GetRawFilePath(""Config/settings"");
if (filePath != null)
{
    // 可以直接使用文件路径进行 IO 操作
    using var stream = File.OpenRead(filePath);
}",
                        Explanation = "Resources 方式要求文件以 .txt/.bytes 等扩展名存储在 Resources 文件夹下。"
                    },
                    new()
                    {
                        Title = "异步加载原始文件",
                        Code = @"// 回调方式
ResKit.LoadRawFileTextAsync(""Config/settings"", text =>
{
    if (text != null)
    {
        var config = JsonUtility.FromJson<GameConfig>(text);
    }
});

ResKit.LoadRawFileDataAsync(""Binary/model"", data =>
{
    if (data != null)
    {
        ProcessBinaryData(data);
    }
});

#if YOKIFRAME_UNITASK_SUPPORT
// UniTask 方式（推荐）
var jsonText = await ResKit.LoadRawFileTextUniTaskAsync(""Config/settings"");
var config = JsonUtility.FromJson<GameConfig>(jsonText);

// 支持取消
var cts = new CancellationTokenSource();
var data = await ResKit.LoadRawFileDataUniTaskAsync(""Binary/model"", cts.Token);
#endif"
                    },
                    new()
                    {
                        Title = "自定义原始文件加载池",
                        Code = @"// 切换到自定义原始文件加载池
ResKit.SetRawFileLoaderPool(new CustomRawFileLoaderPool());

// 获取当前原始文件加载池
var pool = ResKit.GetRawFileLoaderPool();

#if YOKIFRAME_YOOASSET_SUPPORT
// 使用 YooAsset 原始文件加载池
var package = YooAssets.GetPackage(""DefaultPackage"");
ResKit.SetRawFileLoaderPool(new YooAssetRawFileLoaderUniTaskPool(package));

// YooAsset 方式加载原始文件
var jsonText = ResKit.LoadRawFileText(""Assets/GameRes/Config/settings.json"");
var filePath = ResKit.GetRawFilePath(""Assets/GameRes/Config/settings.json"");
#endif",
                        Explanation = "YooAsset 的原始文件加载支持获取实际文件路径，适合需要直接文件访问的场景。"
                    }
                }
            };
        }
    }
}
#endif
