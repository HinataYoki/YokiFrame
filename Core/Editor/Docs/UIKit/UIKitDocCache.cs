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
                Description = "UIKit 提供面板预加载和 LRU 缓存管理，优化加载性能。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "预加载面板",
                        Code = @"// UniTask 方式预加载
bool success = await UIKit.PreloadPanelUniTaskAsync<HeavyPanel>();

// 预加载后打开（从缓存获取）
var panel = UIKit.OpenPanel<HeavyPanel>();",
                        Explanation = "预加载适合在 Loading 界面提前加载后续需要的面板。"
                    },
                    new()
                    {
                        Title = "缓存管理",
                        Code = @"// 检查面板是否已缓存
bool isCached = UIKit.IsPanelCached<MainMenuPanel>();

// 设置缓存容量（默认 10）
UIKit.SetCacheCapacity(20);

// 清理预加载缓存
UIKit.ClearAllPreloadedCache();",
                        Explanation = "LRU 策略会自动淘汰最少使用的面板。"
                    },
                    new()
                    {
                        Title = "热度配置",
                        Code = @"// 配置热度参数
UIKit.OpenHot = 3;   // 创建面板时赋予的热度
UIKit.GetHot = 2;    // 获取面板时赋予的热度
UIKit.Weaken = 1;    // 每次操作的热度衰减",
                        Explanation = "热度机制确保常用面板保持缓存，不常用面板自动释放。"
                    }
                }
            };
        }
    }
}
#endif
