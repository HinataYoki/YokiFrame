namespace YokiFrame
{
    /// <summary>定义场景加载操作的进度、挂起、恢复和回收能力。</summary>
    public interface IResSceneLoadOperation
    {
        /// <summary>获取当前进度，范围为 0 到 1。</summary>
        float Progress { get; }

        /// <summary>获取操作是否已经挂起。</summary>
        bool IsSuspended { get; }

        /// <summary>挂起场景激活或加载。</summary>
        void SuspendLoad();

        /// <summary>恢复场景激活或加载。</summary>
        void ResumeLoad();

        /// <summary>释放操作对宿主异步对象的引用。</summary>
        void Recycle();
    }
}
