#if !GODOT
using UnityEngine;
using YokiFrame;

namespace YokiFrame.Unity
{
    public sealed class UnitySceneLoadOperation : IResSceneLoadOperation
    {
        private AsyncOperation mOperation;

        public UnitySceneLoadOperation(AsyncOperation operation)
        {
            mOperation = operation;
        }

        public bool IsSuspended
        {
            get { return mOperation != null && !mOperation.allowSceneActivation; }
        }

        public float Progress
        {
            get { return mOperation != null ? mOperation.progress : 0f; }
        }

        public void SuspendLoad()
        {
            if (mOperation != null)
                mOperation.allowSceneActivation = false;
        }

        public void ResumeLoad()
        {
            if (mOperation != null)
                mOperation.allowSceneActivation = true;
        }

        public void Recycle()
        {
            mOperation = null;
        }
    }
}
#endif
