namespace YokiFrame
{
    /// <summary>
    /// 绑定类型策略接口 - 定义每种 BindType 的行为
    /// </summary>
    public interface IBindTypeStrategy
    {
        /// <summary>
        /// 绑定类型
        /// </summary>
        BindType Type { get; }

        /// <summary>
        /// 显示名称
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 是否需要生成独立的类文件
        /// </summary>
        bool RequiresClassFile { get; }

        /// <summary>
        /// 是否可以包含子绑定
        /// </summary>
        bool CanContainChildren { get; }

        /// <summary>
        /// 是否支持类型转换
        /// </summary>
        bool SupportsConversion { get; }

        /// <summary>
        /// 是否应该跳过代码生成
        /// </summary>
        bool ShouldSkipCodeGen { get; }

        /// <summary>
        /// 获取默认类型名（当 Type 为空时的推断逻辑）
        /// </summary>
        /// <param name="bind">绑定组件</param>
        /// <returns>推断的类型名，null 表示无法推断</returns>
        string InferTypeName(AbstractBind bind);

        /// <summary>
        /// 验证子绑定是否允许
        /// </summary>
        /// <param name="childType">子绑定类型</param>
        /// <param name="reason">不允许的原因（输出）</param>
        /// <returns>是否允许</returns>
        bool ValidateChild(BindType childType, out string reason);

        /// <summary>
        /// 获取完整类型名（用于代码生成）
        /// </summary>
        /// <param name="bindInfo">绑定信息</param>
        /// <param name="context">代码生成上下文</param>
        /// <returns>完整类型名</returns>
        string GetFullTypeName(BindCodeInfo bindInfo, IBindCodeGenContext context);

        /// <summary>
        /// 获取脚本文件路径
        /// </summary>
        /// <param name="bindInfo">绑定信息</param>
        /// <param name="context">代码生成上下文</param>
        /// <param name="isDesigner">是否为 Designer 文件</param>
        /// <returns>文件路径</returns>
        string GetScriptPath(BindCodeInfo bindInfo, IBindCodeGenContext context, bool isDesigner);

        /// <summary>
        /// 获取命名空间
        /// </summary>
        /// <param name="context">代码生成上下文</param>
        /// <returns>命名空间</returns>
        string GetNamespace(IBindCodeGenContext context);

        /// <summary>
        /// 获取基类名称
        /// </summary>
        /// <returns>基类名称</returns>
        string GetBaseClassName();
    }

    /// <summary>
    /// 绑定代码生成上下文接口
    /// </summary>
    public interface IBindCodeGenContext
    {
        /// <summary>
        /// 面板名称
        /// </summary>
        string PanelName { get; }

        /// <summary>
        /// 脚本根路径
        /// </summary>
        string ScriptRootPath { get; }

        /// <summary>
        /// 脚本命名空间
        /// </summary>
        string ScriptNamespace { get; }
    }
}
