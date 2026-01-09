using System;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// 场景加载器接口，定义场景加载的抽象行为
    /// </summary>
    public interface ISceneLoader
    {
        /// <summary>
        /// 异步加载场景（通过场景名）
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="mode">加载模式</param>
        /// <param name="onComplete">加载完成回调</param>
        /// <param name="onProgress">进度回调</param>
        /// <param name="suspendAtProgress">暂停加载的进度阈值（0-1），1表示不暂停</param>
        void LoadAsync(string sceneName, SceneLoadMode mode,
            Action<Scene> onComplete,
            Action<float> onProgress = null,
            float suspendAtProgress = 1f);

        /// <summary>
        /// 异步加载场景（通过 BuildIndex）
        /// </summary>
        /// <param name="buildIndex">场景在 Build Settings 中的索引</param>
        /// <param name="mode">加载模式</param>
        /// <param name="onComplete">加载完成回调</param>
        /// <param name="onProgress">进度回调</param>
        /// <param name="suspendAtProgress">暂停加载的进度阈值（0-1），1表示不暂停</param>
        void LoadAsync(int buildIndex, SceneLoadMode mode,
            Action<Scene> onComplete,
            Action<float> onProgress = null,
            float suspendAtProgress = 1f);

        /// <summary>
        /// 异步卸载场景
        /// </summary>
        /// <param name="scene">要卸载的场景</param>
        /// <param name="onComplete">卸载完成回调</param>
        void UnloadAsync(Scene scene, Action onComplete);

        /// <summary>
        /// 暂停加载
        /// </summary>
        void SuspendLoad();

        /// <summary>
        /// 恢复加载
        /// </summary>
        void ResumeLoad();

        /// <summary>
        /// 当前是否处于暂停状态
        /// </summary>
        bool IsSuspended { get; }

        /// <summary>
        /// 当前加载进度（0-1）
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// 回收加载器到对象池
        /// </summary>
        void Recycle();
    }
}
