#if YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// SceneKit UniTask 扩展方法
    /// </summary>
    public static class SceneKitUniTask
    {
        /// <summary>
        /// 异步加载场景（UniTask 版本）
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="mode">加载模式</param>
        /// <param name="progress">进度报告</param>
        /// <param name="suspendAtProgress">暂停加载的进度阈值</param>
        /// <param name="data">场景数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static async UniTask<SceneHandler> LoadSceneUniTaskAsync(
            string sceneName,
            SceneLoadMode mode = SceneLoadMode.Single,
            IProgress<float> progress = null,
            float suspendAtProgress = 1f,
            ISceneData data = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new UniTaskCompletionSource<SceneHandler>();
            
            cancellationToken.Register(() => tcs.TrySetCanceled());

            SceneKit.LoadSceneAsync(sceneName, mode,
                handler => tcs.TrySetResult(handler),
                p => progress?.Report(p),
                suspendAtProgress,
                data);

            return await tcs.Task;
        }

        /// <summary>
        /// 异步加载场景（通过 BuildIndex，UniTask 版本）
        /// </summary>
        public static async UniTask<SceneHandler> LoadSceneUniTaskAsync(
            int buildIndex,
            SceneLoadMode mode = SceneLoadMode.Single,
            IProgress<float> progress = null,
            float suspendAtProgress = 1f,
            ISceneData data = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new UniTaskCompletionSource<SceneHandler>();
            
            cancellationToken.Register(() => tcs.TrySetCanceled());

            SceneKit.LoadSceneAsync(buildIndex, mode,
                handler => tcs.TrySetResult(handler),
                p => progress?.Report(p),
                suspendAtProgress,
                data);

            return await tcs.Task;
        }

        /// <summary>
        /// 异步卸载场景（UniTask 版本）
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static async UniTask UnloadSceneUniTaskAsync(
            string sceneName,
            CancellationToken cancellationToken = default)
        {
            var tcs = new UniTaskCompletionSource();
            
            cancellationToken.Register(() => tcs.TrySetCanceled());

            SceneKit.UnloadSceneAsync(sceneName, () => tcs.TrySetResult());

            await tcs.Task;
        }

        /// <summary>
        /// 异步卸载场景（通过句柄，UniTask 版本）
        /// </summary>
        public static async UniTask UnloadSceneUniTaskAsync(
            SceneHandler handler,
            CancellationToken cancellationToken = default)
        {
            var tcs = new UniTaskCompletionSource();
            
            cancellationToken.Register(() => tcs.TrySetCanceled());

            SceneKit.UnloadSceneAsync(handler, () => tcs.TrySetResult());

            await tcs.Task;
        }

        /// <summary>
        /// 异步切换场景（UniTask 版本）
        /// </summary>
        /// <param name="sceneName">目标场景名称</param>
        /// <param name="transition">过渡效果</param>
        /// <param name="data">场景数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static async UniTask<SceneHandler> SwitchSceneUniTaskAsync(
            string sceneName,
            ISceneTransition transition = null,
            ISceneData data = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new UniTaskCompletionSource<SceneHandler>();
            
            cancellationToken.Register(() => tcs.TrySetCanceled());

            SceneKit.SwitchSceneAsync(sceneName, transition, data,
                handler => tcs.TrySetResult(handler));

            return await tcs.Task;
        }

        /// <summary>
        /// 异步预加载场景（UniTask 版本）
        /// </summary>
        public static async UniTask<SceneHandler> PreloadSceneUniTaskAsync(
            string sceneName,
            IProgress<float> progress = null,
            float suspendAtProgress = 0.9f,
            CancellationToken cancellationToken = default)
        {
            var tcs = new UniTaskCompletionSource<SceneHandler>();
            
            cancellationToken.Register(() => tcs.TrySetCanceled());

            SceneKit.PreloadSceneAsync(sceneName,
                handler => tcs.TrySetResult(handler),
                p => progress?.Report(p),
                suspendAtProgress);

            return await tcs.Task;
        }

        /// <summary>
        /// 异步清理所有场景（UniTask 版本）
        /// </summary>
        public static async UniTask ClearAllScenesUniTaskAsync(
            bool preserveActive = true,
            CancellationToken cancellationToken = default)
        {
            var tcs = new UniTaskCompletionSource();
            
            cancellationToken.Register(() => tcs.TrySetCanceled());

            SceneKit.ClearAllScenes(preserveActive, () => tcs.TrySetResult());

            await tcs.Task;
        }
    }
}
#endif
