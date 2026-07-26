using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public static partial class SceneKit
    {
        /// <summary>恢复当前挂起操作，并在同步完成回调清空 Operation 时避免再次访问失效引用。</summary>
        /// <param name="handler">需要继续完成加载的 Handler。</param>
        private static void ResumeSuspendedLoad(SceneHandler handler)
        {
            ISceneLoadOperation operation = handler.Operation;
            if (!handler.IsSuspended || operation == null)
            {
                return;
            }

            operation.ResumeLoad();
            if (ReferenceEquals(handler.Operation, operation))
            {
                handler.IsSuspended = operation.IsSuspended;
            }
        }

        /// <summary>在加载完成后执行卸载请求，避免等待回调期间丢失 Handler。</summary>
        private static void UnloadLoadedHandler(SceneHandler handler)
        {
            if (handler.Backend == null || !handler.Scene.IsValid)
            {
                OnSceneUnloaded(handler);
                return;
            }

            handler.Backend.UnloadSceneAsync(handler.Scene, () => OnSceneUnloaded(handler));
        }

        /// <summary>回收已结束的加载操作并清除 Handler 引用。</summary>
        private static void CompleteOperation(SceneHandler handler)
        {
            if (handler.Operation == null)
            {
                return;
            }

            handler.Operation.Recycle();
            handler.Operation = null;
        }

        /// <summary>卸载单场景替换时旧的逻辑 Handler，并等待后端确认。先快照再遍历，防止同步卸载回调修改集合导致越界。</summary>
        private static void UnloadReplacedScenes(SceneHandler activeHandler)
        {
            var targets = new List<SceneHandler>(sLoadedScenes.Count);
            for (var index = 0; index < sLoadedScenes.Count; index++)
            {
                SceneHandler handler = sLoadedScenes[index];
                if (!ReferenceEquals(handler, activeHandler))
                {
                    targets.Add(handler);
                }
            }

            for (var index = 0; index < targets.Count; index++)
            {
                SceneHandler handler = targets[index];
                if (handler.State != SceneState.Unloaded)
                {
                    UnloadSceneAsync(handler);
                }
            }
        }

        /// <summary>完成后端卸载、移除缓存并在没有激活场景时提升候选场景。</summary>
        private static void OnSceneUnloaded(SceneHandler handler)
        {
            if (handler.State == SceneState.Unloaded)
            {
                return;
            }

            string sceneName = handler.SceneName;
            bool wasActive = ReferenceEquals(sActiveSceneHandler, handler);
            SceneHandle previousScene = handler.Scene;
            Action callbacks = handler.TakeUnloadCallbacks();
            UnregisterHandler(handler);
            handler.MarkUnloaded();
            EventKit.Type.Send(new SceneUnloadEvent { SceneName = sceneName });
            if (wasActive)
            {
                PromoteFirstLoadedScene(previousScene);
            }

            callbacks?.Invoke();
        }

        /// <summary>在场景完成加载后执行 Provider 的激活操作。</summary>
        private static void SetActiveScene(SceneHandler handler)
        {
            SceneHandle previous = sActiveSceneHandler == null ? default : sActiveSceneHandler.Scene;
            SetActiveScene(handler, previous);
        }

        /// <summary>切换激活 Handler，并使用指定旧句柄发布准确的场景切换事件。</summary>
        /// <param name="handler">要成为激活场景的已加载 Handler。</param>
        /// <param name="previous">切换前的场景句柄。</param>
        private static void SetActiveScene(SceneHandler handler, SceneHandle previous)
        {
            bool changed = !ReferenceEquals(sActiveSceneHandler, handler);
            sActiveSceneHandler = handler;
            handler.Backend?.SetActiveScene(handler.Scene);
            if (changed)
            {
                EventKit.Type.Send(new ActiveSceneChangedEvent
                {
                    PreviousScene = previous,
                    NewScene = handler.Scene
                });
            }
        }

        /// <summary>在卸载激活场景后提升第一个 Loaded Handler，或发布空激活场景状态。</summary>
        /// <param name="previousScene">刚刚卸载的激活场景句柄。</param>
        private static void PromoteFirstLoadedScene(SceneHandle previousScene)
        {
            for (var index = 0; index < sLoadedScenes.Count; index++)
            {
                SceneHandler candidate = sLoadedScenes[index];
                if (candidate.State == SceneState.Loaded)
                {
                    SetActiveScene(candidate, previousScene);
                    return;
                }
            }

            EventKit.Type.Send(new ActiveSceneChangedEvent
            {
                PreviousScene = previousScene,
                NewScene = default
            });
        }

        /// <summary>从缓存和加载列表中移除指定 Handler。</summary>
        private static void UnregisterHandler(SceneHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            if (sSceneCache.TryGetValue(handler.SceneName, out SceneHandler cached)
                && ReferenceEquals(cached, handler))
            {
                sSceneCache.Remove(handler.SceneName);
            }

            sLoadedScenes.Remove(handler);
            if (ReferenceEquals(sActiveSceneHandler, handler))
            {
                sActiveSceneHandler = null;
            }
        }
    }
}
