using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// 场景句柄，管理单个场景的引用和状态信息
    /// </summary>
    public class SceneHandler : IPoolable
    {
        #region 基本信息

        /// <summary>
        /// 场景名称
        /// </summary>
        public string SceneName { get; set; }

        /// <summary>
        /// 场景在 Build Settings 中的索引
        /// </summary>
        public int BuildIndex { get; set; } = -1;

        /// <summary>
        /// Unity 场景引用
        /// </summary>
        public Scene Scene { get; set; }

        #endregion

        #region 状态

        /// <summary>
        /// 场景当前状态
        /// </summary>
        public SceneState State { get; private set; }

        /// <summary>
        /// 加载进度（0-1）
        /// </summary>
        public float Progress { get; private set; }

        /// <summary>
        /// 是否处于暂停加载状态
        /// </summary>
        public bool IsSuspended { get; set; }

        /// <summary>
        /// 是否为预加载状态（已加载但未激活）
        /// </summary>
        public bool IsPreloaded { get; set; }

        #endregion

        #region 配置

        /// <summary>
        /// 场景加载模式
        /// </summary>
        public SceneLoadMode LoadMode { get; set; }

        /// <summary>
        /// 场景关联数据
        /// </summary>
        public ISceneData SceneData { get; set; }

        #endregion

        #region 内部引用

        /// <summary>
        /// 场景加载器引用
        /// </summary>
        public ISceneLoader Loader { get; set; }

        /// <summary>
        /// 异步操作引用
        /// </summary>
        public AsyncOperation AsyncOp { get; set; }

        /// <summary>
        /// 等待加载完成的回调链
        /// </summary>
        private Action<SceneHandler> mOnLoaded;

        #endregion

        #region IPoolable

        /// <summary>
        /// 是否已被回收到对象池
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 回收时清理状态
        /// </summary>
        public void OnRecycled()
        {
            SceneName = null;
            BuildIndex = -1;
            Scene = default;
            State = SceneState.None;
            Progress = 0f;
            IsSuspended = false;
            IsPreloaded = false;
            LoadMode = SceneLoadMode.Single;
            SceneData = null;
            Loader?.Recycle();
            Loader = null;
            AsyncOp = null;
            mOnLoaded = null;
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 更新加载进度
        /// </summary>
        public void UpdateProgress(float progress)
        {
            // 确保进度值在有效范围内
            Progress = Mathf.Clamp01(progress);
        }

        /// <summary>
        /// 设置状态
        /// </summary>
        public void SetState(SceneState state)
        {
            State = state;
        }

        /// <summary>
        /// 添加加载完成回调。若已完成则立即回调，否则排队等待。
        /// </summary>
        public void AddLoadedCallback(Action<SceneHandler> callback)
        {
            if (callback is null) return;

            if (State == SceneState.Loaded)
            {
                callback.Invoke(this);
                return;
            }

            mOnLoaded += callback;
        }

        /// <summary>
        /// 加载完成后，通知所有等待者
        /// </summary>
        public void InvokeLoadedCallbacks()
        {
            var callbacks = mOnLoaded;
            mOnLoaded = null;
            callbacks?.Invoke(this);
        }

        #endregion
    }
}
