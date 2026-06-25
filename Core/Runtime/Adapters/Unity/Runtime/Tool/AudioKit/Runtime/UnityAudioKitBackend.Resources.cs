#if !GODOT
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.Unity
{
    public sealed partial class UnityAudioKitBackend
    {
        public void Preload(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var key = Normalize(path);
            if (mClips.ContainsKey(key))
                return;

            var clip = LoadClip(key);
            if (clip == null)
            {
                LogKit.Warning("[AudioKit] 预加载音频资源失败: " + path);
                return;
            }
        }

        public void PreloadAsync(string path, Action onComplete)
        {
            Preload(path);
            if (onComplete != null)
                onComplete();
        }

        public void Unload(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var key = Normalize(path);
            AudioClip clip;
            if (!mClips.TryGetValue(key, out clip) || clip == null)
            {
                mClips.Remove(key);
                mClips.Remove(RemoveExtension(key));
                return;
            }

            ReleaseProviderClip(clip);
            RemoveClipAliases(clip);
        }

        public void UnloadAll()
        {
            ReleaseAllProviderClips();
            mClips.Clear();
        }

        private AudioClip ResolveClip(string path)
        {
            var key = Normalize(path);
            if (mClips.TryGetValue(key, out var clip) && clip != null)
                return clip;

            return LoadClip(key);
        }

        private async System.Threading.Tasks.Task<AudioClip> ResolveClipAsync(string path)
        {
            var key = Normalize(path);
            if (mClips.TryGetValue(key, out var cachedClip) && cachedClip != null)
                return cachedClip;

            var loader = GetEffectiveResourceLoader();
            if (loader != null)
            {
                var loadedClip = await loader.LoadAsync<AudioClip>(key);
                if (loadedClip != null)
                {
                    RegisterClip(key, loadedClip);
                    mClipLoaders[loadedClip] = loader;
                    return loadedClip;
                }
            }

            var resourcesClip = Resources.Load<AudioClip>(key);
            if (resourcesClip != null)
            {
                RegisterClip(key, resourcesClip);
                return resourcesClip;
            }

            return null;
        }
        private AudioClip LoadClip(string key)
        {
            var loader = GetEffectiveResourceLoader();
            if (loader != null)
            {
                var loadedClip = loader.Load<AudioClip>(key);
                if (loadedClip != null)
                {
                    RegisterClip(key, loadedClip);
                    mClipLoaders[loadedClip] = loader;
                    return loadedClip;
                }
            }

            var resourcesClip = Resources.Load<AudioClip>(key);
            if (resourcesClip != null)
            {
                RegisterClip(key, resourcesClip);
                return resourcesClip;
            }

            return null;
        }

        private IAudioResourceLoader GetEffectiveResourceLoader()
        {
            return mResourceLoader ?? AudioKit.GetResourceLoader();
        }

        private void RemoveClipAliases(AudioClip clip)
        {
            var keys = new List<string>();
            foreach (var pair in mClips)
            {
                if (pair.Value == clip)
                    keys.Add(pair.Key);
            }

            for (var i = 0; i < keys.Count; i++)
                mClips.Remove(keys[i]);
        }

        private void ReleaseProviderClip(AudioClip clip)
        {
            if (clip == null)
                return;

            IAudioResourceLoader loader;
            if (!mClipLoaders.TryGetValue(clip, out loader))
                return;

            mClipLoaders.Remove(clip);
            loader.Release(clip);
        }

        private void ReleaseAllProviderClips()
        {
            var clips = new List<AudioClip>(mClipLoaders.Keys);
            for (var i = 0; i < clips.Count; i++)
                ReleaseProviderClip(clips[i]);
        }
    }
}
#endif
