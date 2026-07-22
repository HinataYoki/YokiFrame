#if UNITY_EDITOR && UNITY_2022_3_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace YokiFrame.Tests
{
    /// <summary>仅在验证请求存在时启动默认 AudioKit 真实播放 smoke。</summary>
    internal static class AudioKitUnityPlaybackSmoke
    {
        private const string REQUEST_PATH = ".yokiframe/test-runs/audiokit-playback/request.txt";

        /// <summary>场景加载后检查一次验证请求，日常 Play Mode 不创建任何对象。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWhenRequested()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string requestPath = Path.Combine(projectRoot, REQUEST_PATH);
            if (!File.Exists(requestPath)
                || !string.Equals(File.ReadAllText(requestPath).Trim(), "default", StringComparison.Ordinal)) return;
            var runner = new GameObject("AudioKit Unity Playback Smoke");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<AudioKitUnityPlaybackSmokeRunner>();
        }
    }

    /// <summary>在真实 Play Mode 中验证 ResKit 和自定义 Loader 的 AudioSource 播放状态。</summary>
    internal sealed class AudioKitUnityPlaybackSmokeRunner : MonoBehaviour
    {
        private const string AUDIO_PATH = "Audio/测试音频/战斗/击打";
        private const string RESULT_PATH = ".yokiframe/test-runs/audiokit-playback/default.txt";

        private sealed class SmokeState
        {
            internal AudioVoiceHandle DefaultHandle;
            internal AudioVoiceHandle CustomHandle;
            internal int LoadCount;
            internal int ReleaseCount;
        }

        /// <summary>跨两帧执行播放检查并始终写入可审计结果。</summary>
        private IEnumerator Start()
        {
            yield return null;
            var lines = new List<string>();
            var state = new SmokeState();
            Exception failure = TryRun(() => BeginDefaultLoader(state));
            yield return null;
            if (failure == null) failure = TryRun(() => FinishDefaultAndBeginCustom(state, lines));
            yield return null;
            if (failure == null) failure = TryRun(() => FinishCustomLoader(state, lines));
            lines.Insert(0, failure == null ? "status=passed" : "status=failed");
            if (failure != null) lines.Add("exception=" + Sanitize(failure.ToString()));
            AudioKit.Reset();
            WriteResult(lines);
            Destroy(gameObject);
        }

        /// <summary>启动 ResKit 默认 Loader 的真实 AudioClip 播放。</summary>
        private static void BeginDefaultLoader(SmokeState state)
        {
            AudioKit.Reset();
            AudioKit.ClearResourceLoader();
            state.DefaultHandle = AudioKit.PlaySfx(AUDIO_PATH);
            Ensure(state.DefaultHandle.IsValid, "默认后端未返回有效 voice handle。");
        }

        /// <summary>验证默认 voice 状态并启动显式自定义 Loader 播放。</summary>
        private static void FinishDefaultAndBeginCustom(SmokeState state, List<string> lines)
        {
            AudioVoiceSnapshot voice = FindVoice(state.DefaultHandle);
            Ensure(voice != null && voice.IsPlaying, "默认后端真实 AudioClip 未进入播放状态。");
            Ensure(voice.Duration > 0f, "默认后端真实 AudioClip 时长无效。");
            lines.Add("defaultBackend=" + AudioKit.BackendName);
            lines.Add("defaultLoader=" + AudioKit.ResourceLoaderName);
            lines.Add("resKitProvider=" + ResKit.ProviderName);
            lines.Add("defaultVoicePlaying=true");
            lines.Add("defaultDuration=" + voice.Duration.ToString("R", CultureInfo.InvariantCulture));
            AudioKit.Reset();
            AudioKit.SetResourceLoader(new DelegateAudioResourceLoader(
                "PlayMode.Delegate",
                path =>
                {
                    state.LoadCount++;
                    return Resources.Load<AudioClip>(path);
                },
                _ => state.ReleaseCount++));
            state.CustomHandle = AudioKit.PlaySfx(AUDIO_PATH);
            Ensure(state.CustomHandle.IsValid, "自定义 Loader 未返回有效 voice handle。");
        }

        /// <summary>验证自定义 voice 状态以及资源加载和释放所有权。</summary>
        private static void FinishCustomLoader(SmokeState state, List<string> lines)
        {
            AudioVoiceSnapshot voice = FindVoice(state.CustomHandle);
            Ensure(voice != null && voice.IsPlaying, "自定义 Loader 的真实 AudioClip 未进入播放状态。");
            AudioKit.Reset();
            Ensure(state.LoadCount == 1 && state.ReleaseCount == 1, "自定义 Loader 加载或释放次数不正确。");
            lines.Add("customLoader=PlayMode.Delegate");
            lines.Add("customVoicePlaying=true");
            lines.Add("customLoadCount=" + state.LoadCount);
            lines.Add("customReleaseCount=" + state.ReleaseCount);
        }

        /// <summary>按 generation 与 voice ID 查找当前 voice 快照。</summary>
        private static AudioVoiceSnapshot FindVoice(AudioVoiceHandle handle)
        {
            List<AudioVoiceSnapshot> voices = new();
            AudioKit.GetActiveVoices(voices);
            for (var index = 0; index < voices.Count; index++)
            {
                AudioVoiceSnapshot voice = voices[index];
                if (voice.BackendGeneration == handle.BackendGeneration && voice.VoiceId == handle.VoiceId) return voice;
            }

            return null;
        }

        /// <summary>条件不成立时抛出带业务语义的验证异常。</summary>
        private static void Ensure(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        /// <summary>执行单个无跨帧阶段并把异常转换为验证结果。</summary>
        private static Exception TryRun(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        /// <summary>把验证结果写入项目内测试产物目录。</summary>
        private static void WriteResult(IReadOnlyList<string> lines)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputPath = Path.Combine(projectRoot, RESULT_PATH);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllLines(outputPath, lines);
        }

        /// <summary>把异常正文压缩为单行证据值。</summary>
        private static string Sanitize(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
    }
}
#endif
