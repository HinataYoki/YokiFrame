namespace YokiFrame
{
    /// <summary>
    /// 复数类别枚举，遵循 ICU 复数规则
    /// 不同语言有不同的复数形式规则
    /// </summary>
    public enum PluralCategory
    {
        /// <summary>零 (0)</summary>
        Zero = 0,
        
        /// <summary>单数 (1)</summary>
        One = 1,
        
        /// <summary>双数 (2)，某些语言如阿拉伯语使用</summary>
        Two = 2,
        
        /// <summary>少数 (2-4)，某些语言如俄语、波兰语使用</summary>
        Few = 3,
        
        /// <summary>多数 (5+)，某些语言使用</summary>
        Many = 4,
        
        /// <summary>其他/默认，所有语言都有此类别</summary>
        Other = 5,
    }
}
