namespace YokiFrame
{
    /// <summary>
    /// 单条断言结果；失败不会中断入口，全部断言都会出现在最终结果里。
    /// </summary>
    public sealed class YokiFrameRunAssertion
    {
        /// <summary>创建断言结果。</summary>
        /// <param name="name">断言名。</param>
        /// <param name="passed">是否通过。</param>
        /// <param name="message">补充说明。</param>
        public YokiFrameRunAssertion(string name, bool passed, string message)
        {
            Name = name ?? string.Empty;
            Passed = passed;
            Message = message ?? string.Empty;
        }

        /// <summary>获取断言名。</summary>
        public string Name { get; }

        /// <summary>获取是否通过。</summary>
        public bool Passed { get; }

        /// <summary>获取补充说明。</summary>
        public string Message { get; }
    }
}
