using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// 场景管理工具 - 提供统一的场景加载、切换、卸载等 API
    /// </summary>
    public static partial class SceneKit
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
            Pool.List<SceneHandler>(handlersToRemove =>
            {
                foreach (var handler in sLoadedScenesList)
                {
                    if (handler.SceneName != newSceneName)
                    {
                        handlersToRemove.Add(handler);
                    }
                }

                for (int i = 0; i < handlersToRemove.Count; i++)
                {
                    var h = handlersToRemove[i];
                    UnregisterHandler(h);
                    h.OnRecycled();
                    SafePoolKit<SceneHandler>.Instance.Recycle(h);
                }
            });
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
    }
}
