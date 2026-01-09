#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 绑定系统文档
    /// </summary>
    internal static class UIKitDocBind
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "UI 绑定系统",
                Description = "UIKit 提供 UI 组件绑定系统，自动生成组件引用代码，避免手动拖拽或 Find 查找。支持四种绑定类型和自定义代码生成模板。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "四种绑定类型",
                        Code = @"// UIKit 提供四种绑定类型，适用于不同的 UI 组织场景：

// 1. Member - 成员绑定
// 用途：直接引用 Unity 组件（Button、Text、Image 等）
// 特点：不生成独立类文件，不能包含子绑定
// 适用：按钮点击、文本更新、图片切换等直接操作
[SerializeField] private Button mStartButton;
[SerializeField] private Text mTitleText;

// 2. Element - 元素绑定
// 用途：面板内部可复用的 UI 结构
// 特点：生成独立类（继承 UIElement），可包含子绑定
// 适用：列表项、标签页内容、面板内重复使用的 UI 块
// 生成路径：Scripts/{PanelName}/UIElement/{ElementName}.cs
public partial class ItemCell : UIElement { }

// 3. Component - 组件绑定
// 用途：跨面板复用的独立 UI 组件
// 特点：生成独立类（继承 UIComponent），可跨面板使用
// 适用：通用按钮、头像组件、货币显示等复用组件
// 生成路径：Scripts/UIComponent/{ComponentName}.cs
// 注意：Component 下不能定义 Element
public partial class CommonButton : UIComponent { }

// 4. Leaf - 叶子绑定
// 用途：标记节点，阻止子节点被搜索
// 特点：不生成任何代码，用于标记不需要绑定的子树
// 适用：纯装饰性 UI、第三方插件节点、优化搜索性能",
                        Explanation = "根据 UI 结构的复用范围选择合适的绑定类型：Member 用于直接引用，Element 用于面板内复用，Component 用于跨面板复用，Leaf 用于标记忽略。"
                    },
                    new()
                    {
                        Title = "绑定类型选择指南",
                        Code = @"// 选择绑定类型的决策流程：

// Q1: 是否需要在代码中访问？
//     否 -> 不添加 Bind 或使用 Leaf
//     是 -> 继续 Q2

// Q2: 是否需要生成独立类？
//     否 -> 使用 Member（直接引用组件）
//     是 -> 继续 Q3

// Q3: 是否需要跨面板复用？
//     否 -> 使用 Element（面板内复用）
//     是 -> 使用 Component（跨面板复用）

// 示例层级结构：
// MainPanel (Panel)
// ├── Header
// │   ├── Title (Member: mTitleText)
// │   └── CloseBtn (Member: mCloseBtn)
// ├── Content
// │   ├── ItemList
// │   │   └── ItemCell (Element) <- 面板内复用的列表项
// │   │       ├── Icon (Member)
// │   │       └── Name (Member)
// │   └── AvatarView (Component) <- 跨面板复用的头像组件
// │       ├── Frame (Member)
// │       └── Portrait (Member)
// └── Decorations (Leaf) <- 纯装饰，不需要代码访问
//     └── ... (子节点被忽略)",
                        Explanation = "合理的绑定类型选择可以提高代码复用性和可维护性。"
                    },
                    new()
                    {
                        Title = "命名规范",
                        Code = @"// 推荐的命名规范：
// - mBtn_XXX: Button
// - mTxt_XXX: Text/TMP_Text
// - mImg_XXX: Image
// - mGo_XXX: GameObject
// - mTrans_XXX: Transform
// - mInput_XXX: InputField
// - mToggle_XXX: Toggle
// - mSlider_XXX: Slider
// - mScroll_XXX: ScrollRect

// 示例：
// mBtn_Start     -> private Button mBtn_Start;
// mTxt_Score     -> private Text mTxt_Score;
// mImg_Avatar    -> private Image mImg_Avatar;",
                        Explanation = "统一的命名规范让代码更易读，也便于自动生成。"
                    },
                    new()
                    {
                        Title = "生成的代码",
                        Code = @"// 生成的绑定代码示例：
public partial class MainMenuPanel
{
    // 自动生成的序列化字段（Unity 自动赋值）
    [SerializeField] private Button mBtn_Start;
    [SerializeField] private Button mBtn_Settings;
    [SerializeField] private Button mBtn_Quit;
    [SerializeField] private Text mTxt_Version;
    [SerializeField] private Image mImg_Logo;
}

// 在面板中直接使用（无需手动初始化）
public partial class MainMenuPanel : UIPanel
{
    protected override void OnInit(IUIData data = null)
    {
        // 字段已由 Unity 序列化系统自动赋值
        // 直接使用即可
        mBtn_Start.onClick.AddListener(OnStartClick);
        mBtn_Settings.onClick.AddListener(OnSettingsClick);
    }
}",
                        Explanation = "生成的代码使用 [SerializeField] 标记，Unity 在 Prefab 实例化时自动绑定引用，无需手动初始化。"
                    },
                    new()
                    {
                        Title = "最佳实践",
                        Code = @"// 1. 只绑定需要在代码中访问的元素
// 需要绑定：按钮（需要监听点击）、文本（需要更新内容）
// 不需要绑定：纯装饰的图片、静态文本

// 2. 使用有意义的名称
// 好：mBtn_StartGame, mTxt_PlayerName
// 差：mBtn_1, mTxt_A

// 3. 保持层级结构清晰
// MainMenuPanel
// ├── Header
// │   └── Logo (Bind: mImg_Logo)
// ├── Buttons
// │   ├── Start (Bind: mBtn_Start)
// │   └── Settings (Bind: mBtn_Settings)
// └── Footer
//     └── Version (Bind: mTxt_Version)

// 4. 使用 partial class 分离生成代码
// MainMenuPanel.cs          - 手写的业务逻辑
// MainMenuPanel.Designer.cs - 自动生成的绑定代码",
                        Explanation = "良好的绑定习惯可以提高开发效率和代码可维护性。"
                    }
                }
            };
        }
    }
}
#endif
