namespace YokiFrame
{
    /// <summary>
    /// 并行执行子 Action 的容器契约。
    /// </summary>
    public interface IParallel : ISequence
    {
        /// <summary>
        /// 添加一个子 Action，并保持并行容器链式返回类型。
        /// </summary>
        /// <param name="action">要追加的子 Action。</param>
        new IParallel Append(IAction action);
    }
}
