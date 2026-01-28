using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// 场景加载追踪器 - 追踪所有通过底层 SceneLoader 加载的场景
    /// 编辑器模式下用于监控，运行时为空实现（零开销）
    /// </summary>
    public static class SceneLoadTracker
    {
        /// <summary>
        /// 追踪的场景信息
        /// </summary>
        public class TrackedScene
        {
            public string Path;
            public Scene Scene;
            public ISceneResLoader Loader;
            public bool IsLoaded;
            public bool IsAdditive;
        }

        // 使用 Loader 实例作为 key，因为每个 Loader 只加载一个场景
        private static readonly Dictionary<ISceneResLoader, TrackedScene> sTrackedScenes = new();

#if UNITY_EDITOR
        /// <summary>
        /// 记录场景加载
        /// </summary>
        public static void OnLoad(ISceneResLoader loader, string path, Scene scene, bool isAdditive)
        {
            if (loader == null) return;
            
            if (!sTrackedScenes.TryGetValue(loader, out var tracked))
            {
                tracked = new TrackedScene();
                sTrackedScenes[loader] = tracked;
            }
            
            tracked.Path = path;
            tracked.Scene = scene;
            tracked.Loader = loader;
            tracked.IsLoaded = scene.IsValid() && scene.isLoaded;
            tracked.IsAdditive = isAdditive;
            
            KitLogger.DebugLog($"[SceneLoadTracker] 追踪场景加载: {path}, IsValid={scene.IsValid()}, IsLoaded={scene.isLoaded}, Count={sTrackedScenes.Count}");
        }

        /// <summary>
        /// 记录场景卸载
        /// </summary>
        public static void OnUnload(ISceneResLoader loader)
        {
            if (loader == null) return;
            var removed = sTrackedScenes.Remove(loader);
            KitLogger.DebugLog($"[SceneLoadTracker] 追踪场景卸载: Removed={removed}, Count={sTrackedScenes.Count}");
        }

        /// <summary>
        /// 获取所有追踪的场景
        /// </summary>
        public static IReadOnlyDictionary<ISceneResLoader, TrackedScene> GetTrackedScenes() => sTrackedScenes;

        /// <summary>
        /// 清空追踪数据
        /// </summary>
        public static void Clear() => sTrackedScenes.Clear();
#else
        // 运行时空实现
        public static void OnLoad(ISceneResLoader loader, string path, Scene scene, bool isAdditive) { }
        public static void OnUnload(ISceneResLoader loader) { }
        public static IReadOnlyDictionary<ISceneResLoader, TrackedScene> GetTrackedScenes() => null;
        public static void Clear() { }
#endif
    }
}
