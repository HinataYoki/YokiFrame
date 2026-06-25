namespace YokiFrame
{
    /// <summary>
    /// 定义 ResKit 场景加载操作的进度、挂起、恢复和回收能力。
    /// </summary>
    public interface IResSceneLoadOperation
    {
        bool IsSuspended { get; }
        float Progress { get; }
        void SuspendLoad();
        void ResumeLoad();
        void Recycle();
    }
}
