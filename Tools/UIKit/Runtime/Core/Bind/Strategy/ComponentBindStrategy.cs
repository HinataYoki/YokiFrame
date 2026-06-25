#if !GODOT
namespace YokiFrame
{
    /// <summary>
    /// 组件绑定策略 - 跨面板复用的独立 UI 组件
    /// </summary>
    public sealed class ComponentBindStrategy : BindTypeStrategyBase
    {
        /// <summary>
        /// 当前策略处理的绑定类型。
        /// </summary>
        public override BindType Type => BindType.Component;

        /// <summary>
        /// 编辑器中显示的策略名称。
        /// </summary>
        public override string DisplayName => "组件";

        /// <summary>
        /// Component 绑定需要生成独立类文件。
        /// </summary>
        public override bool RequiresClassFile => true;

        /// <summary>
        /// Component 可以继续包含子绑定。
        /// </summary>
        public override bool CanContainChildren => true;

        /// <summary>
        /// 根据绑定组件推断 Component 类型名。
        /// </summary>
        /// <param name="bind">绑定组件。</param>
        /// <returns>推断出的类型名。</returns>
        public override string InferTypeName(AbstractBind bind)
        {
            // Component 类型：使用 GameObject 名称作为类名
            return bind != default ? bind.name : null;
        }

        /// <summary>
        /// 验证 Component 下的子绑定类型是否合法。
        /// </summary>
        /// <param name="childType">子绑定类型。</param>
        /// <param name="reason">不合法时的原因。</param>
        /// <returns>合法返回 true，否则返回 false。</returns>
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

        /// <summary>
        /// 获取 Component 生成代码中的完整类型名。
        /// </summary>
        /// <param name="bindInfo">绑定信息。</param>
        /// <param name="context">代码生成上下文。</param>
        /// <returns>完整类型名。</returns>
        public override string GetFullTypeName(BindCodeInfo bindInfo, IBindCodeGenContext context)
        {
            // Component 直接使用类型名（在根命名空间下）
            return bindInfo.Type;
        }

        /// <summary>
        /// 获取 Component 生成脚本路径。
        /// </summary>
        /// <param name="bindInfo">绑定信息。</param>
        /// <param name="context">代码生成上下文。</param>
        /// <param name="isDesigner">是否生成 Designer 文件。</param>
        /// <returns>脚本路径。</returns>
        public override string GetScriptPath(BindCodeInfo bindInfo, IBindCodeGenContext context, bool isDesigner)
        {
            var fileName = isDesigner ? $"{bindInfo.Type}.Designer.cs" : $"{bindInfo.Type}.cs";
            return $"{context.ScriptRootPath}/{nameof(UIComponent)}/{fileName}";
        }

        /// <summary>
        /// 获取 Component 生成代码的命名空间。
        /// </summary>
        /// <param name="context">代码生成上下文。</param>
        /// <returns>命名空间。</returns>
        public override string GetNamespace(IBindCodeGenContext context)
        {
            return context.ScriptNamespace;
        }

        /// <summary>
        /// 获取 Component 生成类的基类名称。
        /// </summary>
        /// <returns>基类名称。</returns>
        public override string GetBaseClassName() => nameof(UIComponent);
    }
}
#endif
