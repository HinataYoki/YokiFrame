using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace YokiFrame
{
    public static class AudioKit
    {
        private static Dictionary<AudioType, AudioPlayerBase> players = new();
        private static IAudioLoader defaultLoader;
        private static GameObject KitObject;

        public static ObjectPool<IAudioTrack> TrackPool;

        /// <summary>
        /// 初始化音频系统
        /// </summary>
        public static void Initialize(IAudioLoader customLoader = null)
        {
            KitObject = new GameObject(nameof(AudioKit));
            Object.DontDestroyOnLoad(KitObject);

            defaultLoader = customLoader ?? new ResourcesAudioLoader();

            players.Add(AudioType.Music, new SingleTrackPlayer());
            players.Add(AudioType.Voice, new MultiTrackPlayer());
            players.Add(AudioType.Sound, new MultiTrackPlayer());

            foreach (var player in players.Values)
            {
                player.Initialize(defaultLoader);
            }

            TrackPool ??= new ObjectPool<IAudioTrack>(() =>
            {
                var track = new GameObject(nameof(AudioTrack)).AddComponent<AudioTrack>();
                track.Parent(KitObject);
                return track;
            }, track =>
            {
                track.Transform.gameObject.SetActive(true);
            }, track =>
            {
                track.Transform.gameObject.SetActive(false);
                track.Transform.parent = KitObject.transform;
            });
        }

        /// <summary>
        /// API接口
        /// </summary>
        public static IAudioTrack Play(string clipPath, bool loop = false, AudioType type = AudioType.Sound, float volume = 1.0f)
        {
            if (players.TryGetValue(type, out var player))
            {
                return player.Play(clipPath, loop, volume);
            }
            else
            {
                Debug.LogError($"No player registered for audio type: {type}");
            }
            return default;
        }

        public static void StopAll(AudioType type)
        {
            if (players.TryGetValue(type, out var player))
            {
                player.StopAll();
            }
        }

        public static void PauseAll(AudioType type)
        {
            if (players.TryGetValue(type, out var player))
            {
                player.PauseAll();
            }
        }

        public static void ResumeAll(AudioType type)
        {
            if (players.TryGetValue(type, out var player))
            {
                player.ResumeAll();
            }
        }

        // 动态注册自定义播放器
        public static void RegisterPlayer(AudioType type, AudioPlayerBase player)
        {
            if (players.ContainsKey(type))
            {
                Debug.LogWarning($"Overriding existing player for type: {type}");
            }

            player.Initialize(defaultLoader);
            players[type] = player;
        }

        // 替换默认资源加载器
        public static void SetAudioLoader(IAudioLoader loader)
        {
            defaultLoader = loader;
            foreach (var player in players.Values)
            {
                player.Initialize(defaultLoader);
            }
        }
    }
}