#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame.Json;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>Runtime-created objects can also have nonzero GlobalObjectIds. Trust a pre-Play baseline only.</summary>
    internal static class UnityLiveSnapshotIdentities
    {
        private const string SessionKey = "YokiFrame.LiveCode.SavedSceneIdentities.v1";
        private const int MaxObjects = 32768;
        private static HashSet<string> sSaved;
        private static bool sInstalled;

        internal static void Install()
        {
            if (sInstalled) return;
            sInstalled = true;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        internal static bool IsSaved(Object value, string globalId)
        {
            if (!UnityLiveFieldState.IsPersistentId(globalId)) return false;
            if (EditorUtility.IsPersistent(value)) return true;
            if (sSaved == null)
            {
                sSaved = new HashSet<string>(StringComparer.Ordinal);
                string saved = SessionState.GetString(SessionKey, "[]");
                using var document = JsonDocument.Parse(saved);
                foreach (var item in document.RootElement.EnumerateArray()) sSaved.Add(item.GetString());
            }
            return sSaved.Contains(globalId);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            sSaved = new HashSet<string>(StringComparer.Ordinal);
            SessionState.SetString(SessionKey, "[]");
            int visited = 0;
            var stack = new Stack<Transform>();
            var components = new List<Component>();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded || scene.isDirty || string.IsNullOrEmpty(scene.path)) continue;
                foreach (var root in scene.GetRootGameObjects()) stack.Push(root.transform);
                while (stack.Count > 0)
                {
                    Transform node = stack.Pop();
                    if (++visited > MaxObjects) { sSaved.Clear(); return; }
                    Add(node.gameObject);
                    node.GetComponents(components);
                    foreach (Component component in components)
                    {
                        if (++visited > MaxObjects) { sSaved.Clear(); return; }
                        if (component != default) Add(component);
                    }
                    for (int index = 0; index < node.childCount; index++) stack.Push(node.GetChild(index));
                }
            }
            var json = new YokiFrameEngineJsonBuilder().StartArray();
            foreach (string id in sSaved) json.String(id);
            string text = json.EndArray().ToString();
            if (text.Length > YokiFrameLiveSnapshotStore.MaxBytes) { sSaved.Clear(); return; }
            SessionState.SetString(SessionKey, text);
        }

        private static void Add(Object value)
        {
            string id = GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
            if (UnityLiveFieldState.IsPersistentId(id)) sSaved.Add(id);
        }
    }
}
#endif
