namespace YokiFrame
{
    /// <summary>
    /// 按顺序执行子 Action 的容器契约。
    /// </summary>
    public interface ISequence : IAction
    {
        /// <summary>
        /// 添加一个子 Action。
        /// </summary>
        /// <param name="action">要追加的子 Action。</param>
        ISequence Append(IAction action);
    }
}
