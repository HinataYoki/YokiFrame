using System;

namespace YokiFrame
{
    /// <summary>保存单个场景加载生命周期中的状态、数据、操作和回调。</summary>
    public sealed class SceneHandler
    {
        private Action<SceneHandler> mLoadCallbacks;
        private Action mUnloadCallbacks;

        /// <summary>获取场景名称。</summary>
        public string SceneName { get; internal set; }

        /// <summary>获取构建索引。</summary>
        public int BuildIndex { get; internal set; }

        /// <summary>获取场景句柄。</summary>
        public SceneHandle Scene { get; internal set; }

        /// <summary>获取当前状态。</summary>
        public SceneState State { get; private set; }

        /// <summary>获取当前进度。</summary>
        public float Progress { get; private set; }

        /// <summary>获取是否已挂起。</summary>
        public bool IsSuspended { get; internal set; }

        /// <summary>获取是否为预加载 Handler。</summary>
        public bool IsPreloaded { get; internal set; }

        /// <summary>获取加载完成后是否需要激活；用于保存挂起预加载期间收到的一次性激活意图。</summary>
        internal bool ActivateWhenLoaded { get; set; }

        /// <summary>获取加载模式。</summary>
        public SceneLoadMode LoadMode { get; internal set; }

        /// <summary>获取场景附加数据。</summary>
        public ISceneData SceneData { get; internal set; }

        /// <summary>获取当前 Provider 返回的加载操作。</summary>
        public ISceneLoadOperation Operation { get; internal set; }

        /// <summary>获取该 Handler 使用的后端，Provider 切换后仍保持旧场景所有权。</summary>
        internal ISceneBackend Backend { get; set; }

        /// <summary>注册加载完成回调；已完成时立即回调。</summary>
        /// <param name="callback">加载完成回调。</param>
        public void AddLoadedCallback(Action<SceneHandler> callback)
        {
            if (callback == null)
            {
                return;
            }

            if (State == SceneState.Loaded)
            {
                callback(this);
                return;
            }

            mLoadCallbacks += callback;
        }

        /// <summary>以指定结果调用并清空等待中的加载回调。</summary>
        /// <param name="result">成功时为当前 Handler，失败时为空。</param>
        internal void InvokeLoadCallbacks(SceneHandler result)
        {
            Action<SceneHandler> callbacks = mLoadCallbacks;
            mLoadCallbacks = null;
            callbacks?.Invoke(result);
        }

        /// <summary>登记卸载完成回调；同一 Handler 的并发卸载请求共享一次后端卸载。</summary>
        /// <param name="callback">卸载真正完成后调用的回调。</param>
        internal void AddUnloadCallback(Action callback)
        {
            mUnloadCallbacks += callback;
        }

        /// <summary>取出并清空当前等待中的卸载回调，确保每个回调只执行一次。</summary>
        /// <returns>等待中的卸载回调组合；没有回调时返回空。</returns>
        internal Action TakeUnloadCallbacks()
        {
            Action callbacks = mUnloadCallbacks;
            mUnloadCallbacks = null;
            return callbacks;
        }

        /// <summary>更新进度，拒绝 NaN 并限制到 0 到 1，避免无效 Provider 值进入事件链。</summary>
        /// <param name="progress">Provider 报告的进度。</param>
        public void UpdateProgress(float progress)
        {
            if (float.IsNaN(progress))
            {
                Progress = 0f;
                return;
            }

            Progress = progress < 0f ? 0f : progress > 1f ? 1f : progress;
        }

        /// <summary>设置场景生命周期状态。</summary>
        /// <param name="state">目标状态。</param>
        public void SetState(SceneState state)
        {
            State = state;
        }

        /// <summary>重置 Handler 并进入加载状态。</summary>
        internal void Reset(
            string sceneName,
            int buildIndex,
            SceneLoadMode mode,
            ISceneData data,
            bool isPreload,
            ISceneBackend backend)
        {
            SceneName = sceneName;
            BuildIndex = buildIndex;
            Scene = default;
            State = SceneState.Loading;
            Progress = 0f;
            IsSuspended = false;
            IsPreloaded = isPreload;
            ActivateWhenLoaded = false;
            LoadMode = mode;
            SceneData = data;
            Operation = null;
            Backend = backend;
            mLoadCallbacks = null;
            mUnloadCallbacks = null;
        }

        /// <summary>释放操作引用并把 Handler 标记为已卸载。</summary>
        internal void MarkUnloaded()
        {
            Operation?.Recycle();
            Operation = null;
            Backend = null;
            IsSuspended = false;
            IsPreloaded = false;
            ActivateWhenLoaded = false;
            Progress = 0f;
            State = SceneState.Unloaded;
            mLoadCallbacks = null;
            mUnloadCallbacks = null;
        }
    }
}
