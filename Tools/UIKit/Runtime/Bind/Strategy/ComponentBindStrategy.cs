namespace YokiFrame
{
    /// <summary>
    /// 组件绑定策略 - 跨面板复用的独立 UI 组件
    /// </summary>
    public sealed class ComponentBindStrategy : BindTypeStrategyBase
    {
        public override BindType Type => BindType.Component;
        public override string DisplayName => "组件";
        public override bool RequiresClassFile => true;
        public override bool CanContainChildren => true;

        public override string InferTypeName(AbstractBind bind)
        {
            // Component 类型：使用 GameObject 名称作为类名
            return bind?.name;
        }

        public override bool ValidateChild(BindType childType, out string reason)
        {
            // Component 下不能有 Element
            if (childType == BindType.Element)
            {
                reason = "Component 组件下不支持定义 Element 元素，Element 必须归属于 Panel";
                return false;
            }
            reason = null;
            return true;
        }

        public override string GetFullTypeName(BindCodeInfo bindInfo, IBindCodeGenContext context)
        {
            // Component 直接使用类型名（在根命名空间下）
            return bindInfo.Type;
        }

        public override string GetScriptPath(BindCodeInfo bindInfo, IBindCodeGenContext context, bool isDesigner)
        {
            var fileName = isDesigner ? $"{bindInfo.Type}.Designer.cs" : $"{bindInfo.Type}.cs";
            return $"{context.ScriptRootPath}/{nameof(UIComponent)}/{fileName}";
        }

        public override string GetNamespace(IBindCodeGenContext context)
        {
            return context.ScriptNamespace;
        }

        public override string GetBaseClassName() => nameof(UIComponent);
    }
}
