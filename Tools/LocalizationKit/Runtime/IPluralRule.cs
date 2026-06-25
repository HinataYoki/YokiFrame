namespace YokiFrame
{
    /// <summary>
    /// 定义指定语言的复数分类规则。
    /// </summary>
    public interface IPluralRule
    {
        /// <summary>
        /// 获取该规则适用的语言。
        /// </summary>
        LanguageId LanguageId { get; }

        /// <summary>
        /// 根据整数数量获取复数分类。
        /// </summary>
        /// <param name="count">数量。</param>
        /// <returns>复数分类。</returns>
        PluralCategory GetCategory(int count);

        /// <summary>
        /// 根据浮点数量获取复数分类。
        /// </summary>
        /// <param name="count">数量。</param>
        /// <returns>复数分类。</returns>
        PluralCategory GetCategory(double count);
    }
}
