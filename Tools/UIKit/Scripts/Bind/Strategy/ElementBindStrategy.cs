namespace YokiFrame
{
    /// <summary>
    /// 元素绑定策略 - 面板内部可复用的 UI 结构
    /// </summary>
    public sealed class ElementBindStrategy : BindTypeStrategyBase
    {
        public override BindType Type => BindType.Element;
        public override string DisplayName => "元素";
        public override bool RequiresClassFile => true;
        public override bool CanContainChildren => true;

        public override string InferTypeName(AbstractBind bind)
        {
            // Element 类型：使用 GameObject 名称作为类名
            return bind?.name;
        }

        public override string GetFullTypeName(BindCodeInfo bindInfo, IBindCodeGenContext context)
        {
            // Element 使用完整命名空间路径
            return $"{context.ScriptNamespace}.{context.PanelName}{nameof(UIElement)}.{bindInfo.Type}";
        }

        public override string GetScriptPath(BindCodeInfo bindInfo, IBindCodeGenContext context, bool isDesigner)
        {
            var fileName = isDesigner ? $"{bindInfo.Type}.Designer.cs" : $"{bindInfo.Type}.cs";
            return $"{context.ScriptRootPath}/{context.PanelName}/{nameof(UIElement)}/{fileName}";
        }

        public override string GetNamespace(IBindCodeGenContext context)
        {
            return $"{context.ScriptNamespace}.{context.PanelName}{nameof(UIElement)}";
        }

        public override string GetBaseClassName() => nameof(UIElement);
    }
}
