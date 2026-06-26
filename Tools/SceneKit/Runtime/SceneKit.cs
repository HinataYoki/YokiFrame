using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 提供跨引擎场景加载、预加载、激活、卸载和诊断入口。
    /// </summary>
    public static partial class SceneKit
    {
        private const int INVALID_BUILD_INDEX = -1;
        private const float DEFAULT_LOAD_SUSPEND_PROGRESS = 1f;
        private const float DEFAULT_PRELOAD_SUSPEND_PROGRESS = 0.9f;
        private const float COMPLETE_PROGRESS = 1f;
        private const string BUILD_INDEX_SCENE_PREFIX = "#";

        private static readonly Dictionary<string, SceneHandler> sSceneCache = new();
        private static readonly List<SceneHandler> sLoadedScenes = new();
        private static readonly ResKitSceneBackendAdapter sResKitBackendAdapter = new ResKitSceneBackendAdapter();
        private static ISceneBackend sBackend;
        private static SceneHandler sActiveSceneHandler;
        private static bool sIsTransitioning;

        /// <summary>
        /// 获取当前是否处于场景过渡流程中。
        /// </summary>
        public static bool IsTransitioning
        {
            get { return sIsTransitioning; }
        }

        /// <summary>
        /// 设置当前引擎场景后端。
        /// </summary>
        /// <param name="backend">场景后端。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="backend"/> 为空时抛出。</exception>
        public static void SetBackend(ISceneBackend backend)
        {
            sBackend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>
        /// 获取当前引擎场景后端。
        /// </summary>
        /// <returns>当前场景后端；未设置时返回空。</returns>
        public static ISceneBackend GetBackend()
        {
            return sBackend ?? (ResKit.GetSceneBackend() != null ? sResKitBackendAdapter : null);
        }

        /// <summary>
        /// 重置 SceneKit 的所有运行时状态。
        /// </summary>
        public static void Reset()
        {
            for (int i = 0; i < sLoadedScenes.Count; i++)
            {
                sLoadedScenes[i].MarkUnloaded();
            }

            sSceneCache.Clear();
            sLoadedScenes.Clear();
            sBackend = null;
            sActiveSceneHandler = null;
            sIsTransitioning = false;
        }

        /// <summary>
        /// 获取当前激活场景的处理器。
        /// </summary>
        /// <returns>当前激活场景处理器；没有激活场景时返回空。</returns>
        public static SceneHandler GetActiveSceneHandler() => sActiveSceneHandler;

        /// <summary>
        /// 获取当前激活场景的跨引擎句柄。
        /// </summary>
        /// <returns>当前激活场景句柄；没有激活场景时返回默认值。</returns>
        public static SceneHandle GetActiveScene()
        {
            if (sActiveSceneHandler != null)
                return sActiveSceneHandler.Scene;

            var backend = GetBackend();
            return backend != null ? backend.GetActiveScene() : default(SceneHandle);
        }

        /// <summary>
        /// 获取已登记的场景处理器列表。
        /// </summary>
        /// <returns>已加载或正在加载的场景处理器列表。</returns>
        public static IReadOnlyList<SceneHandler> GetLoadedScenes() => sLoadedScenes;

        /// <summary>
        /// 判断指定场景是否已加载且未处于卸载中。
        /// </summary>
        /// <param name="sceneName">场景名称。</param>
        /// <returns>场景已加载时返回 true。</returns>
        public static bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            SceneHandler handler;
            return sSceneCache.TryGetValue(sceneName, out handler) &&
                   handler.State != SceneState.Unloaded &&
                   handler.State != SceneState.Unloading;
        }

        /// <summary>
        /// 获取指定场景的处理器。
        /// </summary>
        /// <param name="sceneName">场景名称。</param>
        /// <returns>场景处理器；未登记时返回空。</returns>
        public static SceneHandler GetSceneHandler(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return null;

            SceneHandler handler;
            return sSceneCache.TryGetValue(sceneName, out handler) ? handler : null;
        }

        /// <summary>
        /// 获取当前激活场景上的指定类型数据。
        /// </summary>
        /// <typeparam name="T">场景数据类型。</typeparam>
        /// <returns>场景数据；不存在或类型不匹配时返回空。</returns>
        public static T GetSceneData<T>() where T : class, ISceneData
        {
            return sActiveSceneHandler != null ? sActiveSceneHandler.SceneData as T : null;
        }

        /// <summary>
        /// 获取指定场景上的指定类型数据。
        /// </summary>
        /// <typeparam name="T">场景数据类型。</typeparam>
        /// <param name="sceneName">场景名称。</param>
        /// <returns>场景数据；不存在或类型不匹配时返回空。</returns>
        public static T GetSceneData<T>(string sceneName) where T : class, ISceneData
        {
            var handler = GetSceneHandler(sceneName);
            return handler != null ? handler.SceneData as T : null;
        }

        /// <summary>
        /// 按场景名称异步加载场景。
        /// </summary>
        /// <param name="sceneName">场景名称。</param>
        /// <param name="mode">加载模式。</param>
        /// <param name="onComplete">加载完成回调。</param>
        /// <param name="onProgress">加载进度回调。</param>
        /// <param name="suspendAtProgress">挂起加载的进度阈值。</param>
        /// <param name="data">场景附加数据。</param>
        /// <returns>场景处理器；名称无效时返回空。</returns>
        public static SceneHandler LoadSceneAsync(
            string sceneName,
            SceneLoadMode mode = SceneLoadMode.Single,
            Action<SceneHandler> onComplete = null,
            Action<float> onProgress = null,
            float suspendAtProgress = DEFAULT_LOAD_SUSPEND_PROGRESS,
            ISceneData data = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                if (onComplete != null)
                    onComplete(null);
                return null;
            }

            var existing = GetSceneHandler(sceneName);
            if (existing != null && existing.State != SceneState.Unloaded)
            {
                if (existing.State == SceneState.Loaded)
                {
                    if (onComplete != null)
                        onComplete(existing);
                }
                else
                {
                    existing.AddLoadedCallback(onComplete);
                }

                return existing;
            }

            var backend = EnsureBackend();
            if (mode == SceneLoadMode.Single)
                ClearScenesForSingleMode(sceneName, backend);

            var handler = CreateHandler(sceneName, INVALID_BUILD_INDEX, mode, data, false);
            RegisterHandler(handler);
            SendSceneLoadStart(handler);

            var request = new SceneLoadRequest(sceneName, INVALID_BUILD_INDEX, mode, suspendAtProgress, data, false);
            handler.Operation = backend.LoadSceneAsync(
                request,
                result => OnSceneLoaded(handler, result, onComplete),
                progress => OnSceneProgress(handler, progress, onProgress),
                () => OnSceneSuspended(handler));
            return handler;
        }

        /// <summary>
        /// 按构建索引异步加载场景。
        /// </summary>
        /// <param name="buildIndex">场景构建索引。</param>
        /// <param name="mode">加载模式。</param>
        /// <param name="onComplete">加载完成回调。</param>
        /// <param name="onProgress">加载进度回调。</param>
        /// <param name="suspendAtProgress">挂起加载的进度阈值。</param>
        /// <param name="data">场景附加数据。</param>
        /// <returns>场景处理器；索引无效时返回空。</returns>
        public static SceneHandler LoadSceneAsync(
            int buildIndex,
            SceneLoadMode mode = SceneLoadMode.Single,
            Action<SceneHandler> onComplete = null,
            Action<float> onProgress = null,
            float suspendAtProgress = DEFAULT_LOAD_SUSPEND_PROGRESS,
            ISceneData data = null)
        {
            if (buildIndex < 0)
            {
                if (onComplete != null)
                    onComplete(null);
                return null;
            }

            var sceneName = BUILD_INDEX_SCENE_PREFIX + buildIndex;
            var existing = GetSceneHandler(sceneName);
            if (existing != null && existing.State != SceneState.Unloaded)
            {
                if (existing.State == SceneState.Loaded)
                {
                    if (onComplete != null)
                        onComplete(existing);
                }
                else
                {
                    existing.AddLoadedCallback(onComplete);
                }

                return existing;
            }

            var backend = EnsureBackend();
            if (mode == SceneLoadMode.Single)
                ClearScenesForSingleMode(sceneName, backend);

            var handler = CreateHandler(sceneName, buildIndex, mode, data, false);
            RegisterHandler(handler);
            SendSceneLoadStart(handler);

            var request = new SceneLoadRequest(sceneName, buildIndex, mode, suspendAtProgress, data, false);
            handler.Operation = backend.LoadSceneAsync(
                request,
                result => OnSceneLoaded(handler, result, onComplete),
                progress => OnSceneProgress(handler, progress, onProgress),
                () => OnSceneSuspended(handler));
            return handler;
        }

        /// <summary>
        /// 预加载场景并在指定进度附近挂起，等待后续显式激活。
        /// </summary>
        /// <param name="sceneName">场景名称。</param>
        /// <param name="onComplete">加载完成回调。</param>
        /// <param name="onProgress">加载进度回调。</param>
        /// <param name="suspendAtProgress">挂起加载的进度阈值。</param>
        /// <param name="onSuspended">加载挂起回调。</param>
        /// <returns>场景处理器；名称无效时返回空。</returns>
        public static SceneHandler PreloadSceneAsync(
            string sceneName,
            Action<SceneHandler> onComplete = null,
            Action<float> onProgress = null,
            float suspendAtProgress = DEFAULT_PRELOAD_SUSPEND_PROGRESS,
            Action<SceneHandler> onSuspended = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                if (onComplete != null)
                    onComplete(null);
                return null;
            }

            var existing = GetSceneHandler(sceneName);
            if (existing != null && existing.State != SceneState.Unloaded)
            {
                existing.AddLoadedCallback(onComplete);
                return existing;
            }

            var handler = CreateHandler(sceneName, INVALID_BUILD_INDEX, SceneLoadMode.Additive, null, true);
            RegisterHandler(handler);
            SendSceneLoadStart(handler);

            var request = new SceneLoadRequest(sceneName, INVALID_BUILD_INDEX, SceneLoadMode.Additive, suspendAtProgress, null, true);
            handler.Operation = EnsureBackend().LoadSceneAsync(
                request,
                result => OnSceneLoaded(handler, result, onComplete),
                progress => OnSceneProgress(handler, progress, onProgress),
                () =>
                {
                    OnSceneSuspended(handler);
                    if (onSuspended != null)
                        onSuspended(handler);
                });
            return handler;
        }

        /// <summary>
        /// 激活已预加载的场景；若加载被挂起，则先恢复加载。
        /// </summary>
        /// <param name="handler">预加载场景处理器。</param>
        public static void ActivatePreloadedScene(SceneHandler handler)
        {
            if (handler == null || !handler.IsPreloaded)
                return;

            if (handler.IsSuspended && handler.Operation != null)
            {
                handler.Operation.ResumeLoad();
                handler.IsSuspended = false;
                return;
            }

            SetActiveScene(handler);
            handler.IsPreloaded = false;
        }

        /// <summary>
        /// 挂起正在加载的场景。
        /// </summary>
        /// <param name="handler">场景处理器。</param>
        public static void SuspendLoad(SceneHandler handler)
        {
            if (handler == null || handler.Operation == null || handler.State != SceneState.Loading)
                return;

            handler.Operation.SuspendLoad();
            handler.IsSuspended = true;
        }

        /// <summary>
        /// 恢复已挂起的场景加载。
        /// </summary>
        /// <param name="handler">场景处理器。</param>
        public static void ResumeLoad(SceneHandler handler)
        {
            if (handler == null || handler.Operation == null || !handler.IsSuspended)
                return;

            handler.Operation.ResumeLoad();
            handler.IsSuspended = false;
        }

        /// <summary>
        /// 按场景名称异步卸载场景。
        /// </summary>
        /// <param name="sceneName">场景名称。</param>
        /// <param name="onComplete">卸载完成回调。</param>
        public static void UnloadSceneAsync(string sceneName, Action onComplete = null)
        {
            var handler = GetSceneHandler(sceneName);
            if (handler == null)
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            UnloadSceneAsync(handler, onComplete);
        }

        /// <summary>
        /// 按场景处理器异步卸载场景。
        /// </summary>
        /// <param name="handler">场景处理器。</param>
        /// <param name="onComplete">卸载完成回调。</param>
        public static void UnloadSceneAsync(SceneHandler handler, Action onComplete = null)
        {
            if (handler == null)
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            if (sLoadedScenes.Count <= 1)
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            if (handler.State == SceneState.Unloading || handler.State == SceneState.Unloaded)
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            if (handler.State == SceneState.Loading)
            {
                handler.SetState(SceneState.Unloading);
                handler.AddLoadedCallback(_ => EnsureBackend().UnloadSceneAsync(handler.Scene, () => OnSceneUnloaded(handler, onComplete)));
                return;
            }

            handler.SetState(SceneState.Unloading);
            EnsureBackend().UnloadSceneAsync(handler.Scene, () => OnSceneUnloaded(handler, onComplete));
        }

        /// <summary>
        /// 请求后端卸载未使用资源。
        /// </summary>
        /// <param name="onComplete">完成回调。</param>
        public static void UnloadUnusedAssets(Action onComplete = null)
        {
            EnsureBackend().UnloadUnusedAssets(onComplete);
        }

        /// <summary>
        /// 清理所有已登记场景。
        /// </summary>
        /// <param name="preserveActive">是否保留当前激活场景。</param>
        /// <param name="onComplete">清理完成回调。</param>
        public static void ClearAllScenes(bool preserveActive = true, Action onComplete = null)
        {
            if (sLoadedScenes.Count == 0)
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            var scenesToUnload = new List<SceneHandler>(sLoadedScenes.Count);
            for (int i = 0; i < sLoadedScenes.Count; i++)
            {
                var handler = sLoadedScenes[i];
                if (preserveActive && handler == sActiveSceneHandler)
                    continue;

                scenesToUnload.Add(handler);
            }

            if (scenesToUnload.Count == 0)
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            var unloadedCount = 0;
            var totalCount = scenesToUnload.Count;
            for (int i = 0; i < scenesToUnload.Count; i++)
            {
                UnloadSceneAsync(scenesToUnload[i], () =>
                {
                    unloadedCount++;
                    if (unloadedCount >= totalCount && onComplete != null)
                        onComplete();
                });
            }
        }

        private static ISceneBackend EnsureBackend()
        {
            if (sBackend == null)
            {
                if (ResKit.GetSceneBackend() == null)
                    throw new InvalidOperationException("SceneKit backend is not configured. Call SceneKit.SetBackend or ResKit.SetSceneBackend from an engine adapter first.");

                return sResKitBackendAdapter;
            }

            return sBackend;
        }

        private static SceneHandler CreateHandler(string sceneName, int buildIndex, SceneLoadMode mode, ISceneData data, bool isPreload)
        {
            var handler = new SceneHandler();
            handler.Reset(sceneName, buildIndex, mode, data, isPreload);
            return handler;
        }

        private static void RegisterHandler(SceneHandler handler)
        {
            sSceneCache[handler.SceneName] = handler;
            if (!sLoadedScenes.Contains(handler))
                sLoadedScenes.Add(handler);
        }

        private static void UnregisterHandler(SceneHandler handler)
        {
            if (handler == null)
                return;

            if (!string.IsNullOrEmpty(handler.SceneName))
                sSceneCache.Remove(handler.SceneName);

            sLoadedScenes.Remove(handler);
            if (sActiveSceneHandler == handler)
                sActiveSceneHandler = null;
        }

        private static void ClearScenesForSingleMode(string newSceneName, ISceneBackend backend)
        {
            if (sLoadedScenes.Count == 0)
                return;

            var scenesToUnload = new List<SceneHandler>(sLoadedScenes.Count);
            for (int i = sLoadedScenes.Count - 1; i >= 0; i--)
            {
                var handler = sLoadedScenes[i];
                if (handler.SceneName == newSceneName)
                    continue;

                scenesToUnload.Add(handler);
            }

            for (int i = 0; i < scenesToUnload.Count; i++)
            {
                var handler = scenesToUnload[i];
                if (backend != null)
                    backend.UnloadSceneAsync(handler.Scene, null);
                UnregisterHandler(handler);
                handler.MarkUnloaded();
            }
        }

        private static void OnSceneProgress(SceneHandler handler, float progress, Action<float> onProgress)
        {
            handler.UpdateProgress(progress);
            handler.IsSuspended = handler.Operation != null && handler.Operation.IsSuspended;
            EventKit.Type.Send(new SceneLoadProgressEvent
            {
                SceneName = handler.SceneName,
                Progress = handler.Progress
            });

            if (onProgress != null)
                onProgress(handler.Progress);
        }

        private static void OnSceneSuspended(SceneHandler handler)
        {
            handler.IsSuspended = true;
            if (handler.Operation != null)
                handler.UpdateProgress(handler.Operation.Progress);
        }

        private static void OnSceneLoaded(SceneHandler handler, SceneLoadResult result, Action<SceneHandler> onComplete)
        {
            handler.Scene = result.Scene;
            if (handler.State == SceneState.Unloading)
            {
                handler.UpdateProgress(COMPLETE_PROGRESS);
                handler.IsSuspended = false;
                if (onComplete != null)
                    onComplete(handler);
                handler.InvokeLoadedCallbacks();
                return;
            }

            handler.SetState(SceneState.Loaded);
            handler.UpdateProgress(COMPLETE_PROGRESS);
            handler.IsSuspended = false;

            if (handler.LoadMode == SceneLoadMode.Single || sActiveSceneHandler == null)
                SetActiveScene(handler);

            EventKit.Type.Send(new SceneLoadCompleteEvent
            {
                SceneName = handler.SceneName,
                Scene = handler.Scene,
                Handler = handler
            });

            if (onComplete != null)
                onComplete(handler);
            handler.InvokeLoadedCallbacks();
        }

        private static void OnSceneUnloaded(SceneHandler handler, Action onComplete)
        {
            var sceneName = handler.SceneName;
            UnregisterHandler(handler);
            handler.MarkUnloaded();
            EventKit.Type.Send(new SceneUnloadEvent { SceneName = sceneName });

            if (sActiveSceneHandler == null)
                PromoteFirstLoadedScene();

            if (onComplete != null)
                onComplete();
        }

        private static void SetActiveScene(SceneHandler handler)
        {
            var previousScene = sActiveSceneHandler != null ? sActiveSceneHandler.Scene : default(SceneHandle);
            sActiveSceneHandler = handler;
            if (handler != null)
            {
                EnsureBackend().SetActiveScene(handler.Scene);
                EventKit.Type.Send(new ActiveSceneChangedEvent
                {
                    PreviousScene = previousScene,
                    NewScene = handler.Scene
                });
            }
        }

        private static void SendSceneLoadStart(SceneHandler handler)
        {
            EventKit.Type.Send(new SceneLoadStartEvent
            {
                SceneName = handler.SceneName,
                Mode = handler.LoadMode
            });
        }

        private static void PromoteFirstLoadedScene()
        {
            for (int i = 0; i < sLoadedScenes.Count; i++)
            {
                var candidate = sLoadedScenes[i];
                if (candidate.State == SceneState.Loaded)
                {
                    SetActiveScene(candidate);
                    return;
                }
            }
        }
    }

}
