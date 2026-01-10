using System;
using System.Threading;
using UnityEngine.SceneManagement;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 场景资源加载器接口
    /// </summary>
    public interface ISceneResLoader
    {
        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="scenePath">场景路径或名称</param>
        /// <param name="isAdditive">是否为叠加模式</param>
        /// <param name="suspendLoad">是否暂停加载（用于预加载）</param>
        /// <param name="onComplete">加载完成回调</param>
        /// <param name="onProgress">进度回调</param>
        void LoadAsync(string scenePath, bool isAdditive, bool suspendLoad,
            Action<Scene> onComplete, Action<float> onProgress = null);

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
        /// 卸载并回收加载器
        /// </summary>
        void UnloadAndRecycle();
    }

    /// <summary>
    /// 场景资源加载池接口
    /// </summary>
    public interface ISceneResLoaderPool
    {
        /// <summary>
        /// 分配加载器
        /// </summary>
        ISceneResLoader Allocate();

        /// <summary>
        /// 回收加载器
        /// </summary>
        void Recycle(ISceneResLoader loader);
    }

#if YOKIFRAME_UNITASK_SUPPORT
    /// <summary>
    /// 支持 UniTask 的场景资源加载器接口
    /// </summary>
    public interface ISceneResLoaderUniTask : ISceneResLoader
    {
        /// <summary>
        /// [UniTask] 异步加载场景
        /// </summary>
        UniTask<Scene> LoadUniTaskAsync(string scenePath, bool isAdditive, bool suspendLoad,
            IProgress<float> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// [UniTask] 异步卸载场景
        /// </summary>
        UniTask UnloadUniTaskAsync(Scene scene, CancellationToken cancellationToken = default);
    }
#endif
}
