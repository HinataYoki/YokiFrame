using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>提供跨引擎场景加载、预加载、激活、卸载和场景数据入口。</summary>
    public static partial class SceneKit
    {
        private const int INVALID_BUILD_INDEX = -1;
        private const float DEFAULT_LOAD_SUSPEND_PROGRESS = 1f;
        private const float DEFAULT_PRELOAD_SUSPEND_PROGRESS = 0.9f;
        private const float COMPLETE_PROGRESS = 1f;
        private const string BUILD_INDEX_SCENE_PREFIX = "#";

        private static readonly Dictionary<string, SceneHandler> sSceneCache = new(StringComparer.Ordinal);
        private static readonly List<SceneHandler> sLoadedScenes = new();
        private static ISceneBackend sExplicitBackend;
        private static ISceneBackend sDefaultBackend;
        private static IResSceneProvider sDefaultProvider;
        private static SceneHandler sActiveSceneHandler;

        /// <summary>设置显式 SceneKit 后端；显式后端优先于当前 ResKit Provider。</summary>
        /// <param name="backend">要使用的场景后端。</param>
        public static void SetBackend(ISceneBackend backend)
        {
            sExplicitBackend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>清除显式后端，使 SceneKit 恢复跟随 ResKit 当前 Provider。</summary>
        public static void ClearBackend()
        {
            sExplicitBackend = null;
        }

        /// <summary>获取当前显式或 ResKit 默认后端；未配置时返回空。</summary>
        /// <returns>当前场景后端。</returns>
        public static ISceneBackend GetBackend()
        {
            if (sExplicitBackend != null)
            {
                return sExplicitBackend;
            }

            IResSceneProvider provider = ResKit.TryGetSceneProvider();
            if (provider == null)
            {
                return null;
            }

            return ResolveDefaultBackend(provider);
        }

        /// <summary>获取当前是否存在正在加载或卸载的场景。</summary>
        public static bool IsTransitioning
        {
            get
            {
                for (var index = 0; index < sLoadedScenes.Count; index++)
                {
                    SceneState state = sLoadedScenes[index].State;
                    if (state == SceneState.Loading || state == SceneState.Unloading)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>重置 SceneKit 的逻辑状态；收集待处理卸载回调并在状态清空后统一触发，不会尝试调用已失效 Provider 的卸载 API。</summary>
        public static void Reset()
        {
            Action pending = null;
            for (var index = 0; index < sLoadedScenes.Count; index++)
            {
                SceneHandler handler = sLoadedScenes[index];
                pending += handler.TakeUnloadCallbacks();
                handler.MarkUnloaded();
            }

            sSceneCache.Clear();
            sLoadedScenes.Clear();
            sExplicitBackend = null;
            sDefaultBackend = null;
            sDefaultProvider = null;
            sActiveSceneHandler = null;
            pending?.Invoke();
        }

        /// <summary>获取当前激活场景 Handler。</summary>
        public static SceneHandler GetActiveSceneHandler()
        {
            return sActiveSceneHandler;
        }

        /// <summary>获取当前激活场景句柄。</summary>
        public static SceneHandle GetActiveScene()
        {
            if (sActiveSceneHandler != null)
            {
                return sActiveSceneHandler.Scene;
            }

            ISceneBackend backend = GetBackend();
            return backend == null ? default : backend.GetActiveScene();
        }

        /// <summary>获取当前已登记的场景 Handler 列表。</summary>
        public static IReadOnlyList<SceneHandler> GetLoadedScenes()
        {
            return sLoadedScenes;
        }

        /// <summary>判断指定场景是否处于已加载或加载中状态。</summary>
        /// <param name="sceneName">场景名称。</param>
        public static bool IsSceneLoaded(string sceneName)
        {
            SceneHandler handler = GetSceneHandler(sceneName);
            return handler != null
                && (handler.State == SceneState.Loading || handler.State == SceneState.Loaded);
        }

        /// <summary>按缓存键获取场景 Handler。</summary>
        /// <param name="sceneName">场景名称或 BuildIndex 缓存键。</param>
        public static SceneHandler GetSceneHandler(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return null;
            }

            return sSceneCache.TryGetValue(sceneName, out SceneHandler handler) ? handler : null;
        }

        /// <summary>获取当前激活场景上的指定类型业务数据。</summary>
        /// <typeparam name="T">场景数据类型。</typeparam>
        public static T GetSceneData<T>() where T : class, ISceneData
        {
            return sActiveSceneHandler == null ? null : sActiveSceneHandler.SceneData as T;
        }

        /// <summary>获取指定场景上的指定类型业务数据。</summary>
        /// <typeparam name="T">场景数据类型。</typeparam>
        /// <param name="sceneName">场景名称。</param>
        public static T GetSceneData<T>(string sceneName) where T : class, ISceneData
        {
            SceneHandler handler = GetSceneHandler(sceneName);
            return handler == null ? null : handler.SceneData as T;
        }

        /// <summary>按名称异步加载场景。</summary>
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
                onComplete?.Invoke(null);
                return null;
            }

            return StartLoad(new SceneLoadRequest(
                sceneName, INVALID_BUILD_INDEX, mode, suspendAtProgress, data, false),
                onComplete, onProgress, null);
        }

        /// <summary>按构建索引异步加载场景。</summary>
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
                onComplete?.Invoke(null);
                return null;
            }

            return StartLoad(new SceneLoadRequest(
                BUILD_INDEX_SCENE_PREFIX + buildIndex,
                buildIndex,
                mode,
                suspendAtProgress,
                data,
                false), onComplete, onProgress, null);
        }

        /// <summary>预加载场景并在 Provider 支持的阈值处挂起。</summary>
        public static SceneHandler PreloadSceneAsync(
            string sceneName,
            Action<SceneHandler> onComplete = null,
            Action<float> onProgress = null,
            float suspendAtProgress = DEFAULT_PRELOAD_SUSPEND_PROGRESS,
            Action<SceneHandler> onSuspended = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                onComplete?.Invoke(null);
                return null;
            }

            return StartLoad(
                new SceneLoadRequest(sceneName, INVALID_BUILD_INDEX, SceneLoadMode.Additive,
                    suspendAtProgress, null, true),
                onComplete,
                onProgress,
                onSuspended);
        }

        /// <summary>激活预加载场景；已挂起时先恢复 Provider 操作。</summary>
        public static void ActivatePreloadedScene(SceneHandler handler)
        {
            if (handler == null
                || !handler.IsPreloaded
                || (handler.State != SceneState.Loading && handler.State != SceneState.Loaded))
            {
                return;
            }

            if (handler.State == SceneState.Loading)
            {
                if (!handler.IsSuspended)
                {
                    return;
                }

                // 恢复操作前先保存意图，避免异步完成后因已有激活场景而遗漏切换。
                handler.ActivateWhenLoaded = true;
                handler.IsPreloaded = false;
                ResumeSuspendedLoad(handler);
                return;
            }

            SetActiveScene(handler);
            handler.IsPreloaded = false;
            handler.ActivateWhenLoaded = false;
        }

        /// <summary>挂起指定 Handler 的加载操作。</summary>
        public static void SuspendLoad(SceneHandler handler)
        {
            if (handler == null || handler.Operation == null || handler.State != SceneState.Loading)
            {
                return;
            }

            handler.Operation.SuspendLoad();
            handler.IsSuspended = handler.Operation.IsSuspended;
        }

        /// <summary>恢复指定 Handler 的加载操作。</summary>
        public static void ResumeLoad(SceneHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            ResumeSuspendedLoad(handler);
        }

        /// <summary>按场景名称异步卸载场景。</summary>
        public static void UnloadSceneAsync(string sceneName, Action onComplete = null)
        {
            UnloadSceneAsync(GetSceneHandler(sceneName), onComplete);
        }

        /// <summary>按 Handler 异步卸载场景。</summary>
        public static void UnloadSceneAsync(SceneHandler handler, Action onComplete = null)
        {
            if (handler == null || handler.State == SceneState.Unloaded)
            {
                onComplete?.Invoke();
                return;
            }

            handler.AddUnloadCallback(onComplete);
            if (handler.State == SceneState.Unloading)
            {
                return;
            }

            if (handler.State == SceneState.Loading)
            {
                handler.SetState(SceneState.Unloading);
                ResumeSuspendedLoad(handler);
                return;
            }

            if (handler.State == SceneState.Failed || !handler.Scene.IsValid)
            {
                OnSceneUnloaded(handler);
                return;
            }

            handler.SetState(SceneState.Unloading);
            UnloadLoadedHandler(handler);
        }

        /// <summary>请求当前场景后端卸载未使用资源。</summary>
        public static void UnloadUnusedAssets(Action onComplete = null)
        {
            EnsureBackend().UnloadUnusedAssets(onComplete);
        }

        /// <summary>清理全部已登记场景，可选择保留当前激活场景。</summary>
        public static void ClearAllScenes(bool preserveActive = true, Action onComplete = null)
        {
            if (sLoadedScenes.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var targets = new List<SceneHandler>(sLoadedScenes.Count);
            for (var index = 0; index < sLoadedScenes.Count; index++)
            {
                SceneHandler handler = sLoadedScenes[index];
                if (!preserveActive || !ReferenceEquals(handler, sActiveSceneHandler))
                {
                    targets.Add(handler);
                }
            }

            if (targets.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var completed = 0;
            for (var index = 0; index < targets.Count; index++)
            {
                UnloadSceneAsync(targets[index], () =>
                {
                    completed++;
                    if (completed == targets.Count)
                    {
                        onComplete?.Invoke();
                    }
                });
            }
        }

    }
}
