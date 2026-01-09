#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 代码生成扩展文档
    /// </summary>
    internal static class UIKitDocCodeGenExtension
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "代码生成扩展",
                Description = "UIKit 支持自定义代码生成模板，用户可以继承 DefaultUICodeGenTemplate 或实现 IUICodeGenTemplate 接口来定制生成的代码样式。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "扩展机制概述",
                        Code = @"// UIKit 代码生成采用模板模式，支持两种扩展方式：

// 1. IUICodeGenTemplate - 代码生成模板接口
//    控制生成的代码结构和样式
//    适用：自定义代码风格、添加额外生成内容

// 2. IBindTypeStrategy - 绑定类型策略接口
//    控制绑定类型的行为和验证
//    适用：添加新的绑定类型

// 核心组件：
// - UICodeGenTemplateRegistry: 模板注册表，管理所有模板
// - BindStrategyRegistry: 策略注册表，管理绑定类型策略
// - UICodeGenContext: 统一的生成上下文，包含所有生成信息
// - UICodeGenConstants: 共享常量（using 声明等）",
                        Explanation = "UIKit 提供两个扩展点，分别控制代码生成样式和绑定类型行为。"
                    },
                    new()
                    {
                        Title = "继承 DefaultUICodeGenTemplate（推荐）",
                        Code = @"// 推荐方式：继承 DefaultUICodeGenTemplate，只覆盖需要修改的方法

public class MyCustomTemplate : DefaultUICodeGenTemplate
{
    // 覆盖模板名称
    public override string TemplateName => ""MyCustom"";
    public override string Description => ""我的自定义代码生成模板"";
    
    // 覆盖文件头样式
    protected override void WriteUserFileHeader(RootCode rootCode)
    {
        rootCode
            .Custom($""// 自定义文件头"")
            .Custom($""// 生成时间: {DateTime.Now:yyyy-MM-dd}"")
            .EmptyLine();
    }
    
    // 覆盖生命周期方法生成
    protected override void WritePanelLifecycleMethods(ClassCodeScope cls, string dataName)
    {
        // 只生成 OnInit
        cls.ProtectedOverrideVoid(""OnInit"", method =>
        {
            method.WithParameter(nameof(IUIData), ""uiData"", ""null"");
            method.WithBody(body =>
            {
                body.Custom($""mData = uiData as {dataName} ?? new {dataName}();"");
            });
        });
    }
    
    // 不生成生命周期钩子
    protected override void WritePanelLifecycleHooks(ClassCodeScope cls)
    {
        // 留空，不生成
    }
}",
                        Explanation = "继承 DefaultUICodeGenTemplate 可以复用默认实现，只需覆盖需要修改的方法。"
                    },
                    new()
                    {
                        Title = "实现 IUICodeGenTemplate 接口",
                        Code = @"// 完全自定义：实现 IUICodeGenTemplate 接口

public interface IUICodeGenTemplate
{
    // 模板名称（用于配置选择）
    string TemplateName { get; }
    
    // 模板描述
    string Description { get; }
    
    // 生成 Panel 用户文件
    void WritePanel(UICodeGenContext context);
    
    // 生成 Panel Designer 文件
    void WritePanelDesigner(UICodeGenContext context);
    
    // 生成绑定类型用户文件（Element/Component）
    void WriteBindTypeUserFile(
        UICodeGenContext context, 
        BindCodeInfo bindInfo, 
        IBindTypeStrategy strategy);
    
    // 生成绑定类型 Designer 文件
    void WriteBindTypeDesignerFile(
        UICodeGenContext context, 
        BindCodeInfo bindInfo, 
        IBindTypeStrategy strategy);
    
    // 递归生成绑定类型代码
    void WriteBindTypeCode(BindCodeInfo bindInfo, UICodeGenContext context);
}",
                        Explanation = "实现 IUICodeGenTemplate 接口可以完全控制代码生成逻辑。"
                    },
                    new()
                    {
                        Title = "模板自动注册与切换",
                        Code = @"// 模板会被自动发现和注册，无需手动注册

// UICodeGenTemplateRegistry 在初始化时会：
// 1. 注册默认模板 (DefaultUICodeGenTemplate)
// 2. 使用 TypeCache 扫描所有实现 IUICodeGenTemplate 的类型
// 3. 自动实例化并注册
// 4. 从 UIKitCreateConfig 加载用户选择的模板

// 在 UIKit 工具面板中选择模板：
// 1. 打开 YokiFrame 工具窗口
// 2. 切换到 UIKit 标签页
// 3. 在「创建面板」中找到「代码生成模板」下拉框
// 4. 选择需要的模板，配置会自动保存

// 代码中设置激活的模板（会自动保存到配置）：
UICodeGenTemplateRegistry.SetActiveTemplate(""MyCustom"");

// 获取当前激活的模板名称：
var templateName = UICodeGenTemplateRegistry.ActiveTemplateName;

// 获取当前激活的模板实例（永不为 null）：
var template = UICodeGenTemplateRegistry.ActiveTemplate;

// 获取所有已注册的模板：
foreach (var name in UICodeGenTemplateRegistry.GetAllTemplateNames())
{
    var t = UICodeGenTemplateRegistry.Get(name);
    Debug.Log($""模板: {name} - {t.Description}"");
}",
                        Explanation = "模板会被自动发现注册，用户可在 UIKit 工具面板的下拉框中选择模板，配置会自动保存。"
                    },
                    new()
                    {
                        Title = "UICodeGenContext 上下文",
                        Code = @"// UICodeGenContext 是统一的生成上下文，包含所有生成信息

public class UICodeGenContext : IBindCodeGenContext
{
    // 基本信息
    public string PanelName { get; set; }
    public string ScriptRootPath { get; set; }
    public string ScriptNamespace { get; set; }
    
    // 绑定信息
    public BindCodeInfo BindCodeInfo { get; set; }
    
    // 代码生成选项
    public PanelCodeGenOptions Options { get; set; }
    
    // 已生成类型跟踪（避免重复生成）
    public HashSet<string> GeneratedTypes { get; }
    
    // 路径辅助方法
    public string GetPanelFilePath();
    public string GetPanelDesignerPath();
    public bool FileExists(string path);
    
    // 类型生成跟踪
    public bool IsTypeGenerated(BindType bindType, string typeName);
    public bool MarkTypeGenerated(BindType bindType, string typeName);
    
    // 工厂方法
    public static UICodeGenContext Create(string panelName, ...);
}

// 使用示例：
public void WritePanel(UICodeGenContext context)
{
    var path = context.GetPanelFilePath();
    if (context.FileExists(path)) return; // 不覆盖用户文件
    
    // 检查类型是否已生成
    if (!context.MarkTypeGenerated(BindType.Element, ""MyElement""))
    {
        return; // 已生成，跳过
    }
    
    // 生成代码...
}",
                        Explanation = "UICodeGenContext 提供生成所需的所有信息、辅助方法和类型跟踪功能。"
                    },
                    new()
                    {
                        Title = "UICodeGenConstants 共享常量",
                        Code = @"// UICodeGenConstants 提供共享的常量定义

public static class UICodeGenConstants
{
    // Using 声明
    public const string USING_UNITY_ENGINE = ""UnityEngine"";
    public const string USING_UNITY_UI = ""UnityEngine.UI"";
    public const string USING_YOKI_FRAME = ""YokiFrame"";
    public const string USING_SYSTEM = ""System"";
    
    // 文件扩展名
    public const string DESIGNER_SUFFIX = "".Designer.cs"";
    public const string SCRIPT_SUFFIX = "".cs"";
}

// 使用示例：
root.Using(UICodeGenConstants.USING_UNITY_ENGINE)
    .Using(UICodeGenConstants.USING_YOKI_FRAME);",
                        Explanation = "使用共享常量避免魔法字符串，保持代码一致性。"
                    },
                    new()
                    {
                        Title = "完整扩展示例：极简模板",
                        Code = @"// 参考 MinimalUICodeGenTemplate 示例

public class MinimalUICodeGenTemplate : DefaultUICodeGenTemplate
{
    public override string TemplateName => ""Minimal"";
    public override string Description => ""极简代码生成样式"";
    
    // 简化文件头
    protected override void WriteUserFileHeader(RootCode rootCode)
    {
        rootCode.Custom($""// {DateTime.Now:yyyy-MM-dd}"").EmptyLine();
    }
    
    protected override void WriteAutoGeneratedHeader(RootCode rootCode)
    {
        rootCode.Custom(""// <auto-generated />"").EmptyLine();
    }
    
    // 只生成必要的生命周期方法
    protected override void WritePanelLifecycleMethods(ClassCodeScope cls, string dataName)
    {
        cls.ProtectedOverrideVoid(""OnInit"", method =>
        {
            method.WithParameter(nameof(IUIData), ""uiData"", ""null"");
            method.WithBody(body =>
            {
                body.Custom($""mData = uiData as {dataName} ?? new {dataName}();"");
            });
        });
        cls.EmptyLine();
        cls.ProtectedOverrideVoid(""OnClose"", method =>
        {
            method.WithBody(_ => { });
        });
    }
    
    // 不生成钩子和焦点支持
    protected override void WritePanelLifecycleHooks(ClassCodeScope cls) { }
    protected override void WritePanelFocusSupport(ClassCodeScope cls) { }
    
    // 简化 Clear 方法
    protected override void WriteClearMethodBody(ICodeScope body, BindCodeInfo bindCodeInfo, bool clearData)
    {
        foreach (var bindInfo in bindCodeInfo.MemberDic.Values.Where(b => !b.RepeatElement))
        {
            body.Custom($""{bindInfo.Name} = default;"");
        }
        if (clearData) body.Custom(""mData = null;"");
    }
}",
                        Explanation = "MinimalUICodeGenTemplate 展示了如何通过继承创建简化版本的代码生成模板。"
                    }
                }
            };
        }
    }
}
#endif
