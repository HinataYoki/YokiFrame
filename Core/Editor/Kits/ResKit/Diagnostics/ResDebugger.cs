#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit Editor monitor publisher.
    /// Collects loaded-resource snapshots from runtime caches and trackers, then publishes
    /// resource-list and unload events to the shared Editor bus.
    /// </summary>
    public static class ResDebugger
    {
        #region Channels

        /// <summary>
        /// Published when the loaded resource list changes.
        /// Payload type: <c>int</c>, the current loaded count.
        /// </summary>
        public const string CHANNEL_RES_LIST_CHANGED = DataChannels.RES_LIST_CHANGED;

        /// <summary>
        /// Published when a resource unload is detected.
        /// Payload type: <see cref="UnloadRecord"/>.
        /// </summary>
        public const string CHANNEL_RES_UNLOADED = DataChannels.RES_UNLOADED;

        #endregion

        /// <summary>
        /// Source category for a loaded resource snapshot.
        /// </summary>
        public enum ResSource
        {
            /// <summary>
            /// Loaded through the ResKit cache pipeline.
            /// </summary>
            ResKit,

            /// <summary>
            /// Loaded directly by a lower-level loader, such as UIKit or scene loading.
            /// </summary>
            Loader
        }

        /// <summary>
        /// Snapshot describing a currently loaded resource.
        /// </summary>
        public readonly struct ResInfo
        {
            public readonly string Path;
            public readonly string TypeName;
            public readonly int RefCount;
            public readonly bool IsDone;
            public readonly ResSource Source;

            public ResInfo(string path, string typeName, int refCount, bool isDone, ResSource source)
            {
                Path = path;
                TypeName = typeName;
                RefCount = refCount;
                IsDone = isDone;
                Source = source;
            }
        }

        /// <summary>
        /// History entry describing one detected unload.
        /// </summary>
        public readonly struct UnloadRecord
        {
            public readonly string Path;
            public readonly string TypeName;
            public readonly DateTime UnloadTime;
            public readonly string StackTrace;

            public UnloadRecord(string path, string typeName, DateTime unloadTime, string stackTrace)
            {
                Path = path;
                TypeName = typeName;
                UnloadTime = unloadTime;
                StackTrace = stackTrace;
            }
        }

        #region Runtime Snapshot State

        private static FieldInfo sCacheField;
        private static bool sFieldCached;

        private static readonly List<UnloadRecord> sUnloadHistory = new(128);
        private static readonly HashSet<string> sPreviousLoadedPaths = new();
        private const int MAX_HISTORY_COUNT = 100;
        private static int sLastLoadedCount;

        /// <summary>
        /// Returns the cached unload history.
        /// </summary>
        public static IReadOnlyList<UnloadRecord> GetUnloadHistory() => sUnloadHistory;

        #endregion

        #region Editor Bridge Publish

        /// <summary>
        /// Publishes a resource-monitor payload to the shared Editor bus.
        /// Legacy resource monitoring still publishes directly from the debugger layer,
        /// but the page remains isolated behind stable channels.
        /// </summary>
        private static void NotifyEditorDataChanged<T>(string channel, T data) =>
            EditorDataBridge.NotifyDataChanged(channel, data);

        #endregion

        #region Lifecycle

        /// <summary>
        /// Initializes lifecycle cleanup for the legacy resource monitor publisher.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    ClearRuntimeMonitorState();
                }
            };
        }

        /// <summary>
        /// Clears unload history and the previous loaded-resource snapshot.
        /// </summary>
        public static void ClearUnloadHistory()
        {
            sUnloadHistory.Clear();
            sPreviousLoadedPaths.Clear();
        }

        /// <summary>
        /// Clears all runtime monitor caches retained by the resource debugger publisher.
        /// This is the lifecycle reset entry shared by PlayMode teardown and explicit monitor resets.
        /// </summary>
        public static void ClearRuntimeMonitorState()
        {
            sUnloadHistory.Clear();
            sPreviousLoadedPaths.Clear();
            sLastLoadedCount = 0;
        }

        #endregion

        #region Runtime Publish Handlers

        /// <summary>
        /// Detects resource unloads and publishes Editor notifications when changes are found.
        /// This method intentionally remains polling-driven because not every unload path currently
        /// exposes a reliable push callback.
        /// </summary>
        public static void DetectUnloadedAssets()
        {
            if (!EditorApplication.isPlaying) return;

            var currentLoaded = GetLoadedAssets();
            var currentPaths = new HashSet<string>();

            foreach (var asset in currentLoaded)
            {
                currentPaths.Add(GetAssetKey(asset.Path, asset.TypeName));
            }

            bool hasUnloaded = false;

            foreach (var prevPath in sPreviousLoadedPaths)
            {
                if (!currentPaths.Contains(prevPath))
                {
                    var parts = prevPath.Split('|');
                    var path = parts.Length > 0 ? parts[0] : prevPath;
                    var typeName = parts.Length > 1 ? parts[1] : "Unknown";
                    var stackTrace = GetSimplifiedStackTrace();

                    var record = new UnloadRecord(
                        path,
                        typeName,
                        DateTime.Now,
                        stackTrace);

                    sUnloadHistory.Insert(0, record);
                    if (sUnloadHistory.Count > MAX_HISTORY_COUNT)
                    {
                        sUnloadHistory.RemoveAt(sUnloadHistory.Count - 1);
                    }

                    hasUnloaded = true;
                    NotifyEditorDataChanged(CHANNEL_RES_UNLOADED, record);
                }
            }

            sPreviousLoadedPaths.Clear();
            foreach (var path in currentPaths)
            {
                sPreviousLoadedPaths.Add(path);
            }

            int currentCount = currentLoaded.Count;
            if (currentCount != sLastLoadedCount || hasUnloaded)
            {
                sLastLoadedCount = currentCount;
                NotifyEditorDataChanged(CHANNEL_RES_LIST_CHANGED, currentCount);
            }
        }

        #endregion

        #region Snapshot Query Helpers

        /// <summary>
        /// Builds a unique key for a resource snapshot.
        /// </summary>
        private static string GetAssetKey(string path, string typeName) => $"{path}|{typeName}";

        /// <summary>
        /// Extracts a shorter business-facing call stack from the current environment stack trace.
        /// </summary>
        private static string GetSimplifiedStackTrace()
        {
            var fullTrace = Environment.StackTrace;
            var lines = fullTrace.Split('\n');
            var result = new System.Text.StringBuilder();
            int count = 0;

            foreach (var line in lines)
            {
                if (line.Contains("ResDebugger") ||
                    line.Contains("EditorApplication") ||
                    line.Contains("UnityEditor") ||
                    line.Contains("System.Environment"))
                {
                    continue;
                }

                if (line.Contains("YokiFrame") || line.Contains("Assets/"))
                {
                    result.AppendLine(line.Trim());
                    count++;
                    if (count >= 5) break;
                }
            }

            return result.Length > 0 ? result.ToString() : "No useful stack trace.";
        }

        /// <summary>
        /// Returns snapshots for all currently loaded resources, including ResKit cache entries,
        /// lower-level loader entries, and tracked scenes.
        /// </summary>
        public static List<ResInfo> GetLoadedAssets()
        {
            var result = new List<ResInfo>();

            if (!EditorApplication.isPlaying) return result;

            var cache = GetAssetCache();
            if (cache != null)
            {
                foreach (var kvp in cache)
                {
                    var handler = kvp.Value;
                    result.Add(new ResInfo(
                        handler.Path,
                        handler.AssetType?.Name ?? "Unknown",
                        handler.RefCount,
                        handler.IsDone,
                        ResSource.ResKit));
                }
            }

            var tracked = ResLoadTracker.GetTrackedAssets();
            if (tracked != null)
            {
                foreach (var kvp in tracked)
                {
                    var asset = kvp.Value;
                    if (!IsInResKitCache(asset.Path, asset.AssetType, cache))
                    {
                        result.Add(new ResInfo(
                            asset.Path,
                            asset.AssetType?.Name ?? "Unknown",
                            1,
                            asset.IsLoaded,
                            ResSource.Loader));
                    }
                }
            }

            var trackedScenes = SceneLoadTracker.GetTrackedScenes();
            if (trackedScenes != null)
            {
                foreach (var kvp in trackedScenes)
                {
                    var scene = kvp.Value;
                    result.Add(new ResInfo(
                        scene.Path,
                        "Scene",
                        1,
                        scene.IsLoaded,
                        ResSource.Loader));
                }
            }

            return result;
        }

        /// <summary>
        /// Checks whether a tracked loader entry is already represented in the ResKit cache.
        /// </summary>
        private static bool IsInResKitCache(string path, Type assetType, Dictionary<ResCacheKey, ResHandler> cache)
        {
            if (cache == null || string.IsNullOrEmpty(path)) return false;
            var key = new ResCacheKey(assetType, path);
            return cache.ContainsKey(key);
        }

        /// <summary>
        /// Returns the current loaded-resource count.
        /// </summary>
        public static int GetLoadedCount()
        {
            if (!EditorApplication.isPlaying) return 0;

            int count = 0;

            var cache = GetAssetCache();
            if (cache != null) count += cache.Count;

            var tracked = ResLoadTracker.GetTrackedAssets();
            if (tracked != null)
            {
                foreach (var kvp in tracked)
                {
                    if (!IsInResKitCache(kvp.Value.Path, kvp.Value.AssetType, cache))
                    {
                        count++;
                    }
                }
            }

            var trackedScenes = SceneLoadTracker.GetTrackedScenes();
            if (trackedScenes != null) count += trackedScenes.Count;

            return count;
        }

        /// <summary>
        /// Returns the current total reference count inside the ResKit cache.
        /// </summary>
        public static int GetTotalRefCount()
        {
            if (!EditorApplication.isPlaying) return 0;

            var cache = GetAssetCache();
            if (cache == null) return 0;

            int total = 0;
            foreach (var handler in cache.Values)
            {
                total += handler.RefCount;
            }

            return total;
        }

        /// <summary>
        /// Reads the internal ResKit asset cache through reflection.
        /// </summary>
        private static Dictionary<ResCacheKey, ResHandler> GetAssetCache()
        {
            if (!sFieldCached)
            {
                var type = typeof(ResKit);
                sCacheField = type.GetField("sAssetCache", BindingFlags.NonPublic | BindingFlags.Static);
                sFieldCached = true;
            }

            return sCacheField?.GetValue(null) as Dictionary<ResCacheKey, ResHandler>;
        }

        #endregion
    }
}
#endif
