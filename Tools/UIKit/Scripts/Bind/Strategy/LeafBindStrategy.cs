namespace YokiFrame
{
    /// <summary>
    /// 叶子绑定策略 - 标记节点，不生成代码
    /// </summary>
    public sealed class LeafBindStrategy : BindTypeStrategyBase
    {
        public override BindType Type => BindType.Leaf;
        public override string DisplayName => "叶子";
        public override bool RequiresClassFile => false;
        public override bool CanContainChildren => false;
        public override bool SupportsConversion => false;
        public override bool ShouldSkipCodeGen => true;

        public override string InferTypeName(AbstractBind bind)
        {
            // Leaf 不需要类型
            return null;
        }

        public override string GetFullTypeName(BindCodeInfo bindInfo, IBindCodeGenContext context)
        {
            return null;
        }
    }
}
