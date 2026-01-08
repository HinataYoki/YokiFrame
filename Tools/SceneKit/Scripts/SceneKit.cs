using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// 场景管理工具 - 提供统一的场景加载、切换、卸载等 API
    /// </summary>
    public static class SceneKit
    {
        #region 私有字段

        private static ISceneLoaderPool sLoaderPool = new ResKitSceneLoaderPool();
        private static readonly Dictionary<string, SceneHandler> sSceneCache = new(8);
        private static readonly List<SceneHandler> sLoadedScenesList = new(4);
        private static SceneHandler sActiveSceneHandler;
        private static bool sIsTransitioning;

        #endregion

        #region 加载器池配置

        /// <summary>
        /// 设置自定义加载器池（用于 YooAsset 等扩展）
        /// </summary>
        /// <param name="pool">加载器池实例</param>
        public static void SetLoaderPool(ISceneLoaderPool pool)
        {
            if (pool == null)
            {
                KitLogger.Warning("[SceneKit] 加载器池不能为 null，保持当前配置");
                return;
            }
            sLoaderPool = pool;
            KitLogger.Log($"[SceneKit] 加载器池已切换为: {pool.GetType().Name}");
        }

        /// <summary>
        /// 获取当前加载器池
        /// </summary>
        public static ISceneLoaderPool GetLoaderPool() => sLoaderPool;

        #endregion

        #region 场景查询

        /// <summary>
        /// 获取当前活动场景
        /// </summary>
        public static Scene GetActiveScene() => SceneManager.GetActiveScene();

        /// <summary>
        /// 获取当前活动场景的句柄
        /// </summary>
        public static SceneHandler GetActiveSceneHandler() => sActiveSceneHandler;

        /// <summary>
        /// 获取所有已加载场景的句柄列表
        /// </summary>
        public static IReadOnlyList<SceneHandler> GetLoadedScenes() => sLoadedScenesList;

        /// <summary>
        /// 检查指定场景是否已加载
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        public static bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return sSceneCache.ContainsKey(sceneName);
        }

        /// <summary>
        /// 获取指定场景的句柄
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        public static SceneHandler GetSceneHandler(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            return sSceneCache.TryGetValue(sceneName, out var handler) ? handler : null;
        }

        /// <summary>
        /// 是否正在进行场景切换过渡
        /// </summary>
        public static bool IsTransitioning => sIsTransitioning;

        #endregion

        #region 场景数据

        /// <summary>
        /// 获取当前活动场景的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        public static T GetSceneData<T>() where T : class, ISceneData
        {
            return sActiveSceneHandler?.SceneData as T;
        }

        /// <summary>
        /// 获取指定场景的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="sceneName">场景名称</param>
        public static T GetSceneData<T>(string sceneName) where T : class, ISceneData
        {
            var handler = GetSceneHandler(sceneName);
            return handler?.SceneData as T;
        }

        #endregion

        #region 同步加载

        /// <summary>
        /// 同步加载场景（仅用于编辑器或特殊场景）
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="mode">加载模式</param>
        public static Scene LoadScene(string sceneName, SceneLoadMode mode = SceneLoadMode.Single)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                KitLogger.Error("[SceneKit] 场景名称不能为空");
                return default;
            }

            var loadMode = mode == SceneLoadMode.Single 
                ? LoadSceneMode.Single 
                : LoadSceneMode.Additive;

            SceneManager.LoadScene(sceneName, loadMode);
            return SceneManager.GetSceneByName(sceneName);
        }

        /// <summary>
        /// 同步加载场景（通过 BuildIndex）
        /// </summary>
        /// <param name="buildIndex">场景在 Build Settings 中的索引</param>
        /// <param name="mode">加载模式</param>
        public static Scene LoadScene(int buildIndex, SceneLoadMode mode = SceneLoadMode.Single)
        {
            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                KitLogger.Error($"[SceneKit] 无效的场景索引: {buildIndex}");
                return default;
            }

            var loadMode = mode == SceneLoadMode.Single 
                ? LoadSceneMode.Single 
                : LoadSceneMode.Additive;

            SceneManager.LoadScene(buildIndex, loadMode);
            return SceneManager.GetSceneByBuildIndex(buildIndex);
        }

        #endregion

        #region 异步加载

        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="mode">加载模式</param>
        /// <param name="onComplete">加载完成回调</param>
        /// <param name="onProgress">进度回调</param>
        /// <param name="suspendAtProgress">暂停加载的进度阈值（0-1），1表示不暂停，0.9f 用于 YooAsset 兼容</param>
        /// <param name="data">场景数据</param>
        public static SceneHandler LoadSceneAsync(string sceneName,
            SceneLoadMode mode = SceneLoadMode.Single,
            Action<SceneHandler> onComplete = null,
            Action<float> onProgress = null,
            float suspendAtProgress = 1f,
            ISceneData data = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                KitLogger.Error("[SceneKit] 场景名称不能为空");
                onComplete?.Invoke(null);
                return null;
            }

            // 检查是否已加载
            if (IsSceneLoaded(sceneName))
            {
                var existingHandler = GetSceneHandler(sceneName);
                KitLogger.Warning($"[SceneKit] 场景已加载: {sceneName}");
                onComplete?.Invoke(existingHandler);
                return existingHandler;
            }

            // 创建句柄
            var handler = CreateHandler(sceneName, -1, mode, data);
            
            // 发送加载开始事件
            EventKit.Type.Send(new SceneLoadStartEvent { SceneName = sceneName, Mode = mode });

            // 如果是 Single 模式，清理旧场景缓存
            if (mode == SceneLoadMode.Single)
            {
                ClearScenesForSingleMode(sceneName);
            }

            // 注册句柄
            RegisterHandler(handler);

            // 开始加载
            handler.Loader.LoadAsync(sceneName, mode,
                scene => OnSceneLoaded(handler, scene, onComplete),
                progress => OnSceneProgress(handler, progress, onProgress),
                suspendAtProgress);

            return handler;
        }

        /// <summary>
        /// 异步加载场景（通过 BuildIndex）
        /// </summary>
        /// <param name="buildIndex">场景在 Build Settings 中的索引</param>
        /// <param name="mode">加载模式</param>
        /// <param name="onComplete">加载完成回调</param>
        /// <param name="onProgress">进度回调</param>
        /// <param name="suspendAtProgress">暂停加载的进度阈值</param>
        /// <param name="data">场景数据</param>
        public static SceneHandler LoadSceneAsync(int buildIndex,
            SceneLoadMode mode = SceneLoadMode.Single,
            Action<SceneHandler> onComplete = null,
            Action<float> onProgress = null,
            float suspendAtProgress = 1f,
            ISceneData data = null)
        {
            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                KitLogger.Error($"[SceneKit] 无效的场景索引: {buildIndex}");
                onComplete?.Invoke(null);
                return null;
            }

            // 获取场景路径作为名称
            string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            // 检查是否已加载
            if (IsSceneLoaded(sceneName))
            {
                var existingHandler = GetSceneHandler(sceneName);
                KitLogger.Warning($"[SceneKit] 场景已加载: {sceneName}");
                onComplete?.Invoke(existingHandler);
                return existingHandler;
            }

            // 创建句柄
            var handler = CreateHandler(sceneName, buildIndex, mode, data);
            
            // 发送加载开始事件
            EventKit.Type.Send(new SceneLoadStartEvent { SceneName = sceneName, Mode = mode });

            // 如果是 Single 模式，清理旧场景缓存
            if (mode == SceneLoadMode.Single)
            {
                ClearScenesForSingleMode(sceneName);
            }

            // 注册句柄
            RegisterHandler(handler);

            // 开始加载
            handler.Loader.LoadAsync(buildIndex, mode,
                scene => OnSceneLoaded(handler, scene, onComplete),
                progress => OnSceneProgress(handler, progress, onProgress),
                suspendAtProgress);

            return handler;
        }

        /// <summary>
        /// 场景加载进度回调
        /// </summary>
        private static void OnSceneProgress(SceneHandler handler, float progress, Action<float> onProgress)
        {
            handler.UpdateProgress(progress);
            handler.IsSuspended = handler.Loader?.IsSuspended ?? false;
            
            // 发送进度事件
            EventKit.Type.Send(new SceneLoadProgressEvent 
            { 
                SceneName = handler.SceneName, 
                Progress = progress 
            });
            
            onProgress?.Invoke(progress);
        }

        /// <summary>
        /// 场景加载完成回调
        /// </summary>
        private static void OnSceneLoaded(SceneHandler handler, Scene scene, Action<SceneHandler> onComplete)
        {
            handler.Scene = scene;
            handler.SetState(SceneState.Loaded);
            handler.UpdateProgress(1f);
            handler.IsSuspended = false;

            // 如果是 Single 模式或第一个场景，设为活动场景
            if (handler.LoadMode == SceneLoadMode.Single || sActiveSceneHandler == null)
            {
                sActiveSceneHandler = handler;
                SceneManager.SetActiveScene(scene);
            }

            // 发送加载完成事件
            EventKit.Type.Send(new SceneLoadCompleteEvent 
            { 
                SceneName = handler.SceneName, 
                Scene = scene,
                Handler = handler
            });

            onComplete?.Invoke(handler);
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 创建并缓存场景句柄
        /// </summary>
        private static SceneHandler CreateHandler(string sceneName, int buildIndex, SceneLoadMode mode, ISceneData data)
        {
            var handler = SafePoolKit<SceneHandler>.Instance.Allocate();
            handler.SceneName = sceneName;
            handler.BuildIndex = buildIndex;
            handler.LoadMode = mode;
            handler.SceneData = data;
            handler.SetState(SceneState.Loading);
            handler.Loader = sLoaderPool.Allocate();
            return handler;
        }

        /// <summary>
        /// 注册场景句柄到缓存
        /// </summary>
        private static void RegisterHandler(SceneHandler handler)
        {
            if (handler == null || string.IsNullOrEmpty(handler.SceneName)) return;
            
            sSceneCache[handler.SceneName] = handler;
            if (!sLoadedScenesList.Contains(handler))
            {
                sLoadedScenesList.Add(handler);
            }
        }

        /// <summary>
        /// 从缓存移除场景句柄
        /// </summary>
        private static void UnregisterHandler(SceneHandler handler)
        {
            if (handler == null) return;
            
            if (!string.IsNullOrEmpty(handler.SceneName))
            {
                sSceneCache.Remove(handler.SceneName);
            }
            sLoadedScenesList.Remove(handler);
            
            if (sActiveSceneHandler == handler)
            {
                sActiveSceneHandler = null;
            }
        }

        /// <summary>
        /// 清理 Single 模式下的旧场景
        /// </summary>
        private static void ClearScenesForSingleMode(string newSceneName)
        {
            var handlersToRemove = new List<SceneHandler>();
            
            foreach (var handler in sLoadedScenesList)
            {
                if (handler.SceneName != newSceneName)
                {
                    handlersToRemove.Add(handler);
                }
            }

            foreach (var handler in handlersToRemove)
            {
                UnregisterHandler(handler);
                handler.OnRecycled();
                SafePoolKit<SceneHandler>.Instance.Recycle(handler);
            }
        }

        /// <summary>
        /// 设置活动场景句柄
        /// </summary>
        internal static void SetActiveSceneHandler(SceneHandler handler)
        {
            sActiveSceneHandler = handler;
        }

        /// <summary>
        /// 设置过渡状态
        /// </summary>
        internal static void SetTransitioning(bool value)
        {
            sIsTransitioning = value;
        }

        #endregion

        #region 场景卸载

        /// <summary>
        /// 异步卸载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="onComplete">卸载完成回调</param>
        public static void UnloadSceneAsync(string sceneName, Action onComplete = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                KitLogger.Error("[SceneKit] 场景名称不能为空");
                onComplete?.Invoke();
                return;
            }

            var handler = GetSceneHandler(sceneName);
            if (handler == null)
            {
                KitLogger.Warning($"[SceneKit] 场景未加载: {sceneName}");
                onComplete?.Invoke();
                return;
            }

            UnloadSceneAsync(handler, onComplete);
        }

        /// <summary>
        /// 异步卸载场景（通过句柄）
        /// </summary>
        /// <param name="handler">场景句柄</param>
        /// <param name="onComplete">卸载完成回调</param>
        public static void UnloadSceneAsync(SceneHandler handler, Action onComplete = null)
        {
            if (handler == null)
            {
                KitLogger.Error("[SceneKit] 场景句柄不能为 null");
                onComplete?.Invoke();
                return;
            }

            // 检查是否是最后一个场景
            if (sLoadedScenesList.Count <= 1)
            {
                KitLogger.Warning("[SceneKit] 无法卸载最后一个场景");
                onComplete?.Invoke();
                return;
            }

            // 检查场景状态
            if (handler.State == SceneState.Unloading || handler.State == SceneState.Unloaded)
            {
                KitLogger.Warning($"[SceneKit] 场景已在卸载中或已卸载: {handler.SceneName}");
                onComplete?.Invoke();
                return;
            }

            // 设置卸载状态
            handler.SetState(SceneState.Unloading);

            // 如果是活动场景，切换到其他场景
            if (sActiveSceneHandler == handler && sLoadedScenesList.Count > 1)
            {
                foreach (var h in sLoadedScenesList)
                {
                    if (h != handler && h.State == SceneState.Loaded)
                    {
                        sActiveSceneHandler = h;
                        if (h.Scene.IsValid())
                        {
                            SceneManager.SetActiveScene(h.Scene);
                        }
                        break;
                    }
                }
            }

            // 执行卸载
            if (handler.Loader != null)
            {
                handler.Loader.UnloadAsync(handler.Scene, () => OnSceneUnloaded(handler, onComplete));
            }
            else
            {
                // 使用默认卸载方式
                if (handler.Scene.IsValid())
                {
                    var op = SceneManager.UnloadSceneAsync(handler.Scene);
                    if (op != null)
                    {
                        op.completed += _ => OnSceneUnloaded(handler, onComplete);
                    }
                    else
                    {
                        OnSceneUnloaded(handler, onComplete);
                    }
                }
                else
                {
                    OnSceneUnloaded(handler, onComplete);
                }
            }
        }

        /// <summary>
        /// 场景卸载完成回调
        /// </summary>
        private static void OnSceneUnloaded(SceneHandler handler, Action onComplete)
        {
            handler.SetState(SceneState.Unloaded);

            // 发送卸载事件
            EventKit.Type.Send(new SceneUnloadEvent { SceneName = handler.SceneName });

            // 从缓存移除
            UnregisterHandler(handler);

            // 回收加载器
            if (handler.Loader != null)
            {
                sLoaderPool.Recycle(handler.Loader);
                handler.Loader = null;
            }

            // 回收句柄
            handler.OnRecycled();
            SafePoolKit<SceneHandler>.Instance.Recycle(handler);

            onComplete?.Invoke();
        }

        #endregion

        #region 预加载与暂停/恢复

        /// <summary>
        /// 预加载场景（加载但不激活）
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="onComplete">预加载完成回调</param>
        /// <param name="onProgress">进度回调</param>
        /// <param name="suspendAtProgress">暂停加载的进度阈值，默认 0.9f 用于 YooAsset 兼容</param>
        public static SceneHandler PreloadSceneAsync(string sceneName,
            Action<SceneHandler> onComplete = null,
            Action<float> onProgress = null,
            float suspendAtProgress = 0.9f)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                KitLogger.Error("[SceneKit] 场景名称不能为空");
                onComplete?.Invoke(null);
                return null;
            }

            // 检查是否已加载
            if (IsSceneLoaded(sceneName))
            {
                var existingHandler = GetSceneHandler(sceneName);
                KitLogger.Warning($"[SceneKit] 场景已加载: {sceneName}");
                onComplete?.Invoke(existingHandler);
                return existingHandler;
            }

            // 创建句柄
            var handler = CreateHandler(sceneName, -1, SceneLoadMode.Additive, null);
            handler.IsPreloaded = true;

            // 发送加载开始事件
            EventKit.Type.Send(new SceneLoadStartEvent { SceneName = sceneName, Mode = SceneLoadMode.Additive });

            // 注册句柄
            RegisterHandler(handler);

            // 开始加载（使用暂停阈值）
            handler.Loader.LoadAsync(sceneName, SceneLoadMode.Additive,
                scene => OnPreloadComplete(handler, scene, onComplete),
                progress => OnSceneProgress(handler, progress, onProgress),
                suspendAtProgress);

            return handler;
        }

        /// <summary>
        /// 预加载完成回调
        /// </summary>
        private static void OnPreloadComplete(SceneHandler handler, Scene scene, Action<SceneHandler> onComplete)
        {
            handler.Scene = scene;
            handler.SetState(SceneState.Loaded);
            handler.IsPreloaded = true;
            handler.IsSuspended = false;

            // 发送加载完成事件
            EventKit.Type.Send(new SceneLoadCompleteEvent
            {
                SceneName = handler.SceneName,
                Scene = scene,
                Handler = handler
            });

            onComplete?.Invoke(handler);
        }

        /// <summary>
        /// 激活预加载的场景
        /// </summary>
        /// <param name="handler">预加载的场景句柄</param>
        public static void ActivatePreloadedScene(SceneHandler handler)
        {
            if (handler == null)
            {
                KitLogger.Error("[SceneKit] 场景句柄不能为 null");
                return;
            }

            if (!handler.IsPreloaded)
            {
                KitLogger.Error($"[SceneKit] 场景未预加载: {handler.SceneName}");
                return;
            }

            // 如果加载器还在暂停状态，先恢复
            if (handler.IsSuspended && handler.Loader != null)
            {
                handler.Loader.ResumeLoad();
                handler.IsSuspended = false;
            }

            // 设置为活动场景
            if (handler.Scene.IsValid())
            {
                SceneManager.SetActiveScene(handler.Scene);
                sActiveSceneHandler = handler;
                
                // 发送活动场景切换事件
                EventKit.Type.Send(new ActiveSceneChangedEvent
                {
                    PreviousScene = GetActiveScene(),
                    NewScene = handler.Scene
                });
            }

            handler.IsPreloaded = false;
            handler.UpdateProgress(1f);
        }

        /// <summary>
        /// 恢复暂停的场景加载
        /// </summary>
        /// <param name="handler">场景句柄</param>
        public static void ResumeLoad(SceneHandler handler)
        {
            if (handler == null)
            {
                KitLogger.Error("[SceneKit] 场景句柄不能为 null");
                return;
            }

            if (!handler.IsSuspended)
            {
                KitLogger.Warning($"[SceneKit] 场景未暂停: {handler.SceneName}");
                return;
            }

            if (handler.Loader != null)
            {
                handler.Loader.ResumeLoad();
                handler.IsSuspended = false;
                KitLogger.Log($"[SceneKit] 恢复加载场景: {handler.SceneName}");
            }
        }

        /// <summary>
        /// 暂停场景加载
        /// </summary>
        /// <param name="handler">场景句柄</param>
        public static void SuspendLoad(SceneHandler handler)
        {
            if (handler == null)
            {
                KitLogger.Error("[SceneKit] 场景句柄不能为 null");
                return;
            }

            if (handler.State != SceneState.Loading)
            {
                KitLogger.Warning($"[SceneKit] 场景不在加载中: {handler.SceneName}");
                return;
            }

            if (handler.Loader != null)
            {
                handler.Loader.SuspendLoad();
                handler.IsSuspended = true;
                KitLogger.Log($"[SceneKit] 暂停加载场景: {handler.SceneName}");
            }
        }

        #endregion

        #region 场景切换（带过渡效果）

        /// <summary>
        /// 异步切换场景（带过渡效果）
        /// </summary>
        /// <param name="sceneName">目标场景名称</param>
        /// <param name="transition">过渡效果，null 则直接切换</param>
        /// <param name="data">场景数据</param>
        /// <param name="onComplete">切换完成回调</param>
        public static void SwitchSceneAsync(string sceneName,
            ISceneTransition transition = null,
            ISceneData data = null,
            Action<SceneHandler> onComplete = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                KitLogger.Error("[SceneKit] 场景名称不能为空");
                onComplete?.Invoke(null);
                return;
            }

            // 检查是否正在过渡中
            if (sIsTransitioning)
            {
                KitLogger.Warning("[SceneKit] 场景切换正在进行中，请等待完成");
                onComplete?.Invoke(null);
                return;
            }

            sIsTransitioning = true;

            // 无过渡效果，直接加载
            if (transition == null)
            {
                LoadSceneAsync(sceneName, SceneLoadMode.Single, handler =>
                {
                    sIsTransitioning = false;
                    onComplete?.Invoke(handler);
                }, data: data);
                return;
            }

            // 有过渡效果，执行淡出 -> 加载 -> 淡入
            transition.FadeOutAsync(() =>
            {
                LoadSceneAsync(sceneName, SceneLoadMode.Single, handler =>
                {
                    transition.FadeInAsync(() =>
                    {
                        sIsTransitioning = false;
                        onComplete?.Invoke(handler);
                    });
                }, data: data);
            });
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 卸载未使用的资源
        /// </summary>
        public static AsyncOperation UnloadUnusedAssets()
        {
            KitLogger.Log("[SceneKit] 开始卸载未使用的资源");
            return Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// 清理所有场景（卸载所有附加场景）
        /// </summary>
        /// <param name="preserveActive">是否保留当前活动场景</param>
        /// <param name="onComplete">清理完成回调</param>
        public static void ClearAllScenes(bool preserveActive = true, Action onComplete = null)
        {
            if (sLoadedScenesList.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var scenesToUnload = new List<SceneHandler>();
            
            foreach (var handler in sLoadedScenesList)
            {
                if (preserveActive && handler == sActiveSceneHandler)
                {
                    continue;
                }
                scenesToUnload.Add(handler);
            }

            if (scenesToUnload.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            int unloadedCount = 0;
            int totalCount = scenesToUnload.Count;

            foreach (var handler in scenesToUnload)
            {
                UnloadSceneAsync(handler, () =>
                {
                    unloadedCount++;
                    if (unloadedCount >= totalCount)
                    {
                        KitLogger.Log($"[SceneKit] 已清理 {totalCount} 个场景");
                        onComplete?.Invoke();
                    }
                });
            }
        }

        #endregion
    }
}
