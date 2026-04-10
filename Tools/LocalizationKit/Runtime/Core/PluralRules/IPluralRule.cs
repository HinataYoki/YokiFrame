namespace YokiFrame
{
    /// <summary>
    /// 复数规则接口
    /// 根据数量返回对应的复数类别
    /// </summary>
    public interface IPluralRule
    {
        /// <summary>
        /// 获取适用的语言
        /// </summary>
        LanguageId LanguageId { get; }

        /// <summary>
        /// 根据数量获取复数类别
        /// </summary>
        /// <param name="count">数量</param>
        /// <returns>复数类别</returns>
        PluralCategory GetCategory(int count);

        /// <summary>
        /// 根据数量获取复数类别（支持小数）
        /// </summary>
        /// <param name="count">数量</param>
        /// <returns>复数类别</returns>
        PluralCategory GetCategory(double count);
    }
}
