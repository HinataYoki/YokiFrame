namespace YokiFrame
{
    /// <summary>把 ResKit 场景加载操作适配为 SceneKit 操作。</summary>
    internal sealed class ResSceneLoadOperationAdapter : ISceneLoadOperation
    {
        private readonly IResSceneLoadOperation mOperation;

        /// <summary>创建操作适配器。</summary>
        /// <param name="operation">ResKit Provider 返回的操作。</param>
        internal ResSceneLoadOperationAdapter(IResSceneLoadOperation operation)
        {
            mOperation = operation;
        }

        /// <inheritdoc />
        public float Progress => mOperation == null ? 0f : mOperation.Progress;

        /// <inheritdoc />
        public bool IsSuspended => mOperation != null && mOperation.IsSuspended;

        /// <inheritdoc />
        public void SuspendLoad()
        {
            mOperation?.SuspendLoad();
        }

        /// <inheritdoc />
        public void ResumeLoad()
        {
            mOperation?.ResumeLoad();
        }

        /// <inheritdoc />
        public void Recycle()
        {
            mOperation?.Recycle();
        }
    }
}
