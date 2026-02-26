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
                Description = "自动生成 UI 组件引用代码，避免手动拖拽或 Find 查找。支持四种绑定类型，适用于不同的 UI 组织场景。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "四种绑定类型",
                        Code = @"// 1. Member - 成员绑定
// 用途：直接引用 Unity 组件（Button、Text、Image 等）
// 特点：不生成独立类文件，不能包含子绑定
[SerializeField] private Button mStartButton;
[SerializeField] private Text mTitleText;

// 2. Element - 元素绑定
// 用途：面板内部可复用的 UI 结构
// 特点：生成独立类（继承 UIElement），可包含子绑定
// 生成路径：Scripts/{PanelName}/UIElement/{ElementName}.cs

// 3. Component - 组件绑定
// 用途：跨面板复用的独立 UI 组件
// 特点：生成独立类（继承 UIComponent），可跨面板使用
// 生成路径：Scripts/UIComponent/{ComponentName}.cs
// 注意：Component 下不能定义 Element

// 4. Leaf - 叶子绑定
// 用途：标记节点，阻止子节点被搜索
// 特点：不生成任何代码，用于标记不需要绑定的子树",
                        Explanation = "Member 用于直接引用，Element 用于面板内复用，Component 用于跨面板复用，Leaf 用于标记忽略。"
                    },
                    new()
                    {
                        Title = "绑定类型选择",
                        Code = @"// 选择决策流程：
// Q1: 需要在代码中访问？ 否 -> 不添加 Bind 或使用 Leaf
// Q2: 需要生成独立类？   否 -> 使用 Member
// Q3: 需要跨面板复用？   否 -> Element，是 -> Component

// 示例层级结构：
// MainPanel (Panel)
// ├── Header
// │   ├── Title (Member: mTitleText)
// │   └── CloseBtn (Member: mCloseBtn)
// ├── Content
// │   ├── ItemList
// │   │   └── ItemCell (Element) <- 面板内复用
// │   └── AvatarView (Component) <- 跨面板复用
// └── Decorations (Leaf) <- 纯装饰，忽略子节点",
                        Explanation = "合理的绑定类型选择可以提高代码复用性和可维护性。"
                    },
                    new()
                    {
                        Title = "命名规范",
                        Code = @"// 推荐命名规范：
// mBtn_XXX     -> Button
// mTxt_XXX     -> Text/TMP_Text
// mImg_XXX     -> Image
// mGo_XXX      -> GameObject
// mTrans_XXX   -> Transform
// mInput_XXX   -> InputField
// mToggle_XXX  -> Toggle
// mSlider_XXX  -> Slider
// mScroll_XXX  -> ScrollRect

// 示例：
// mBtn_Start   -> private Button mBtn_Start;
// mTxt_Score   -> private Text mTxt_Score;",
                        Explanation = "统一的命名规范让代码更易读，也便于自动生成。"
                    },
                    new()
                    {
                        Title = "生成的代码",
                        Code = @"// 生成的绑定代码使用 [SerializeField] 标记
// Unity 在 Prefab 实例化时自动绑定引用，无需手动初始化

public partial class MainMenuPanel
{
    [SerializeField] private Button mBtn_Start;
    [SerializeField] private Button mBtn_Settings;
    [SerializeField] private Text mTxt_Version;
}

// 在面板中直接使用
public partial class MainMenuPanel : UIPanel
{
    protected override void OnInit(IUIData data = null)
    {
        // 字段已由 Unity 序列化系统自动赋值
        mBtn_Start.onClick.AddListener(OnStartClick);
    }
}",
                        Explanation = "生成的代码使用 partial class 分离，用户文件和 Designer 文件互不干扰。"
                    }
                }
            };
        }
    }
}
#endif
