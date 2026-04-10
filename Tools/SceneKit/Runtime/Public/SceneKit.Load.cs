using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// SceneKit - 同步/异步加载方法
    /// </summary>
    public static partial class SceneKit
    {
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

                if (existingHandler.State == SceneState.Loaded)
                {
                    KitLogger.Warning($"[SceneKit] 场景已加载: {sceneName}");
                    onComplete?.Invoke(existingHandler);
                }
                else
                {
                    existingHandler.AddLoadedCallback(onComplete);
                }

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

                if (existingHandler.State == SceneState.Loaded)
                {
                    KitLogger.Warning($"[SceneKit] 场景已加载: {sceneName}");
                    onComplete?.Invoke(existingHandler);
                }
                else
                {
                    existingHandler.AddLoadedCallback(onComplete);
                }

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
            handler.InvokeLoadedCallbacks();
        }

        #endregion
    }
}
