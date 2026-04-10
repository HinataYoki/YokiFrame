#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 编辑器工具文档
    /// </summary>
    internal static class UIKitDocEditorTools
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "编辑器工具",
                Description = "UIKit 提供面板创建向导和 UI 绑定工具，简化 UI 开发流程。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "打开工具面板",
                        Code = @"// 快捷键：Ctrl+E 打开 YokiFrame Tools 面板
// 选择 UIKit 标签页

// 主要功能：
// 1. 创建面板向导 - 自动生成脚本和预制体
// 2. UI 绑定工具 - 自动生成组件引用代码
// 3. 代码生成模板选择 - 自定义生成的代码样式",
                        Explanation = "所有 UIKit 编辑器功能集中在工具面板中。"
                    },
                    new()
                    {
                        Title = "创建面板向导",
                        Code = @"// 创建面板向导配置项：

// 面板名称：输入面板类名（如 MainMenuPanel）

// UI 层级：
// - AlwayBottom: 始终在最底层
// - Bg: 背景层
// - Common: 常规层（默认）
// - Pop: 弹窗层
// - AlwayTop: 始终在最顶层

// UIPrefab 路径：默认 Art/UIPrefab/（自定义加载器可忽略）

// 程序集：面板脚本所在的程序集（默认 Assembly-CSharp）

// 代码生成模板：选择生成的代码样式",
                        Explanation = "向导会自动生成符合规范的面板脚本和预制体。"
                    },
                    new()
                    {
                        Title = "UI 绑定命名规范",
                        Code = @"// 在 Hierarchy 中添加 Bind 组件标记需要绑定的元素
// 推荐命名规范：

// mBtn_XXX   -> Button
// mTxt_XXX   -> Text/TMP_Text
// mImg_XXX   -> Image
// mGo_XXX    -> GameObject
// mTrans_XXX -> Transform
// mInput_XXX -> InputField
// mToggle_XXX -> Toggle
// mSlider_XXX -> Slider
// mScroll_XXX -> ScrollRect

// 生成的代码使用 [SerializeField] 标记
// Unity 在 Prefab 实例化时自动绑定引用",
                        Explanation = "统一的命名规范让代码更易读，也便于自动生成。"
                    }
                }
            };
        }
    }
}
#endif
