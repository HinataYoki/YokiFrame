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
                Description = "现代化 UI 管理工具，提供面板动画系统、增强生命周期钩子、多命名栈管理、预加载缓存、LRU 淘汰策略等功能。支持同步/异步加载、UniTask 集成、DOTween 动画。",
                Keywords = new List<string> { "UI管理", "面板堆栈", "缓存", "异步加载", "动画", "生命周期", "预加载", "LRU" },
                Sections = new List<DocSection>
                {
                    CreateBasicUsageSection(),
                    CreateAnimationSection(),
                    CreateLifecycleSection(),
                    CreateStackSection(),
                    CreateCacheSection(),
                    CreateCustomPanelSection(),
                    CreateHotCacheSection(),
                    CreateEditorToolsSection()
                }
            };
        }

        private DocSection CreateBasicUsageSection()
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

// UniTask 方式（推荐，需要 YOKIFRAME_UNITASK_SUPPORT）
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
            };
        }

        private DocSection CreateAnimationSection()
        {
            return new DocSection
            {
                Title = "动画系统",
                Description = "UIKit 提供灵活的动画系统，支持内置动画、DOTween 动画和自定义动画。动画可以组合使用。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "使用内置动画",
                        Code = @"// 创建淡入淡出动画
var fadeIn = UIAnimationFactory.CreateFadeIn(0.3f);
var fadeOut = UIAnimationFactory.CreateFadeOut(0.3f);

// 创建缩放动画
var scaleIn = UIAnimationFactory.CreateScaleIn(0.3f);
var scaleOut = UIAnimationFactory.CreateScaleOut(0.3f);

// 创建滑动动画
var slideIn = UIAnimationFactory.CreateSlideIn(SlideDirection.Left, 0.3f);
var slideOut = UIAnimationFactory.CreateSlideOut(SlideDirection.Right, 0.3f);

// 设置面板动画
panel.SetShowAnimation(fadeIn);
panel.SetHideAnimation(fadeOut);",
                        Explanation = "内置动画使用协程实现，无需额外依赖。"
                    },
                    new()
                    {
                        Title = "使用 DOTween 动画",
                        Code = @"// 需要定义 YOKIFRAME_DOTWEEN_SUPPORT 宏
// 或安装 DOTween 包后自动启用

// 创建 DOTween 版本动画（性能更好）
var fadeIn = UIAnimationFactory.CreateDOTweenFadeIn(0.3f, Ease.OutQuad);
var scaleIn = UIAnimationFactory.CreateDOTweenScaleIn(0.3f, Ease.OutBack);
var slideIn = UIAnimationFactory.CreateDOTweenSlideIn(SlideDirection.Bottom, 0.3f);

panel.SetShowAnimation(fadeIn);",
                        Explanation = "DOTween 动画支持更多缓动函数，性能更优。"
                    },
                    new()
                    {
                        Title = "组合动画",
                        Code = @"// 并行组合（同时播放）
var parallelAnim = UIAnimationFactory.CreateParallel(
    UIAnimationFactory.CreateFadeIn(0.3f),
    UIAnimationFactory.CreateScaleIn(0.3f)
);

// 顺序组合（依次播放）
var sequenceAnim = UIAnimationFactory.CreateSequence(
    UIAnimationFactory.CreateFadeIn(0.2f),
    UIAnimationFactory.CreateScaleIn(0.2f)
);

panel.SetShowAnimation(parallelAnim);",
                        Explanation = "组合动画可以创建复杂的入场/退场效果。"
                    },
                    new()
                    {
                        Title = "在 Inspector 中配置动画",
                        Code = @"// UIPanel 支持在 Inspector 中配置动画
// 在面板预制体上设置：
// - Show Animation Config: 显示动画配置
// - Hide Animation Config: 隐藏动画配置

// 也可以通过代码动态设置
[SerializeField] private UIAnimationConfig mShowAnimConfig;
[SerializeField] private UIAnimationConfig mHideAnimConfig;

protected override void Awake()
{
    base.Awake();
    // 动画会在 Awake 中自动创建
}",
                        Explanation = "Inspector 配置适合设计师调整动画参数。"
                    },
                    new()
                    {
                        Title = "异步动画（UniTask）",
                        Code = @"// 使用 UniTask 等待动画完成
await panel.ShowUniTaskAsync();
Debug.Log(""显示动画完成"");

await panel.HideUniTaskAsync();
Debug.Log(""隐藏动画完成"");

// 支持取消令牌
var cts = new CancellationTokenSource();
await panel.ShowUniTaskAsync(cts.Token);",
                        Explanation = "UniTask 版本适合需要等待动画完成的场景。"
                    }
                }
            };
        }

        private DocSection CreateLifecycleSection()
        {
            return new DocSection
            {
                Title = "生命周期钩子",
                Description = "UIKit 提供丰富的生命周期钩子，支持动画前后回调、焦点管理等。所有钩子都有异常保护，不会中断流程。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "完整生命周期",
                        Code = @"public class MyPanel : UIPanel
{
    // === 初始化阶段 ===
    protected override void OnInit(IUIData data = null)
    {
        // 面板首次创建时调用，只调用一次
        // 适合：绑定事件、初始化组件引用
    }

    // === 打开阶段 ===
    protected override void OnOpen(IUIData data = null)
    {
        // 每次打开面板时调用
        // 适合：刷新数据、重置状态
    }

    // === 显示阶段 ===
    protected override void OnWillShow()
    {
        // 显示动画开始前调用
        // 适合：准备动画、预加载资源
    }

    protected override void OnShow()
    {
        // 显示时调用（动画播放中）
    }

    protected override void OnDidShow()
    {
        // 显示动画完成后调用
        // 适合：开始交互、播放音效
    }

    // === 隐藏阶段 ===
    protected override void OnWillHide()
    {
        // 隐藏动画开始前调用
        // 适合：保存状态、停止交互
    }

    protected override void OnHide()
    {
        // 隐藏时调用（动画播放中）
    }

    protected override void OnDidHide()
    {
        // 隐藏动画完成后调用
        // 适合：清理临时资源
    }

    // === 关闭阶段 ===
    protected override void OnClose()
    {
        // 面板关闭时调用
        // 适合：解绑事件、释放资源
    }
}",
                        Explanation = "生命周期钩子按顺序调用，异常不会中断后续钩子。"
                    },
                    new()
                    {
                        Title = "焦点管理钩子",
                        Code = @"public class MyPanel : UIPanel
{
    protected override void OnFocus()
    {
        // 面板成为栈顶时调用
        // 适合：恢复输入、播放背景音乐
        Debug.Log(""面板获得焦点"");
    }

    protected override void OnBlur()
    {
        // 面板失去栈顶位置时调用
        // 适合：暂停输入、降低音量
        Debug.Log(""面板失去焦点"");
    }

    protected override void OnResume()
    {
        // 面板从栈中恢复时调用（Pop 后）
        // 适合：刷新数据、恢复状态
        Debug.Log(""面板恢复"");
    }
}",
                        Explanation = "焦点钩子配合堆栈系统使用，自动管理面板焦点状态。"
                    },
                    new()
                    {
                        Title = "生命周期事件",
                        Code = @"// 通过 EventKit 监听面板生命周期事件
EventKit.Type.Register<PanelWillShowEvent>(e => 
{
    Debug.Log($""{e.Panel.GetType().Name} 即将显示"");
}).UnRegisterWhenGameObjectDestroyed(gameObject);

EventKit.Type.Register<PanelDidShowEvent>(e => 
{
    Debug.Log($""{e.Panel.GetType().Name} 显示完成"");
});

EventKit.Type.Register<PanelFocusEvent>(e => 
{
    Debug.Log($""{e.Panel.GetType().Name} 获得焦点"");
});

EventKit.Type.Register<PanelBlurEvent>(e => 
{
    Debug.Log($""{e.Panel.GetType().Name} 失去焦点"");
});

// 可用事件：
// PanelWillShowEvent, PanelDidShowEvent
// PanelWillHideEvent, PanelDidHideEvent
// PanelFocusEvent, PanelBlurEvent, PanelResumeEvent",
                        Explanation = "事件系统适合全局监控面板状态，如统计、日志等。"
                    }
                }
            };
        }

        private DocSection CreateStackSection()
        {
            return new DocSection
            {
                Title = "面板堆栈",
                Description = "UIKit 提供增强的面板堆栈管理，支持多命名栈、焦点自动管理、异步操作。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基本堆栈操作",
                        Code = @"// 打开并压入堆栈（自动隐藏上一层，触发 OnBlur）
UIKit.PushOpenPanel<SettingsPanel>();

// 压入已存在的面板
UIKit.PushPanel<InventoryPanel>(hidePreLevel: true);

// 弹出面板（自动显示上一层，触发 OnFocus/OnResume）
var panel = UIKit.PopPanel();

// 弹出但不关闭
var panel = UIKit.PopPanel(showPreLevel: true, autoClose: false);

// 查看栈顶面板（不移除）
var topPanel = UIKit.PeekPanel();

// 获取栈深度
int depth = UIKit.GetStackDepth();

// 清空堆栈
UIKit.ClearStack(closeAll: true);",
                        Explanation = "堆栈模式适合设置页面、背包等需要返回上一级的场景。"
                    },
                    new()
                    {
                        Title = "多命名栈",
                        Code = @"// 使用不同的栈管理不同类型的面板
UIKit.PushPanel(mainPanel, ""main"");      // 主界面栈
UIKit.PushPanel(dialogPanel, ""dialog"");  // 对话框栈
UIKit.PushPanel(tutorialPanel, ""tutorial""); // 教程栈

// 从指定栈弹出
var panel = UIKit.PopPanel(""dialog"");

// 查看指定栈的栈顶
var top = UIKit.PeekPanel(""tutorial"");

// 获取指定栈深度
int depth = UIKit.GetStackDepth(""dialog"");

// 清空指定栈
UIKit.ClearStack(""dialog"", closeAll: true);",
                        Explanation = "多命名栈适合复杂 UI 场景，如同时存在主界面和弹窗。"
                    },
                    new()
                    {
                        Title = "异步堆栈操作",
                        Code = @"// 回调方式
UIKit.PushOpenPanelAsync<DetailPanel>(panel =>
{
    // 面板已打开并压入堆栈
});

// UniTask 方式（等待动画完成）
var panel = await UIKit.PushOpenPanelUniTaskAsync<DetailPanel>();

// 异步弹出（等待动画完成）
var poppedPanel = await UIKit.PopPanelUniTaskAsync();

// 从指定栈异步弹出
var poppedPanel = await UIKit.PopPanelUniTaskAsync(""dialog"");",
                        Explanation = "异步操作会等待动画完成后返回。"
                    }
                }
            };
        }

        private DocSection CreateCacheSection()
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
                        Code = @"// 回调方式预加载
UIKit.PreloadPanelAsync<HeavyPanel>(UILevel.Common, success =>
{
    if (success)
    {
        Debug.Log(""预加载成功，后续打开将立即显示"");
    }
});

// UniTask 方式预加载
bool success = await UIKit.PreloadPanelUniTaskAsync<HeavyPanel>();

// 预加载后打开（从缓存获取，无需等待加载）
var panel = UIKit.OpenPanel<HeavyPanel>();",
                        Explanation = "预加载适合在 Loading 界面提前加载后续需要的面板。"
                    },
                    new()
                    {
                        Title = "缓存查询",
                        Code = @"// 检查面板是否已缓存
bool isCached = UIKit.IsPanelCached<MainMenuPanel>();

// 获取所有已缓存的面板类型
var cachedTypes = UIKit.GetCachedPanelTypes();
foreach (var type in cachedTypes)
{
    Debug.Log($""已缓存: {type.Name}"");
}",
                        Explanation = "缓存查询可用于调试或决定是否需要预加载。"
                    },
                    new()
                    {
                        Title = "缓存管理",
                        Code = @"// 设置缓存容量（默认 10）
UIKit.SetCacheCapacity(20);

// 清理指定面板的预加载缓存
UIKit.ClearPreloadedCache<HeavyPanel>();

// 清理所有预加载缓存
UIKit.ClearAllPreloadedCache();",
                        Explanation = "LRU 策略会自动淘汰最少使用的面板，无需手动管理。"
                    },
                    new()
                    {
                        Title = "LRU 淘汰策略",
                        Code = @"// LRU 淘汰规则：
// 1. 当预加载缓存满时，自动淘汰
// 2. 优先淘汰 Hot 值最低的面板
// 3. Hot 值相同时，淘汰访问时间最早的面板

// Hot 值来源：
// - 创建面板：+3 (UIKit.OpenHot)
// - 获取面板：+2 (UIKit.GetHot)
// - 每次操作：-1 (UIKit.Weaken)

// 配置热度参数
UIKit.OpenHot = 5;   // 增加创建热度
UIKit.GetHot = 3;    // 增加获取热度
UIKit.Weaken = 1;    // 保持衰减速度",
                        Explanation = "热度机制确保常用面板保持缓存，不常用面板自动释放。"
                    }
                }
            };
        }

        private DocSection CreateCustomPanelSection()
        {
            return new DocSection
            {
                Title = "自定义面板",
                Description = "创建自定义面板需继承 UIPanel 并实现生命周期方法。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "完整面板示例",
                        Code = @"public class MainMenuPanel : UIPanel
{
    // UI 组件引用（使用 Bind 系统自动生成）
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

    protected override void OnWillShow()
    {
        // 显示动画开始前
        AudioKit.Play(""UI/MenuAppear"");
    }

    protected override void OnDidShow()
    {
        // 显示动画完成后
        mBtnStart.Select(); // 设置默认焦点
    }

    protected override void OnFocus()
    {
        // 获得焦点时
        base.OnFocus();
        InputSystem.EnableUIInput();
    }

    protected override void OnBlur()
    {
        // 失去焦点时
        base.OnBlur();
        InputSystem.DisableUIInput();
    }

    protected override void OnClose()
    {
        // 关闭时清理
        mBtnStart.onClick.RemoveAllListeners();
        mBtnSettings.onClick.RemoveAllListeners();
    }

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
    private Text mTxtScore;
    private Text mTxtHighScore;
    private GameObject mNewRecordEffect;

    protected override void OnOpen(IUIData data = null)
    {
        if (data is GameOverData gameOverData)
        {
            mTxtScore.text = $""得分: {gameOverData.Score}"";
            mTxtHighScore.text = $""最高分: {gameOverData.HighScore}"";
            mNewRecordEffect.SetActive(gameOverData.IsNewRecord);
        }
    }
}

// 打开面板并传递数据
var data = new GameOverData 
{ 
    Score = 1000, 
    HighScore = 1500, 
    IsNewRecord = false 
};
UIKit.OpenPanel<GameOverPanel>(UILevel.PopUp, data);"
                    },
                    new()
                    {
                        Title = "配置面板动画",
                        Code = @"public class AnimatedPanel : UIPanel
{
    protected override void Awake()
    {
        base.Awake();
        
        // 代码配置动画
        SetShowAnimation(UIAnimationFactory.CreateParallel(
            UIAnimationFactory.CreateFadeIn(0.3f),
            UIAnimationFactory.CreateScaleIn(0.3f)
        ));
        
        SetHideAnimation(UIAnimationFactory.CreateParallel(
            UIAnimationFactory.CreateFadeOut(0.2f),
            UIAnimationFactory.CreateScaleOut(0.2f)
        ));
    }

    // 或者在 Inspector 中配置：
    // [SerializeField] UIAnimationConfig mShowAnimationConfig;
    // [SerializeField] UIAnimationConfig mHideAnimationConfig;
}"
                    }
                }
            };
        }

        private DocSection CreateHotCacheSection()
        {
            return new DocSection
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
UIKit.Weaken = 1;    // 每次操作的热度衰减

// 热度机制说明：
// - 面板创建时获得 OpenHot 热度
// - 每次 GetPanel 获得 GetHot 热度
// - 每次 UI 操作所有面板热度 -Weaken
// - 热度 <= 0 且面板已关闭时，自动销毁",
                        Explanation = "热度机制确保常用面板保持缓存，不常用面板自动释放。"
                    },
                    new()
                    {
                        Title = "自定义加载器",
                        Code = @"// 实现自定义加载器
public class AddressablesPanelLoader : IPanelLoader
{
    private AsyncOperationHandle<GameObject> mHandle;

    public IPanel Load(PanelHandler handler)
    {
        // 同步加载（不推荐用于 Addressables）
        var prefab = Addressables.LoadAssetAsync<GameObject>(handler.Type.Name).WaitForCompletion();
        var go = Object.Instantiate(prefab);
        return go.GetComponent<IPanel>();
    }

    public void LoadAsync(PanelHandler handler, Action<IPanel> onComplete)
    {
        mHandle = Addressables.LoadAssetAsync<GameObject>(handler.Type.Name);
        mHandle.Completed += op =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                var go = Object.Instantiate(op.Result);
                onComplete?.Invoke(go.GetComponent<IPanel>());
            }
            else
            {
                onComplete?.Invoke(null);
            }
        };
    }

    public void UnLoad() => Addressables.Release(mHandle);
    public void Recycle() { /* 回收到池 */ }
}

// 实现加载器池
public class AddressablesPanelLoaderPool : IPanelLoaderPool
{
    public IPanelLoader AllocateLoader() => new AddressablesPanelLoader();
}

// 设置加载器
UIKit.SetPanelLoader(new AddressablesPanelLoaderPool());"
                    }
                }
            };
        }

        private DocSection CreateEditorToolsSection()
        {
            return new DocSection
            {
                Title = "编辑器工具",
                Description = "UIKit 提供面板创建向导、运行时面板查看器和 UI 绑定工具。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "使用编辑器工具",
                        Code = @"// 快捷键：Ctrl+E 打开 YokiFrame Tools 面板
// 选择 UIKit 标签页

// 功能：
// 1. 创建面板向导
//    - 输入面板名称
//    - 选择 UI 层级
//    - 自动创建脚本和预制体

// 2. 运行时面板查看
//    - 查看所有打开的面板
//    - 查看堆栈状态
//    - 查看热度值

// 3. UI 绑定工具（Alt+B）
//    - 自动生成 UI 组件绑定代码
//    - 支持 Button、Text、Image 等组件",
                        Explanation = "编辑器工具简化 UI 开发流程，提高开发效率。"
                    },
                    new()
                    {
                        Title = "UI 绑定系统",
                        Code = @"// 在 Hierarchy 中选择 UI 组件
// 按 Alt+B 添加绑定标记

// 绑定命名规则：
// - mBtn_XXX: Button
// - mTxt_XXX: Text/TMP_Text
// - mImg_XXX: Image
// - mGo_XXX: GameObject
// - mTrans_XXX: Transform

// 生成的代码示例：
public partial class MainMenuPanel
{
    private Button mBtn_Start;
    private Button mBtn_Settings;
    private Text mTxt_Version;
    private Image mImg_Logo;

    private void InitBind()
    {
        mBtn_Start = transform.Find(""Buttons/Start"").GetComponent<Button>();
        mBtn_Settings = transform.Find(""Buttons/Settings"").GetComponent<Button>();
        mTxt_Version = transform.Find(""Version"").GetComponent<Text>();
        mImg_Logo = transform.Find(""Logo"").GetComponent<Image>();
    }
}",
                        Explanation = "Bind 系统自动生成组件引用代码，避免手动拖拽。"
                    }
                }
            };
        }
    }
}
#endif
