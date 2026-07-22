namespace YokiFrame
{
    /// <summary>定义 SceneKit 加载操作的进度、挂起、恢复和回收能力。</summary>
    public interface ISceneLoadOperation
    {
        /// <summary>获取加载进度。</summary>
        float Progress { get; }

        /// <summary>获取是否已挂起。</summary>
        bool IsSuspended { get; }

        /// <summary>挂起加载或场景激活。</summary>
        void SuspendLoad();

        /// <summary>恢复加载或场景激活。</summary>
        void ResumeLoad();

        /// <summary>回收操作占用的托管引用。</summary>
        void Recycle();
    }
}
