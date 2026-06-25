#if !GODOT
namespace YokiFrame
{
    /// <summary>
    /// 元素绑定策略 - 面板内部可复用的 UI 结构
    /// </summary>
    public sealed class ElementBindStrategy : BindTypeStrategyBase
    {
        /// <summary>
        /// 当前策略处理的绑定类型。
        /// </summary>
        public override BindType Type => BindType.Element;

        /// <summary>
        /// 编辑器中显示的策略名称。
        /// </summary>
        public override string DisplayName => "元素";

        /// <summary>
        /// Element 绑定需要生成独立类文件。
        /// </summary>
        public override bool RequiresClassFile => true;

        /// <summary>
        /// Element 可以继续包含子绑定。
        /// </summary>
        public override bool CanContainChildren => true;

        /// <summary>
        /// 根据绑定组件推断 Element 类型名。
        /// </summary>
        /// <param name="bind">绑定组件。</param>
        /// <returns>推断出的类型名。</returns>
        public override string InferTypeName(AbstractBind bind)
        {
            // Element 类型：使用 GameObject 名称作为类名
            return bind != default ? bind.name : null;
        }

        /// <summary>
        /// 获取 Element 生成代码中的完整类型名。
        /// </summary>
        /// <param name="bindInfo">绑定信息。</param>
        /// <param name="context">代码生成上下文。</param>
        /// <returns>完整类型名。</returns>
        public override string GetFullTypeName(BindCodeInfo bindInfo, IBindCodeGenContext context)
        {
            // Element 使用完整命名空间路径
            return $"{context.ScriptNamespace}.{context.PanelName}{nameof(UIElement)}.{bindInfo.Type}";
        }

        /// <summary>
        /// 获取 Element 生成脚本路径。
        /// </summary>
        /// <param name="bindInfo">绑定信息。</param>
        /// <param name="context">代码生成上下文。</param>
        /// <param name="isDesigner">是否生成 Designer 文件。</param>
        /// <returns>脚本路径。</returns>
        public override string GetScriptPath(BindCodeInfo bindInfo, IBindCodeGenContext context, bool isDesigner)
        {
            var fileName = isDesigner ? $"{bindInfo.Type}.Designer.cs" : $"{bindInfo.Type}.cs";
            return $"{context.ScriptRootPath}/{context.PanelName}/{nameof(UIElement)}/{fileName}";
        }

        /// <summary>
        /// 获取 Element 生成代码的命名空间。
        /// </summary>
        /// <param name="context">代码生成上下文。</param>
        /// <returns>命名空间。</returns>
        public override string GetNamespace(IBindCodeGenContext context)
        {
            return $"{context.ScriptNamespace}.{context.PanelName}{nameof(UIElement)}";
        }

        /// <summary>
        /// 获取 Element 生成类的基类名称。
        /// </summary>
        /// <returns>基类名称。</returns>
        public override string GetBaseClassName() => nameof(UIElement);
    }
}
#endif
