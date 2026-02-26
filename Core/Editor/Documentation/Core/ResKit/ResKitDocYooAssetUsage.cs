#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 使用 ResKit 加载资源文档
    /// </summary>
    internal static class ResKitDocYooAssetUsage
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "使用 ResKit 加载资源",
                Description = "切换加载池后，使用 ResKit 的统一 API 加载资源。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "加载资源示例",
                        Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
// 同步加载（YooAsset 使用完整路径）
var prefab = ResKit.Load<GameObject>(""Assets/GameRes/Prefabs/Player.prefab"");
var sprite = ResKit.Load<Sprite>(""Assets/GameRes/Sprites/Icon.png"");

// 异步加载（UniTask 方式，推荐）
var enemy = await ResKit.LoadUniTaskAsync<GameObject>(""Assets/GameRes/Prefabs/Enemy.prefab"");
var instance = Object.Instantiate(enemy);
#endif",
                        Explanation = "YooAsset 默认使用完整的资源路径（Assets/...），建议将游戏资源放在统一目录如 Assets/GameRes/。"
                    },
                    new()
                    {
                        Title = "可寻址资源定位",
                        Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
// 开启可寻址后，可以直接使用资源名加载
var prefab = ResKit.Load<GameObject>(""Player"");
var sprite = ResKit.Load<Sprite>(""Icon"");

// 两种方式都可以
var player1 = ResKit.Load<GameObject>(""Player"");
var player2 = ResKit.Load<GameObject>(""Assets/Prefabs/Player.prefab"");
#endif",
                        Explanation = "开启可寻址后，资源名必须唯一。建议使用有意义的命名规范。"
                    }
                }
            };
        }
    }
}
#endif
