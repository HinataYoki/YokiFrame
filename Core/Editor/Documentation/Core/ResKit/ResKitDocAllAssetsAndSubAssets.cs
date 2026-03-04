#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 批量加载与子资源加载文档
    /// </summary>
    internal static class ResKitDocAllAssetsAndSubAssets
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "批量加载与子资源加载",
                Description = "加载多个资源（AllAssets）或加载包含子对象的资源（SubAssets，如 SpriteAtlas）。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "AllAssets — 各后端行为差异",
                        Code = @"// ⚠ LoadAll 的 path 含义因后端而异：
//
// ● YooAsset 后端：path 定位到一个 Bundle，加载该 Bundle 内所有匹配类型的资源。
//   例：将所有配置表打包到同一 Bundle，传入其中任意一个地址即可加载全部。
var handler = ResKit.LoadAllAsset<TextAsset>(""Assets/GameRes/Configs/any_config"");

// ● Resources 后端：path 为 Resources 文件夹内的路径，
//   加载该路径文件夹下所有资源（或该路径文件的子对象）。
var configs = ResKit.LoadAll<TextAsset>(""Configs""); // 加载 Resources/Configs/ 下全部 TextAsset

// ● Editor 后端（AssetDatabase）：path 为 Assets/ 路径，
//   加载该路径下的所有资源对象。
var all = ResKit.LoadAll<TextAsset>(""Assets/GameRes/Configs/data.bytes"");",
                        Explanation = @"YooAsset 的 LoadAllAssets 是包级别加载（Bundle），传入的 location 只用于定位 Bundle。
Resources.LoadAll 是文件夹级别加载，传入文件路径时返回该文件及其子对象。
使用时务必根据实际后端理解 path 的含义。"
                    },
                    new()
                    {
                        Title = "AllAssets — 批量加载配置表",
                        Code = @"// 便利方法（自动管理生命周期，适合一次性读取后不再引用的场景）
var configs = ResKit.LoadAll<TextAsset>(""Assets/GameRes/Configs/config_table"");
foreach (var cfg in configs)
    ParseConfig(cfg);

// 句柄方式（需手动 Release，适合需要持续引用的场景）
var handler = ResKit.LoadAllAsset<TextAsset>(""Assets/GameRes/Configs/config_table"");
var allConfigs = handler.GetAllAssetObjects<TextAsset>();
foreach (var cfg in allConfigs)
    ParseConfig(cfg);
// 使用完毕后释放
handler.Release();

#if YOKIFRAME_UNITASK_SUPPORT
// UniTask 异步加载（推荐，返回句柄）
var handler = await ResKit.LoadAllAssetUniTaskAsync<TextAsset>(
    ""Assets/GameRes/Configs/config_table"", destroyCancellationToken);
var allConfigs = handler.GetAllAssetObjects<TextAsset>();
// 使用完毕后释放
handler.Release();
#endif",
                        Explanation = "LoadAll 便利方法适合加载后立即解析的场景；LoadAllAsset 返回句柄适合需要长期持有资源引用的场景。YooAsset 模式下句柄会持有底层 AllAssetsHandle，直到 Release() 才释放。"
                    },
                    new()
                    {
                        Title = "SubAssets — SpriteAtlas / TexturePacker 图集",
                        Code = @"// 加载图集并按名称获取精灵
var handler = ResKit.LoadSubAsset<Sprite>(""Assets/GameRes/UIAtlas/login.spriteatlas"");
var loginBtn = handler.GetSubAssetObject<Sprite>(""login_button"");
var loginBg = handler.GetSubAssetObject<Sprite>(""login_background"");
image.sprite = loginBtn;

// 获取所有子精灵
var allSprites = handler.GetAllSubAssetObjects<Sprite>();

// 使用完毕后释放
handler.Release();

#if YOKIFRAME_UNITASK_SUPPORT
// UniTask 异步加载（推荐）
var handler = await ResKit.LoadSubAssetUniTaskAsync<Sprite>(
    ""Assets/GameRes/UIAtlas/login.spriteatlas"", destroyCancellationToken);
var sprite = handler.GetSubAssetObject<Sprite>(""spriteName"");
// 使用完毕后释放
handler.Release();
#endif",
                        Explanation = @"SubAssetsResHandler 持有底层 YooAsset SubAssetsHandle，确保精灵对象在使用期间不被回收。
通过 GetSubAssetObject<T>(name) 按名称查找特定精灵（对应 YooAsset 的 SubAssetsHandle.GetSubAssetObject）。
TexturePacker 图集同理：加载图集文件 → GetSubAssetObject 获取切片精灵。"
                    }
                }
            };
        }
    }
}
#endif
