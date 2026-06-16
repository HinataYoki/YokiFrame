#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 绑定系统完整文档
    /// </summary>
    internal static class UIKitDocBind
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "UI 绑定系统",
                Description = "自动生成 UI 组件引用代码，避免手动拖拽或 Find 查找。通过给 Prefab 节点添加 Bind 组件并生成代码，即可在 Panel 中直接使用类型安全的字段。",
                CodeExamples = new List<CodeExample>
                {
                    // ================================================================
                    // 1. 概览与四种绑定类型
                    // ================================================================
                    new()
                    {
                        Title = "四种绑定类型",
                        Code = @"// 1. Member（成员）- 最常用的绑定类型
//   · 用途：直接引用 Unity 组件（Button、TMP_Text、Image 等）
//   · 特点：不生成独立类文件，字段生成在父级 Designer 中
//   · 适用：单个 UI 控件的组件引用

// 2. Element（元素）- 面板内可复用的 UI 结构
//   · 用途：面板内部多次出现的子结构（如列表项、标签页）
//   · 特点：生成独立类文件（继承 UIElement），可包含子绑定
//   · 生成路径：Scripts/{PanelName}/UIElement/{ElementName}.cs

// 3. Component（组件）- 跨面板复用的独立 UI 组件
//   · 用途：多个面板共用的 UI 组件（如头像、进度条）
//   · 特点：生成独立类文件（继承 UIComponent），可跨面板使用
//   · 生成路径：Scripts/UIComponent/{ComponentName}.cs
//   · 注意：Component 下不能定义 Element

// 4. Leaf（叶子）- 标记忽略节点
//   · 用途：标记某个子树为纯装饰，跳过代码生成
//   · 特点：不生成任何代码，子节点不被搜索
//   · 适用：特效节点、纯视觉装饰层",
                        Explanation = "四种绑定类型覆盖从简单引用到跨面板复用的全部场景。"
                    },
                    // ================================================================
                    // 2. 典型绑定层级示例
                    // ================================================================
                    new()
                    {
                        Title = "绑定层级示例",
                        Code = @"// 推荐的组织方式：
//
// MainPanel (Panel 根节点 - 挂载 UIPanel 脚本)
// ├── Header
// │   ├── Title          → Bind: Member, Name: mTxt_Title
// │   └── Btn_Close      → Bind: Member, Name: mBtn_Close
// ├── Content
// │   ├── ItemList
// │   │   └── ItemCell    → Bind: Element, Name: ShopItemCell
// │   │       ├── Icon    → Bind: Member, Name: mImg_Icon
// │   │       ├── Name    → Bind: Member, Name: mTxt_Name
// │   │       └── Price   → Bind: Member, Name: mTxt_Price
// │   └── AvatarView     → Bind: Component, Name: AvatarView
// └── Decorations        → Bind: Leaf（纯装饰，忽略所有子节点）

// 生成结果：
// - MainMenuPanel.cs           （Panel 用户代码）
// - MainMenuPanel.Designer.cs  （自动生成字段 + ClearUIComponents）
// - UIElement/ShopItemCell.cs  （Element 用户代码）
// - UIElement/ShopItemCell.Designer.cs（Element 自动生成字段）
// - UIComponent/AvatarView.cs  （Component 用户代码）
// - UIComponent/AvatarView.Designer.cs（Component 自动生成字段）",
                        Explanation = "合理规划绑定层级可提高代码复用性。Member 内嵌在 Panel/Element 里，Element 和 Component 各自生成独立文件。"
                    },
                    // ================================================================
                    // 3. 步骤 1：添加 Bind 组件
                    // ================================================================
                    new()
                    {
                        Title = "步骤 1：为 UI 节点添加 Bind 组件",
                        Code = @"// ===== 方式 A：快捷键（推荐） =====
// 在 Hierarchy 中选中一个或多个 GameObject，按 ALT+B
// → 自动为选中节点添加 Bind 组件

// ===== 方式 B：Inspector 手动添加 =====
// 选中 GameObject → Inspector 底部 → Add Component
// → 搜索「Bind」→ 点击添加

// ===== 方式 C：批量移除 =====
// 选中节点 → ALT+SHIFT+B → 移除 Bind 组件

// ===== 操作时机 =====
// 1. 在 Project 窗口双击 Prefab 进入 Prefab Mode
// 2. 在 Hierarchy 中选中需要绑定的 UI 控件节点
// 3. 按 ALT+B 添加 Bind
// 4. 或先手动创建好整个 UI 层级，再统一添加 Bind",
                        Explanation = "快捷键 ALT+B 是最快的添加方式，支持多选批量添加。建议在 Prefab Mode 中操作。"
                    },
                    // ================================================================
                    // 4. 步骤 2：配置绑定属性
                    // ================================================================
                    new()
                    {
                        Title = "步骤 2：配置绑定属性",
                        Code = @"// 选中已挂载 Bind 组件的 GameObject，在 Inspector 中配置：

// ╔══════════════╦═══════════════════════════════════════════╗
// ║ 属性          ║ 说明                                      ║
// ╠══════════════╬═══════════════════════════════════════════╣
// ║ 绑定类型      ║ Member / Element / Component / Leaf       ║
// ║ 字段名称      ║ 生成的 C# 字段名（如 mBtn_Start）          ║
// ║ 组件类型      ║ 自动检测或手动指定（如 Button、TMP_Text）  ║
// ║ 注释          ║ 可选，生成 [Tooltip] 标记                  ║
// ╚══════════════╩═══════════════════════════════════════════╝

// ===== 关于「组件类型」的自动检测 =====
// Bind 组件会自动检测所在 GameObject 上挂载的其他组件，
// 并在「组件类型」下拉框中列出。默认选择最具体的组件。
//
// 例如：GameObject 上同时有 Button + Image
//   → 下拉框：UnityEngine.UI.Button, UnityEngine.UI.Image
//   → 默认选中：Button（更具体）

// ===== Member 类型专属 =====
// 绑定类型 = Member → 仅显示「组件类型」下拉框
// → 选择「字段名称」输入框即可输入自定义字段名

// ===== Element / Component 类型专属 =====
// 绑定类型 = Element 或 Component → 显示「自定义类型名」输入框
// → 输入的类型名将作为生成的类名（如 ShopItemCell）
// → 可包含命名空间（如 MyGame.UI.ShopItemCell）
//
// Element 生成路径：{ScriptPath}/{PanelName}/UIElement/{TypeName}.cs
// Component 生成路径：{ScriptPath}/UIComponent/{TypeName}.cs",
                        Explanation = "重命名 GameObject 为语义化名称（如 Btn_Start），Bind 会自动建议字段名。绑定类型决定了代码生成的范围和文件位置。"
                    },
                    // ================================================================
                    // 5. 步骤 3：生成 UI 代码
                    // ================================================================
                    new()
                    {
                        Title = "步骤 3：生成 UI 代码",
                        Code = @"// ===== 方式 A：Inspector 按钮（推荐） =====
// 选中 UIPanel 预制体 → Inspector 中展开「绑定树」→ 点击「生成 UI 代码」
// → 自动解析 Prefab 层级中所有 Bind 组件并生成对应代码

// ===== 方式 B：右键菜单 =====
// 在 Project 窗口中右键 Prefab → 「UIKit - 生成 UI 代码」
// → 等效于方式 A

// ===== 生成行为 =====
// 1. Panel 用户文件（{PanelName}.cs）
//    → 仅在首次创建时生成，后续重新生成不会覆盖
//    → 包含 OnOpen/OnClose/OnInit 等生命周期方法模板
//
// 2. Panel Designer 文件（{PanelName}.Designer.cs）
//    → 每次生成都会覆盖（包含自动生成标记头）
//    → 包含所有 [SerializeField] 绑定字段 + ClearUIComponents 方法
//
// 3. Element / Component 用户文件
//    → 仅在首次创建时生成
//    → 包含继承自 UIElement/UIComponent 的空类模板
//
// 4. Element / Component Designer 文件
//    → 每次生成都会覆盖
//    → 包含子绑定的 [SerializeField] 字段 + Clear 方法

// ===== 重复生成 =====
// 如果 Designer 文件已存在，重生成会弹出确认对话框：
// 「Designer 文件已存在，重新生成将覆盖...
//   绑定字段会被刷新，手动修改将丢失。」
// → 点「继续」覆盖 Designer，用户文件保持不变",
                        Explanation = "推荐在 Prefab Mode 中配置完所有 Bind 后，直接在 Inspector 中点击「生成 UI 代码」。用户文件只生成一次，Designer 文件每次覆盖。"
                    },
                    // ================================================================
                    // 6. 步骤 4：绑定按钮事件（在 OnInit / OnOpen 中）
                    // ================================================================
                    new()
                    {
                        Title = "步骤 4：在 Panel 中使用绑定字段（事件绑定）",
                        Code = @"// ===== 生成的 Panel 用户文件（MainMenuPanel.cs）=====

public partial class MainMenuPanel : UIPanel
{
    protected override void OnInit(IUIData uiData = null)
    {
        mData = uiData as MainMenuPanelData ?? new MainMenuPanelData();

        // 绑定字段已在 Designer 中以 [SerializeField] 声明
        // Unity 在 Prefab 实例化时自动赋值，无需手动初始化
        mBtn_Start.onClick.AddListener(OnStartClick);
        mBtn_Settings.onClick.AddListener(OnSettingsClick);
        mBtn_Quit.onClick.AddListener(OnQuitClick);

        // Toggle 事件
        mToggle_Music.onValueChanged.AddListener(OnMusicToggle);
    }

    private void OnStartClick()
    {
        // 处理开始按钮点击
    }

    // ===== 也支持通过 UQuery 直接在 __mBind 中查找 =====
    // 如果不在 OnInit 中绑定，也可以在运行时通过 mBind.Get<T>() 查找
    // （详见 UIKitDocLifecycle 中的 __mBind 用法）

    protected override void OnClose()
    {
        // 清理监听器（防止重复注册）
        mBtn_Start.onClick.RemoveAllListeners();
        mBtn_Settings.onClick.RemoveAllListeners();
    }
}


// ===== 生成的 Panel Designer 文件（MainMenuPanel.Designer.cs）=====
// <auto-generated> 此文件由 YokiFrame UIKit 生成，手动修改将丢失

public partial class MainMenuPanel
{
    [SerializeField] private Button mBtn_Start;
    [SerializeField] private Button mBtn_Settings;
    [SerializeField] private Button mBtn_Quit;
    [SerializeField] private Toggle mToggle_Music;

    protected override void ClearUIComponents()
    {
        mBtn_Start = default;
        mBtn_Settings = default;
        mBtn_Quit = default;
        mToggle_Music = default;

        mData = null;
    }
}",
                        Explanation = "绑定字段通过 [SerializeField] 标记，Unity 序列化系统在 Prefab 实例化时自动赋值。在生命周期方法中注册事件即可。注意 OnClose 中清理监听器。"
                    },
                    // ================================================================
                    // 7. 命名规范
                    // ================================================================
                    new()
                    {
                        Title = "命名规范（推荐）",
                        Code = @"// 字段命名 = 类型前缀 + 下划线 + 功能名
// 前缀约定（由 BindNameSuggester 自动建议）：

mBtn_XXX     // Button
mTxt_XXX     // Text / TMP_Text
mImg_XXX     // Image / RawImage
mGo_XXX      // GameObject
mTrans_XXX   // RectTransform / Transform
mInput_XXX   // InputField / TMP_InputField
mToggle_XXX  // Toggle
mSlider_XXX  // Slider
mScroll_XXX  // ScrollRect
mDropdown_XXX// Dropdown / TMP_Dropdown
mLayout_XXX  // LayoutGroup / HorizontalLayoutGroup 等
mAnim_XXX    // Animator

// ===== 命名示例 =====
// GameObject 名        → 生成的字段名
// Btn_Start            → mBtn_Start
// Txt_PlayerName       → mTxt_PlayerName
// Img_Avatar           → mImg_Avatar
// Toggle_BGM           → mToggle_BGM
// Scroll_ItemList      → mScroll_ItemList

// 提示：如果字段名为空，Bind 会自动使用 GameObject 名称填充
// 输入时 BindNameSuggester 会根据组件类型实时建议前缀",
                        Explanation = "统一的命名规范让代码更易读，也便于 BindNameSuggester 自动建议。建议 GameObject 也使用 Txt_/Btn_/Img_ 前缀命名。"
                    },
                    // ================================================================
                    // 8. Element/Component 使用示例
                    // ================================================================
                    new()
                    {
                        Title = "Element 和 Component 的使用",
                        Code = @"// ===== Element 示例（面板内复用）=====
// 假设 ShopItemCell 被定义为 Element，生成文件：
// Scripts/ShopPanel/UIElement/ShopItemCell.cs

public partial class ShopItemCell : UIElement
{
    public void SetData(ShopItemData data)
    {
        mTxt_Name.text = data.Name;
        mTxt_Price.text = data.Price.ToString();
        mImg_Icon.sprite = data.Icon;
    }
}

// 在父 Panel 中使用：
public partial class ShopPanel : UIPanel
{
    // Designer 中已有：private ShopItemCell ShopItemCell;
    // （Unity 序列化自动赋值）

    protected override void OnInit(IUIData uiData = null)
    {
        ShopItemCell.SetData(new ShopItemData { ... });
    }
}


// ===== Component 示例（跨面板复用）=====
// 假设 AvatarView 被定义为 Component，生成文件：
// Scripts/UIComponent/AvatarView.cs

public partial class AvatarView : UIComponent
{
    public void SetAvatar(Sprite sprite, int level)
    {
        mImg_Avatar.sprite = sprite;
        mTxt_Level.text = level.ToString();
    }
}

// 在任意 Panel 中使用：
public partial class ProfilePanel : UIPanel
{
    // Designer 中已有：private AvatarView AvatarView;

    protected override void OnInit(IUIData uiData = null)
    {
        AvatarView.SetAvatar(playerSprite, playerLevel);
    }
}",
                        Explanation = "Element 用于面板内复用的 UI 片段，Component 用于跨面板复用的独立组件。两者都通过 [SerializeField] 自动赋值。"
                    },
                    // ================================================================
                    // 9. 代码生成自定义选项
                    // ================================================================
                    new()
                    {
                        Title = "代码生成选项（PanelCodeGenOptions）",
                        Code = @"// 通过 PanelCodeGenOptions 控制生成内容：

var options = new PanelCodeGenOptions
{
    Level = UILevel.Common,            // UI 层级（影响加载路径）
    IsModal = false,                   // 是否为模态面板
    GenerateLifecycleHooks = true,     // 是否生成 OnWillShow/OnDidShow 等扩展钩子
    GenerateFocusSupport = false,      // 是否生成手柄焦点支持代码
    AnimationDuration = 0.3f,          // 动画时长
};

// 在创建面板时传入选项：
UICodeGenerator.DoCreateCode(prefab, ""MyGame.UI"", options);

// 注意：Inspector 中的「生成 UI 代码」按钮使用默认选项。
// 如需自定义，通过代码调用 UICodeGenerator.DoCreateCode。",
                        Explanation = "通过代码生成选项可控制是否生成生命周期钩子、焦点支持等扩展代码。日常使用默认选项即可。"
                    },
                    // ================================================================
                    // 10. 完整工作流检查清单
                    // ================================================================
                    new()
                    {
                        Title = "完整工作流检查清单",
                        Code = @"// ┌─────────────────────────────────────────────────────┐
// │  UI 绑定工作流 Checklist                             │
// ├─────────────────────────────────────────────────────┤
// │  1. 创建 Panel Prefab（通过 UIKit 工具页或手动）       │
// │  2. 搭建 UI 层级结构                                  │
// │  3. 给需要代码引用的节点添加 Bind（ALT+B）             │
// │  4. 配置每个 Bind 的类型和名称                        │
// │  5. 在 Inspector「绑定树」中检查配置是否正确           │
// │  6. 点击「生成 UI 代码」                              │
// │  7. 打开生成的 .cs 文件，在 OnInit 中绑定事件         │
// │  8. 再次修改 UI 层级后，重新点击「生成 UI 代码」      │
// │     → User 文件不变，Designer 文件自动刷新            │
// └─────────────────────────────────────────────────────┘

// ===== 绑定树检查提示 =====
// Inspector 中「绑定树」区块会显示：
// - 树形结构：所有 Bind 节点的层级关系
// - 统计信息：「共 N 个绑定 (M Member, E Element, C Component, L Leaf)」
// - 校验错误：红色标记的节点（未命名、类型缺失等）
// → 生成代码前务必修正所有红色错误标记",
                        Explanation = "遵循此检查清单可确保绑定配置正确，避免生成后发现遗漏。绑定树视图是调试绑定配置的核心工具。"
                    },
                    // ================================================================
                    // 11. 常见问题
                    // ================================================================
                    new()
                    {
                        Title = "常见问题与解决",
                        Code = @"// Q1: 绑定的字段在代码中为 null？
// A1: 检查是否在 Prefab 上生成了代码。如果是 Scene 中的实例，
//     请先定位到原始 Prefab，再在 Inspector 中点击「生成 UI 代码」。

// Q2: 修改 UI 层级后（添加/删除控件），字段没更新？
// A2: 重新点击「生成 UI 代码」→ Designer 文件会被覆盖刷新。
//     新增的节点如果没有添加 Bind 组件，不会出现在 Designer 中。

// Q3: 生成代码时报「Bind 组件的 Type 为空」？
// A3: 检查该 Bind 所在的 GameObject 上是否挂载了可识别的组件。
//     如果是自定义组件，请在「组件类型」下拉框中手动选择。

// Q4: Element 和 Component 怎么选？
// A4: Element = 只在当前面板复用；Component = 跨多个面板复用。
//     不确定时先用 Element，后续可通过 Inspector 中的类型转换功能升级为 Component。

// Q5: 重复生成后之前写的代码丢了？
// A5: User 文件（*.cs）只生成一次，不会被覆盖。
//     只有 Designer 文件（*.Designer.cs）每次覆盖。
//     所以业务逻辑写在 User 文件的 partial class 中是安全的。

// Q6: 怎么在运行时通过代码查找绑定？
// A6: Designer 中的字段是 [SerializeField] 的，Unity 自动赋值。
//     不需要手动 Find。特殊情况可用 __mBind.Get<T>()，详见生命周期文档。",
                        Explanation = "以上是使用绑定系统时最常见的问题。Designer 文件是自动生成的，不要手动修改；所有业务逻辑应在 User 文件中编写。"
                    }
                }
            };
        }
    }
}
#endif
