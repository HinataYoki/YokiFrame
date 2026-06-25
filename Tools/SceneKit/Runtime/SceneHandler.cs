using System;

namespace YokiFrame
{
    /// <summary>
    /// 保存单个场景加载生命周期中的状态、数据和回调。
    /// </summary>
    public sealed class SceneHandler
    {
        private const float MIN_PROGRESS = 0f;
        private const float MAX_PROGRESS = 1f;

        private Action<SceneHandler> mLoadedCallbacks;

        /// <summary>
        /// 获取场景名称。
        /// </summary>
        public string SceneName { get; internal set; }

        /// <summary>
        /// 获取场景构建索引。
        /// </summary>
        public int BuildIndex { get; internal set; }

        /// <summary>
        /// 获取引擎后端返回的场景句柄。
        /// </summary>
        public SceneHandle Scene { get; internal set; }

        /// <summary>
        /// 获取当前场景状态。
        /// </summary>
        public SceneState State { get; private set; }

        /// <summary>
        /// 获取当前加载进度，范围为 0 到 1。
        /// </summary>
        public float Progress { get; private set; }

        /// <summary>
        /// 获取加载是否已挂起。
        /// </summary>
        public bool IsSuspended { get; internal set; }

        /// <summary>
        /// 获取该场景是否为预加载场景。
        /// </summary>
        public bool IsPreloaded { get; internal set; }

        /// <summary>
        /// 获取场景加载模式。
        /// </summary>
        public SceneLoadMode LoadMode { get; internal set; }

        /// <summary>
        /// 获取场景附加数据。
        /// </summary>
        public ISceneData SceneData { get; internal set; }

        /// <summary>
        /// 获取后端加载操作。
        /// </summary>
        public ISceneLoadOperation Operation { get; internal set; }

        /// <summary>
        /// 添加场景加载完成回调；场景已加载时立即调用。
        /// </summary>
        /// <param name="callback">加载完成回调。</param>
        public void AddLoadedCallback(Action<SceneHandler> callback)
        {
            if (callback == null)
                return;

            if (State == SceneState.Loaded)
            {
                callback(this);
                return;
            }

            mLoadedCallbacks += callback;
        }

        /// <summary>
        /// 调用并清空已登记的加载完成回调。
        /// </summary>
        public void InvokeLoadedCallbacks()
        {
            var callbacks = mLoadedCallbacks;
            mLoadedCallbacks = null;
            if (callbacks != null)
                callbacks(this);
        }

        /// <summary>
        /// 更新加载进度，并限制在 0 到 1。
        /// </summary>
        /// <param name="progress">加载进度。</param>
        public void UpdateProgress(float progress)
        {
            if (progress < MIN_PROGRESS)
            {
                Progress = MIN_PROGRESS;
                return;
            }

            Progress = progress > MAX_PROGRESS ? MAX_PROGRESS : progress;
        }

        /// <summary>
        /// 设置当前场景状态。
        /// </summary>
        /// <param name="state">场景状态。</param>
        public void SetState(SceneState state)
        {
            State = state;
        }

        internal void Reset(
            string sceneName,
            int buildIndex,
            SceneLoadMode mode,
            ISceneData data,
            bool isPreload)
        {
            SceneName = sceneName;
            BuildIndex = buildIndex;
            Scene = default(SceneHandle);
            LoadMode = mode;
            SceneData = data;
            IsPreloaded = isPreload;
            IsSuspended = false;
            Operation = null;
            mLoadedCallbacks = null;
            UpdateProgress(MIN_PROGRESS);
            SetState(SceneState.Loading);
        }

        internal void MarkUnloaded()
        {
            if (Operation != null)
                Operation.Recycle();

            Operation = null;
            IsSuspended = false;
            IsPreloaded = false;
            UpdateProgress(MIN_PROGRESS);
            SetState(SceneState.Unloaded);
            mLoadedCallbacks = null;
        }
    }
}
