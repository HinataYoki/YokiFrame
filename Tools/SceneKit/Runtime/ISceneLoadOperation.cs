namespace YokiFrame
{
    /// <summary>
    /// 定义场景加载操作的进度、挂起、恢复和回收能力。
    /// </summary>
    public interface ISceneLoadOperation
    {
        /// <summary>
        /// 获取操作是否已挂起。
        /// </summary>
        bool IsSuspended { get; }

        /// <summary>
        /// 获取加载进度。
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// 挂起加载。
        /// </summary>
        void SuspendLoad();

        /// <summary>
        /// 恢复加载。
        /// </summary>
        void ResumeLoad();

        /// <summary>
        /// 回收加载操作占用的资源。
        /// </summary>
        void Recycle();
    }
}
