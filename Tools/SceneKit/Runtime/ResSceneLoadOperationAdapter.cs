namespace YokiFrame
{
    internal sealed class ResSceneLoadOperationAdapter : ISceneLoadOperation
    {
        private readonly IResSceneLoadOperation mOperation;

        public ResSceneLoadOperationAdapter(IResSceneLoadOperation operation)
        {
            mOperation = operation;
        }

        public bool IsSuspended
        {
            get { return mOperation != null && mOperation.IsSuspended; }
        }

        public float Progress
        {
            get { return mOperation != null ? mOperation.Progress : 0f; }
        }

        public void SuspendLoad()
        {
            if (mOperation != null)
                mOperation.SuspendLoad();
        }

        public void ResumeLoad()
        {
            if (mOperation != null)
                mOperation.ResumeLoad();
        }

        public void Recycle()
        {
            if (mOperation != null)
                mOperation.Recycle();
        }
    }
}
