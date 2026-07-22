namespace YokiFrame
{
    /// <summary>
    /// 向调度、所有权和诊断代码提供无分配的子 Action 索引访问。
    /// </summary>
    internal interface IActionContainerInternal
    {
        /// <summary>获取当前直接子 Action 数量。</summary>
        int ChildCount { get; }

        /// <summary>
        /// 获取指定直接子 Action。
        /// </summary>
        /// <param name="index">从零开始的子索引。</param>
        /// <returns>对应子 Action。</returns>
        IAction GetChild(int index);
    }
}
