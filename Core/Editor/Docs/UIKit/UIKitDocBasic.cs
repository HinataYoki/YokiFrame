#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 基本使用文档
    /// </summary>
    internal static class UIKitDocBasic
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "基本使用",
                Description = "UIKit 提供静态方法管理 UI 面板的生命周期。面板类需继承 UIPanel。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "打开面板",
                        Code = @"// 同步打开面板
var panel = UIKit.OpenPanel<MainMenuPanel>();

// 指定层级打开
var panel = UIKit.OpenPanel<SettingsPanel>(UILevel.Pop);

// 传递数据
var data = new GameOverData { Score = 1000 };
var panel = UIKit.OpenPanel<GameOverPanel>(UILevel.Common, data);",
                        Explanation = "OpenPanel 会自动处理面板的创建、缓存和显示。如果面板已存在则复用。"
                    },
                    new()
                    {
                        Title = "异步打开面板",
                        Code = @"// 回调方式
UIKit.OpenPanelAsync<LoadingPanel>(panel =>
{
    if (panel != null)
    {
        // 面板加载成功
    }
});

// UniTask 方式（推荐）
var panel = await UIKit.OpenPanelUniTaskAsync<LoadingPanel>();",
                        Explanation = "异步加载适合大型面板或需要从 AssetBundle 加载的情况。"
                    },
                    new()
                    {
                        Title = "获取/显示/隐藏面板",
                        Code = @"// 获取已存在的面板（不创建）
var panel = UIKit.GetPanel<MainMenuPanel>();

// 显示面板
UIKit.ShowPanel<MainMenuPanel>();

// 隐藏面板
UIKit.HidePanel<MainMenuPanel>();

// 隐藏所有面板
UIKit.HideAllPanel();"
                    },
                    new()
                    {
                        Title = "关闭面板",
                        Code = @"// 关闭指定类型面板
UIKit.ClosePanel<SettingsPanel>();

// 关闭面板实例
UIKit.ClosePanel(panel);

// 关闭所有面板
UIKit.CloseAllPanel();

// 强制关闭所有面板（忽略热度，用于场景切换）
UIKit.ForceCloseAllPanel();"
                    },
                    new()
                    {
                        Title = "UI 层级",
                        Code = @"// UILevel 枚举定义了 UI 的显示层级
// 从低到高依次为：
// - UILevel.AlwayBottom  // 始终在最底层（如背景）
// - UILevel.Bg           // 背景层
// - UILevel.Common       // 常规层（默认）
// - UILevel.Pop          // 弹窗层
// - UILevel.AlwayTop     // 始终在最顶层（如 Loading）

// 打开面板时指定层级
UIKit.OpenPanel<DialogPanel>(UILevel.Pop);

// 动态修改面板层级
UIKit.SetPanelLevel(panel, UILevel.AlwayTop);

// 设置子层级（同层级内的排序）
UIKit.SetPanelSubLevel(panel, 10);",
                        Explanation = "层级系统确保 UI 按正确顺序显示，如弹窗始终在主界面之上。"
                    }
                }
            };
        }
    }
}
#endif
