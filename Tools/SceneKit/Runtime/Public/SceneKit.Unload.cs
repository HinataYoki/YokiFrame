using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// SceneKit - 卸载和清理方法
    /// </summary>
    public static partial class SceneKit
    {
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

            if (handler.State == SceneState.Loading)
            {
                KitLogger.Warning($"[SceneKit] 场景正在加载中，无法卸载: {handler.SceneName}");
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

            // 使用临时数组避免在回调中修改集合
            var scenesToUnload = new SceneHandler[sLoadedScenesList.Count];
            int count = 0;
            
            for (int i = 0; i < sLoadedScenesList.Count; i++)
            {
                var handler = sLoadedScenesList[i];
                if (preserveActive && handler == sActiveSceneHandler)
                {
                    continue;
                }
                scenesToUnload[count++] = handler;
            }

            if (count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            int unloadedCount = 0;
            int totalCount = count;

            for (int i = 0; i < count; i++)
            {
                UnloadSceneAsync(scenesToUnload[i], () =>
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
