namespace YokiFrame
{
    /// <summary>
    /// 定义同时推进多个子 Action 的 fluent 容器。
    /// </summary>
    public interface IParallel : ISequence
    {
        /// <summary>
        /// 追加一个并行子 Action，并保持并行容器返回类型。
        /// </summary>
        /// <param name="action">待追加 Action。</param>
        /// <returns>当前并行容器。</returns>
        new IParallel Append(IAction action);
    }
}
