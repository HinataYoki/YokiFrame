namespace YokiFrame
{
    /// <summary>
    /// 定义按顺序持有子 Action 的 fluent 容器。
    /// </summary>
    public interface ISequence : IAction
    {
        /// <summary>
        /// 追加一个尚未被其它父容器或 controller 拥有的 Action。
        /// </summary>
        /// <param name="action">待追加 Action。</param>
        /// <returns>当前容器，供 fluent 调用继续装配。</returns>
        ISequence Append(IAction action);
    }
}
