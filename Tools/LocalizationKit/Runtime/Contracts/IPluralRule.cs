namespace YokiFrame
{
    /// <summary>定义一种语言的复数分类规则。</summary>
    public interface IPluralRule
    {
        /// <summary>获取规则所属语言。</summary>
        LanguageId LanguageId { get; }
        /// <summary>按整数数量计算复数分类。</summary>
        PluralCategory GetCategory(int count);
        /// <summary>按浮点数量计算复数分类。</summary>
        PluralCategory GetCategory(double count);
    }
}
