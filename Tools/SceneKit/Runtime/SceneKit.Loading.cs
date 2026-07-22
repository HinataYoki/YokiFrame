using System;

namespace YokiFrame
{
    public static partial class SceneKit
    {
        /// <summary>启动一次由当前默认或显式后端执行的场景加载。</summary>
        private static SceneHandler StartLoad(
            SceneLoadRequest request,
            Action<SceneHandler> onComplete,
            Action<float> onProgress,
            Action<SceneHandler> onSuspended)
        {
            SceneHandler existing = GetSceneHandler(request.SceneName);
            if (existing != null && existing.State == SceneState.Unloading)
            {
                // 同名后端卸载尚未完成时不能覆盖缓存或并发重载，调用方应在卸载回调后重试。
                onComplete?.Invoke(null);
                return null;
            }

            existing = GetReusableHandler(existing, onComplete);
            if (existing != null)
            {
                return existing;
            }

            ISceneBackend backend = EnsureBackend();
            SceneHandler handler = RegisterLoadingHandler(request, backend, onProgress);
            BeginBackendLoad(handler, request, onComplete, onProgress, onSuspended);
            return handler;
        }

        /// <summary>复用仍在有效生命周期中的 Handler；失败或已卸载 Handler 会先移除。</summary>
        /// <param name="existing">同一缓存键下已登记的 Handler。</param>
        /// <param name="onComplete">当前请求的完成回调。</param>
        /// <returns>可复用的 Handler；不存在或已终止时返回空。</returns>
        private static SceneHandler GetReusableHandler(SceneHandler existing, Action<SceneHandler> onComplete)
        {
            if (existing == null)
            {
                return null;
            }

            if (existing.State == SceneState.Unloaded || existing.State == SceneState.Failed)
            {
                UnregisterHandler(existing);
                return null;
            }

            if (existing.State == SceneState.Loaded)
            {
                onComplete?.Invoke(existing);
            }
            else
            {
                existing.AddLoadedCallback(onComplete);
            }

            return existing;
        }

        /// <summary>创建并登记新的 Loading Handler，同时发送首次加载事件与零进度。</summary>
        private static SceneHandler RegisterLoadingHandler(
            SceneLoadRequest request,
            ISceneBackend backend,
            Action<float> onProgress)
        {
            var handler = new SceneHandler();
            handler.Reset(request.SceneName, request.BuildIndex, request.Mode, request.Data, request.IsPreload, backend);
            sSceneCache[request.SceneName] = handler;
            sLoadedScenes.Add(handler);
            EventKit.Type.Send(new SceneLoadStartEvent { SceneName = handler.SceneName, Mode = handler.LoadMode });
            ReportProgress(handler, 0f, onProgress, true);
            return handler;
        }

        /// <summary>调用后端并处理同步完成、异常回滚和异步操作所有权。</summary>
        private static void BeginBackendLoad(
            SceneHandler handler,
            SceneLoadRequest request,
            Action<SceneHandler> onComplete,
            Action<float> onProgress,
            Action<SceneHandler> onSuspended)
        {
            try
            {
                ISceneLoadOperation operation = handler.Backend.LoadSceneAsync(
                    request,
                    result => OnSceneLoaded(handler, result, onComplete, onProgress),
                    progress => ReportProgress(handler, progress, onProgress),
                    () => OnSceneSuspended(handler, onSuspended));
                if (handler.State == SceneState.Loading || handler.State == SceneState.Unloading)
                {
                    handler.Operation = operation;
                    if (handler.State == SceneState.Unloading || handler.ActivateWhenLoaded)
                    {
                        ResumeSuspendedLoad(handler);
                    }

                    return;
                }

                operation?.Recycle();
            }
            catch
            {
                UnregisterHandler(handler);
                handler.MarkUnloaded();
                throw;
            }
        }

        /// <summary>报告有效进度变化，避免宿主帧循环重复值反复分配事件对象和调用回调。</summary>
        /// <param name="handler">当前加载 Handler。</param>
        /// <param name="progress">Provider 报告的原始进度。</param>
        /// <param name="callback">调用方注册的进度回调。</param>
        /// <param name="force">是否在进度未变化时仍发送一次通知，用于首帧和终态保证。</param>
        private static void ReportProgress(
            SceneHandler handler,
            float progress,
            Action<float> callback,
            bool force = false)
        {
            float previousProgress = handler.Progress;
            handler.UpdateProgress(progress);
            if (!force && previousProgress == handler.Progress)
            {
                return;
            }

            if (handler.Operation != null)
            {
                handler.IsSuspended = handler.Operation.IsSuspended;
            }

            EventKit.Type.Send(new SceneLoadProgressEvent
            {
                SceneName = handler.SceneName,
                Progress = handler.Progress
            });
            callback?.Invoke(handler.Progress);
        }

        /// <summary>处理 Provider 的挂起通知并转发预加载回调。</summary>
        private static void OnSceneSuspended(SceneHandler handler, Action<SceneHandler> callback)
        {
            handler.IsSuspended = true;
            if (handler.Operation != null)
            {
                handler.UpdateProgress(handler.Operation.Progress);
            }

            callback?.Invoke(handler);
        }

        /// <summary>处理 Provider 完成回调，并路由到失败、卸载或正常完成分支。</summary>
        private static void OnSceneLoaded(
            SceneHandler handler,
            SceneLoadResult result,
            Action<SceneHandler> onComplete,
            Action<float> onProgress)
        {
            handler.Scene = result.Scene;
            if (!result.Succeeded)
            {
                CompleteFailedLoad(handler, onComplete);
                return;
            }

            if (handler.State == SceneState.Unloading)
            {
                CompleteLoadBeforeUnload(handler, onComplete, onProgress);
                return;
            }

            CompleteSuccessfulLoad(handler, onComplete, onProgress);
        }

        /// <summary>把无效场景结果转换为 Failed，并通知全部加载等待者。</summary>
        private static void CompleteFailedLoad(SceneHandler handler, Action<SceneHandler> onComplete)
        {
            CompleteOperation(handler);
            if (handler.State == SceneState.Unloading)
            {
                onComplete?.Invoke(null);
                handler.InvokeLoadCallbacks(null);
                OnSceneUnloaded(handler);
                return;
            }

            handler.SetState(SceneState.Failed);
            handler.IsSuspended = false;
            handler.ActivateWhenLoaded = false;
            EventKit.Type.Send(new SceneLoadFailedEvent { SceneName = handler.SceneName, Handler = handler });
            onComplete?.Invoke(null);
            handler.InvokeLoadCallbacks(null);
        }

        /// <summary>完成已被请求卸载的加载，并且只提交一次后端卸载。</summary>
        private static void CompleteLoadBeforeUnload(
            SceneHandler handler,
            Action<SceneHandler> onComplete,
            Action<float> onProgress)
        {
            ReportProgress(handler, COMPLETE_PROGRESS, onProgress, true);
            handler.IsSuspended = false;
            CompleteOperation(handler);
            onComplete?.Invoke(handler);
            handler.InvokeLoadCallbacks(handler);
            UnloadLoadedHandler(handler);
        }

        /// <summary>完成正常加载、按模式激活场景并卸载 Single 模式替换的旧场景。</summary>
        private static void CompleteSuccessfulLoad(
            SceneHandler handler,
            Action<SceneHandler> onComplete,
            Action<float> onProgress)
        {
            bool activateWhenLoaded = handler.ActivateWhenLoaded;
            handler.ActivateWhenLoaded = false;
            handler.SetState(SceneState.Loaded);
            ReportProgress(handler, COMPLETE_PROGRESS, onProgress, true);
            handler.IsSuspended = false;
            CompleteOperation(handler);
            if (activateWhenLoaded
                || (!handler.IsPreloaded
                    && (handler.LoadMode == SceneLoadMode.Single || sActiveSceneHandler == null)))
            {
                SetActiveScene(handler);
            }

            if (handler.LoadMode == SceneLoadMode.Single)
            {
                UnloadReplacedScenes(handler);
            }

            EventKit.Type.Send(new SceneLoadCompleteEvent
            {
                SceneName = handler.SceneName,
                Scene = handler.Scene,
                Handler = handler
            });
            onComplete?.Invoke(handler);
            handler.InvokeLoadCallbacks(handler);
        }
    }
}
