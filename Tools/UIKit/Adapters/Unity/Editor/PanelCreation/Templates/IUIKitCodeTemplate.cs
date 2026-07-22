#if UNITY_EDITOR
namespace YokiFrame
{
    /// <summary>标识 UIKit 代码生成流水线中的文件角色。</summary>
    public enum UIKitCodeTemplatePart
    {
        /// <summary>Panel 用户 partial 文件。</summary>
        PanelUser,

        /// <summary>Panel Designer partial 文件。</summary>
        PanelDesigner,

        /// <summary>UIElement/UIComponent 用户 partial 文件。</summary>
        BindingUser,

        /// <summary>UIElement/UIComponent Designer partial 文件。</summary>
        BindingDesigner
    }

    /// <summary>向项目自定义模板公开不含 Unity 对象或内部扫描节点的生成上下文。</summary>
    public sealed class UIKitCodeTemplateContext
    {
        /// <summary>创建一个稳定模板上下文。</summary>
        /// <param name="panelName">所属 Panel 类型名。</param>
        /// <param name="scriptNamespace">所属脚本命名空间。</param>
        /// <param name="ownerTypeName">当前文件 owner 类型名。</param>
        /// <param name="ownerKind">Panel、Element 或 Component。</param>
        /// <param name="bindingKind">当前生成绑定类型；Panel 文件为空。</param>
        public UIKitCodeTemplateContext(
            string panelName,
            string scriptNamespace,
            string ownerTypeName,
            string ownerKind,
            string bindingKind)
        {
            PanelName = panelName ?? string.Empty;
            ScriptNamespace = scriptNamespace ?? string.Empty;
            OwnerTypeName = ownerTypeName ?? string.Empty;
            OwnerKind = ownerKind ?? string.Empty;
            BindingKind = bindingKind ?? string.Empty;
        }

        /// <summary>获取所属 Panel 类型名。</summary>
        public string PanelName { get; }

        /// <summary>获取所属脚本命名空间。</summary>
        public string ScriptNamespace { get; }

        /// <summary>获取当前文件 owner 类型名。</summary>
        public string OwnerTypeName { get; }

        /// <summary>获取 Panel、Element 或 Component owner 类别。</summary>
        public string OwnerKind { get; }

        /// <summary>获取当前生成绑定类型；Panel 文件为空。</summary>
        public string BindingKind { get; }
    }

    /// <summary>
    /// 定义 Editor-only UIKit 代码模板转换器。
    /// 模板只转换内存源码，不能替代 CodeGenKit 文件事务或 Prefab 回填。
    /// </summary>
    public interface IUIKitCodeTemplate
    {
        /// <summary>获取满足 SafeId 约束的唯一模板名。</summary>
        string Name { get; }

        /// <summary>获取模板用途说明。</summary>
        string Description { get; }

        /// <summary>转换 CodeGenKit 已生成的单个源码文件。</summary>
        /// <param name="part">当前文件角色。</param>
        /// <param name="context">不含 Unity 对象的生成上下文。</param>
        /// <param name="generatedSource">当前内置生成器产生的源码。</param>
        /// <returns>待交给现有文件事务提交的完整源码。</returns>
        string Transform(
            UIKitCodeTemplatePart part,
            UIKitCodeTemplateContext context,
            string generatedSource);
    }
}
#endif
