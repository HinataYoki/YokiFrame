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
                Description = "UIKit 是静态门面，所有调用转发到 UIRoot 单例。面板类继承 UIPanel，通过 UIKit 静态方法管理。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "打开面板",
                        Code = @"// 同步打开（默认 Common 层级）
var panel = UIKit.OpenPanel<MainMenuPanel>();

// 指定层级
var panel = UIKit.OpenPanel<DialogPanel>(UILevel.Pop);

// 传递数据
var data = new GameOverData { Score = 1000 };
var panel = UIKit.OpenPanel<GameOverPanel>(UILevel.Common, data);",
                        Explanation = "OpenPanel 自动处理创建、缓存和显示。已存在的面板会复用。"
                    },
                    new()
                    {
                        Title = "异步打开",
                        Code = @"// 回调方式
UIKit.OpenPanelAsync<LoadingPanel>(panel =>
{
    if (panel != default) { /* 加载成功 */ }
});

// UniTask 方式（推荐）
var panel = await UIKit.OpenPanelUniTaskAsync<LoadingPanel>(
    UILevel.Common, data, destroyCancellationToken);",
                        Explanation = "异步加载适合大型面板或 YooAsset 资源。"
                    },
                    new()
                    {
                        Title = "获取/显示/隐藏",
                        Code = @"// 获取已存在的面板（不创建）
var panel = UIKit.GetPanel<MainMenuPanel>();
if (panel != default) { /* 面板存在 */ }

// 显示/隐藏
UIKit.ShowPanel<MainMenuPanel>();
UIKit.HidePanel<MainMenuPanel>();
UIKit.HideAllPanel();",
                        Explanation = "GetPanel 返回 null 表示面板未创建。使用 == default 判空。"
                    },
                    new()
                    {
                        Title = "关闭面板",
                        Code = @"// 关闭指定类型
UIKit.ClosePanel<SettingsPanel>();

// 关闭实例
UIKit.ClosePanel(panel);

// 关闭所有
UIKit.CloseAllPanel();"
                    },
                    new()
                    {
                        Title = "UI 层级",
                        Code = @"// UILevel 从低到高：
// AlwayBottom → Bg → Common（默认）→ Pop → AlwayTop → CanvasPanel

// 打开时指定层级
UIKit.OpenPanel<DialogPanel>(UILevel.Pop);

// 动态修改层级
UIKit.SetPanelLevel(panel, UILevel.AlwayTop, subLevel: 0);

// 仅修改子层级（同层级内排序）
UIKit.SetPanelSubLevel(panel, 10);

// 获取顶部面板
var topPanel = UIKit.GetGlobalTopPanel();
var topAtLevel = UIKit.GetTopPanelAtLevel(UILevel.Pop);",
                        Explanation = "层级确保 UI 正确叠放，如弹窗始终在主界面之上。"
                    }
                }
            };
        }
    }
}
#endif
