namespace YokiFrame
{
    /// <summary>
    /// 可选的 Kit snapshot 失效标记来源。实现者应返回轻量、稳定、能代表当前 state snapshot 变化的 key。
    /// </summary>
    public interface IKitSnapshotInvalidationProvider
    {
        /// <summary>
        /// 获取当前 snapshot 的失效 key。
        /// </summary>
        /// <returns>当前 snapshot 变化标记。</returns>
        string GetSnapshotInvalidationKey();
    }
}
