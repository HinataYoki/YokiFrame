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
//    - 选择程序集（用于反射绑定）
//    - 自动创建脚本和预制体

// 2. 运行时面板查看
//    - 查看所有打开的面板
//    - 查看堆栈状态
//    - 查看热度值

// 3. UI 绑定工具
//    - 自动生成 UI 组件绑定代码
//    - 支持 Button、Text、Image 等组件",
                        Explanation = "编辑器工具简化 UI 开发流程，提高开发效率。"
                    },
                    new()
                    {
                        Title = "创建面板向导",
                        Code = @"// 创建面板向导配置项：

// 面板名称
// - 输入面板类名（如 MainMenuPanel）
// - 自动生成对应的脚本和预制体

// UI 层级
// - AlwayBottom: 始终在最底层
// - Bg: 背景层
// - Common: 常规层（默认）
// - Pop: 弹窗层
// - AlwayTop: 始终在最顶层

// UIPrefab 路径
// - 默认：Art/UIPrefab/
// - 这是默认加载器的路径前缀
// - 如果使用自定义加载器，可以忽略此设置

// 程序集
// - 选择面板脚本所在的程序集
// - 用于反射绑定时查找类型
// - 默认：Assembly-CSharp",
                        Explanation = "创建向导会自动生成符合规范的面板脚本和预制体。"
                    },
                    new()
                    {
                        Title = "UI 绑定系统",
                        Code = @"// 在 Hierarchy 中选择 UI 组件
// 添加 Bind 组件标记需要绑定的元素

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
                    },
                    new()
                    {
                        Title = "运行时调试",
                        Code = @"// UIDebugOverlay 提供运行时 UI 调试信息

// 显示内容：
// - 当前打开的面板列表
// - 面板堆栈状态
// - 面板热度值
// - 缓存状态

// 启用方式：
// 1. 在 UIRoot 上添加 UIDebugOverlay 组件
// 2. 或通过代码启用：
#if UNITY_EDITOR || DEVELOPMENT_BUILD
UIDebugOverlay.Enable();
#endif

// UIDebugLogger 提供详细的 UI 操作日志
UIDebugLogger.LogLevel = LogLevel.Verbose;",
                        Explanation = "调试工具帮助排查 UI 相关问题。"
                    }
                }
            };
        }
    }
}
#endif
