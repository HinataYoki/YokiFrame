using System;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// SceneKit - 预加载和暂停/恢复方法
    /// </summary>
    public static partial class SceneKit
    {
        #region 预加载

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
            handler.InvokeLoadedCallbacks();
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

        #endregion

        #region 暂停/恢复

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
    }
}
