#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 缓存与加载器文档
    /// </summary>
    internal static class UIKitDocCache
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "预加载与缓存",
                Description = "UIKit 提供面板预加载和热度缓存管理。缓存分为已打开缓存和预加载缓存两个独立池，热度系统自动管理面板生命周期。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "预加载面板",
                        Code = @"// 回调方式
UIKit.PreloadPanelAsync<HeavyPanel>(UILevel.Common, success =>
{
    if (success) Debug.Log(""预加载成功"");
});

// UniTask 方式
bool success = await UIKit.PreloadPanelUniTaskAsync<HeavyPanel>(
    UILevel.Common, destroyCancellationToken);

// 预加载后打开（自动从预加载池迁移到已打开池）
var panel = UIKit.OpenPanel<HeavyPanel>();",
                        Explanation = "预加载面板存储在独立的预加载池，打开时自动迁移到已打开池。"
                    },
                    new()
                    {
                        Title = "缓存查询",
                        Code = @"// 检查面板是否已缓存（包括已打开和预加载）
bool cached = UIKit.IsPanelCached<MainMenuPanel>();

// 获取所有已缓存的面板类型
var types = UIKit.GetCachedPanelTypes();

// 获取所有已缓存的面板实例
var panels = UIKit.GetCachedPanels();

// 获取缓存容量（预加载池容量）
int capacity = UIKit.GetCacheCapacity();",
                        Explanation = "缓存查询会同时检查已打开池和预加载池。"
                    },
                    new()
                    {
                        Title = "缓存模式（CacheMode）",
                        Code = @"// 面板缓存模式（通过 PanelHandler.CacheMode 设置）
// - Hot: 热度模式（默认），根据热度值决定是否销毁
// - Persistent: 常驻模式，关闭后不会被自动销毁
// - Temporary: 临时模式，关闭后立即销毁

// 缓存模式在关闭面板时生效：
// Temporary → 立即销毁
// Persistent → 保留在缓存，永不自动销毁
// Hot → 热度 ≤ 0 时销毁",
                        Explanation = "CacheMode 决定面板关闭后的缓存行为。"
                    },
                    new()
                    {
                        Title = "热度系统",
                        Code = @"// 热度配置（可在运行时修改）
UIKit.OpenHot = 3;   // 创建/打开面板时 +3
UIKit.GetHot = 2;    // 获取面板时 +2
UIKit.Weaken = 1;    // 每秒衰减 -1

// 热度规则：
// - 热度衰减在 Update 中定时执行（每秒一次）
// - 热度 ≤ 0 且状态为 Close 的面板会被销毁
// - GetPanel 是纯查询，不触发衰减（只增加热度）
// - 常用面板热度高，保持缓存",
                        Explanation = "热度定时衰减，避免查询操作产生副作用。"
                    },
                    new()
                    {
                        Title = "缓存管理",
                        Code = @"// 设置预加载池容量（默认 10）
UIKit.SetCacheCapacity(20);

// 清理指定面板的预加载缓存
UIKit.ClearPreloadedCache<HeavyPanel>();

// 清理所有预加载缓存
UIKit.ClearAllPreloadedCache();

// 预加载池满时自动 LRU 淘汰最久未访问的面板",
                        Explanation = "LRU 策略只作用于预加载池，已打开池由热度管理。"
                    },
                    new()
                    {
                        Title = "自定义加载器",
                        Code = @"// 实现自定义加载器池
public class MyLoaderPool : AbstractPanelLoaderPool
{
    protected override IPanelLoader CreatePanelLoader()
    {
        return new MyLoader(this);
    }
}

// 设置自定义加载器
UIKit.SetPanelLoader(new MyLoaderPool());

// 默认加载路径：Resources/Art/UIPrefab/{PanelName}
// YooAsset 加载器已内置支持",
                        Explanation = "自定义加载器适合特殊资源加载方案。"
                    }
                }
            };
        }
    }
}
#endif
