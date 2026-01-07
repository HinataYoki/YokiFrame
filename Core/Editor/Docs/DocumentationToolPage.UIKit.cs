#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    public partial class DocumentationToolPage
    {
        private DocModule CreateUIKitDoc()
        {
            return new DocModule
            {
                Name = "UIKit",
                Icon = KitIcons.UIKIT,
                Category = "TOOLS",
                Description = "UI 管理工具，提供面板的创建、缓存、堆栈管理等功能。支持同步/异步加载、热度缓存机制、面板堆栈导航。",
                Sections = new List<DocSection>
                {
                    new()
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
var panel = UIKit.OpenPanel<SettingsPanel>(UILevel.PopUp);

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
                            }
                        }
                    },
                    new()
                    {
                        Title = "面板堆栈",
                        Description = "UIKit 提供面板堆栈管理，适合多级菜单导航场景。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "堆栈操作",
                                Code = @"// 打开并压入堆栈（自动隐藏上一层）
UIKit.PushOpenPanel<SettingsPanel>();

// 压入已存在的面板
UIKit.PushPanel<InventoryPanel>(hidePreLevel: true);

// 弹出面板（自动显示上一层，自动关闭当前）
var panel = UIKit.PopPanel();

// 弹出但不关闭
var panel = UIKit.PopPanel(showPreLevel: true, autoClose: false);

// 关闭所有堆栈面板
UIKit.CloseAllStackPanel();",
                                Explanation = "堆栈模式适合设置页面、背包等需要返回上一级的场景。"
                            },
                            new()
                            {
                                Title = "异步堆栈操作",
                                Code = @"// 回调方式
UIKit.PushOpenPanelAsync<DetailPanel>(panel =>
{
    // 面板已打开并压入堆栈
});

// UniTask 方式
var panel = await UIKit.PushOpenPanelUniTaskAsync<DetailPanel>();"
                            }
                        }
                    },
                    new()
                    {
                        Title = "自定义面板",
                        Description = "创建自定义面板需继承 UIPanel 并实现生命周期方法。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "面板定义",
                                Code = @"public class MainMenuPanel : UIPanel
{
    private Button mBtnStart;
    private Button mBtnSettings;
    private Text mTxtVersion;

    protected override void OnInit(IUIData data = null)
    {
        // 初始化，只调用一次
        mBtnStart.onClick.AddListener(OnStartClick);
        mBtnSettings.onClick.AddListener(OnSettingsClick);
    }

    protected override void OnOpen(IUIData data = null)
    {
        // 每次打开时调用
        mTxtVersion.text = Application.version;
    }

    protected override void OnShow() { }
    protected override void OnHide() { }
    protected override void OnClose() { }

    private void OnStartClick() => CloseSelf();
    private void OnSettingsClick() => UIKit.PushOpenPanel<SettingsPanel>();
}",
                                Explanation = "UIPanel 继承自 MonoBehaviour，但业务逻辑应尽量与 Unity 生命周期解耦。"
                            },
                            new()
                            {
                                Title = "面板数据传递",
                                Code = @"// 定义数据类
public class GameOverData : IUIData
{
    public int Score;
    public int HighScore;
    public bool IsNewRecord;
}

// 面板中使用数据
public class GameOverPanel : UIPanel
{
    protected override void OnOpen(IUIData data = null)
    {
        if (data is GameOverData gameOverData)
        {
            mTxtScore.text = gameOverData.Score.ToString();
        }
    }
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "热度缓存与加载器",
                        Description = "UIKit 使用热度值管理面板缓存，支持自定义加载器。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "热度配置",
                                Code = @"// 配置热度参数
UIKit.OpenHot = 3;   // 创建面板时赋予的热度
UIKit.GetHot = 2;    // 获取面板时赋予的热度
UIKit.Weaken = 1;    // 每次操作的热度衰减",
                                Explanation = "热度机制确保常用面板保持缓存，不常用面板自动释放。"
                            },
                            new()
                            {
                                Title = "自定义加载器",
                                Code = @"// 实现自定义加载器池
public class AddressablesPanelLoaderPool : IPanelLoaderPool
{
    public IPanelLoader AllocateLoader() => new AddressablesPanelLoader();
}

// 设置加载器
UIKit.SetPanelLoader(new AddressablesPanelLoaderPool());"
                            }
                        }
                    },
                    new()
                    {
                        Title = "编辑器工具",
                        Description = "UIKit 提供面板创建向导和运行时面板查看器，可在 YokiFrame Tools 面板中管理 UI。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "使用编辑器工具",
                                Code = @"// 快捷键：Ctrl+E 打开 YokiFrame Tools 面板
// 选择 UIKit 标签页

// 功能：
// - 创建面板向导：快速创建 UIPanel 脚本和预制体
// - 运行时面板查看：查看所有打开的面板和堆栈状态
// - 热度监控：查看面板的热度值和缓存状态
// - UI 绑定工具：自动生成 UI 组件绑定代码",
                                Explanation = "编辑器工具简化 UI 开发流程，提高开发效率。"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
