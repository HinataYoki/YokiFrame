namespace YokiFrame
{
    /// <summary>
    /// 成员绑定策略 - 直接引用 Unity 组件
    /// </summary>
    public sealed class MemberBindStrategy : BindTypeStrategyBase
    {
        public override BindType Type => BindType.Member;
        public override string DisplayName => "成员";
        public override bool RequiresClassFile => false;
        public override bool CanContainChildren => false;

        public override string InferTypeName(AbstractBind bind)
        {
            // Member 类型：查找最后一个非 AbstractBind 组件
            return InferTypeFromComponents(bind);
        }

        public override string GetFullTypeName(BindCodeInfo bindInfo, IBindCodeGenContext context)
        {
            // Member 直接使用组件类型
            return bindInfo.Type;
        }

        public override string GetBaseClassName() => null;
    }
}
