#if !GODOT
using UnityEngine;

namespace YokiFrame.Unity
{
    public sealed partial class UnityAudioKitBackend
    {
        private void EnsureRoot()
        {
            if (mRoot != null)
                return;

            mRoot = new GameObject("YokiFrameAudioKit");
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(mRoot);
        }

        private void RegisterAlias(string path, AudioClip clip)
        {
            var key = Normalize(path);
            if (string.IsNullOrEmpty(key) || clip == null)
                return;

            mClips[key] = clip;
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        private static string RemoveExtension(string path)
        {
            var normalized = Normalize(path);
            var index = normalized.LastIndexOf('.');
            return index > 0 ? normalized.Substring(0, index) : normalized;
        }
    }
}
#endif
