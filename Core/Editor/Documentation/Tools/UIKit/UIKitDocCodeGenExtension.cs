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
                Description = "支持自定义代码生成模板，通过继承 DefaultUICodeGenTemplate 或实现 IUICodeGenTemplate 接口来定制生成的代码样式。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "扩展机制概述",
                        Code = @"// UIKit 代码生成采用模板模式，支持两种扩展方式：

// 1. IUICodeGenTemplate - 代码生成模板接口
//    控制生成的代码结构和样式

// 2. IBindTypeStrategy - 绑定类型策略接口
//    控制绑定类型的行为和验证

// 核心组件：
// - UICodeGenTemplateRegistry: 模板注册表
// - BindStrategyRegistry: 策略注册表
// - UICodeGenContext: 统一的生成上下文
// - UICodeGenConstants: 共享常量",
                        Explanation = "UIKit 提供两个扩展点，分别控制代码生成样式和绑定类型行为。"
                    },
                    new()
                    {
                        Title = "继承 DefaultUICodeGenTemplate（推荐）",
                        Code = @"// 推荐方式：继承默认模板，只覆盖需要修改的方法

public class MyCustomTemplate : DefaultUICodeGenTemplate
{
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
        cls.ProtectedOverrideVoid(""OnInit"", method =>
        {
            method.WithParameter(nameof(IUIData), ""uiData"", ""null"");
            method.WithBody(body =>
            {
                body.Custom($""mData = uiData as {dataName} ?? new {dataName}();"");
            });
        });
    }
}",
                        Explanation = "继承 DefaultUICodeGenTemplate 可以复用默认实现，只需覆盖需要修改的方法。"
                    },
                    new()
                    {
                        Title = "模板自动注册与切换",
                        Code = @"// 模板会被自动发现和注册，无需手动注册

// 在 UIKit 工具面板中选择模板：
// 1. 打开 YokiFrame 工具窗口 (Ctrl+E)
// 2. 切换到 UIKit 标签页
// 3. 在「创建面板」中找到「代码生成模板」下拉框
// 4. 选择需要的模板，配置会自动保存

// 代码中设置激活的模板：
UICodeGenTemplateRegistry.SetActiveTemplate(""MyCustom"");

// 获取当前激活的模板：
var template = UICodeGenTemplateRegistry.ActiveTemplate;
var templateName = UICodeGenTemplateRegistry.ActiveTemplateName;

// 获取所有已注册的模板：
foreach (var name in UICodeGenTemplateRegistry.GetAllTemplateNames())
{
    var t = UICodeGenTemplateRegistry.Get(name);
    Debug.Log($""模板: {name} - {t.Description}"");
}",
                        Explanation = "模板会被自动发现注册，用户可在 UIKit 工具面板的下拉框中选择模板。"
                    },
                    new()
                    {
                        Title = "UICodeGenContext 上下文",
                        Code = @"// UICodeGenContext 是统一的生成上下文

public class UICodeGenContext : IBindCodeGenContext
{
    // 基本信息
    public string PanelName { get; set; }
    public string ScriptRootPath { get; set; }
    public string ScriptNamespace { get; set; }
    
    // 绑定信息
    public BindCodeInfo BindCodeInfo { get; set; }
    
    // 路径辅助方法
    public string GetPanelFilePath();
    public string GetPanelDesignerPath();
    public bool FileExists(string path);
    
    // 类型生成跟踪（避免重复生成）
    public bool IsTypeGenerated(BindType bindType, string typeName);
    public bool MarkTypeGenerated(BindType bindType, string typeName);
}",
                        Explanation = "UICodeGenContext 提供生成所需的所有信息和辅助方法。"
                    }
                }
            };
        }
    }
}
#endif
