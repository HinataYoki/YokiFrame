#if UNITY_EDITOR && UNITY_2022_3_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace YokiFrame.Tests
{
    /// <summary>使用真实 Unity AudioClip 验证默认 ResKit 与自定义 Loader 播放链。</summary>
    public sealed class AudioKitUnityPlaybackTests
    {
        private const string AUDIO_PATH = "Audio/测试音频/战斗/击打";

        /// <summary>每条测试前恢复 AudioKit 默认资源加载器和宿主默认后端。</summary>
        [SetUp]
        public void SetUp()
        {
            AudioKit.Reset();
            AudioKit.ClearResourceLoader();
        }

        /// <summary>每条测试后停止播放并释放后端持有的资源租约。</summary>
        [TearDown]
        public void TearDown()
        {
            AudioKit.Reset();
        }

        /// <summary>验证默认 AudioSource 后端通过 ResKit 加载真实 Resources AudioClip 并进入播放状态。</summary>
        [UnityTest]
        public IEnumerator DefaultBackendPlaysRealClipThroughResKit()
        {
            Assert.AreEqual("ResKit", AudioKit.ResourceLoaderName);
            AudioVoiceHandle handle = AudioKit.PlaySfx(AUDIO_PATH);

            Assert.IsTrue(handle.IsValid, "默认后端未返回有效 voice handle。");
            Assert.AreEqual("Unity.AudioSource", AudioKit.BackendName);
            yield return null;

            AudioVoiceSnapshot voice = FindVoice(handle);
            Assert.IsNotNull(voice, "默认后端未保留 active voice。");
            Assert.IsTrue(voice.IsPlaying, "真实 AudioClip 未进入播放状态。");
            Assert.Greater(voice.Duration, 0f, "真实 AudioClip 时长无效。");
            Assert.AreEqual(AUDIO_PATH, voice.Path);
        }

        /// <summary>验证显式 Delegate Loader 替换 ResKit，并由创建资源的 Loader 接收释放。</summary>
        [UnityTest]
        public IEnumerator ExplicitLoaderPlaysAndReleasesRealClip()
        {
            var loadCount = 0;
            var releaseCount = 0;
            DelegateAudioResourceLoader loader = new(
                "PlayMode.Delegate",
                path =>
                {
                    loadCount++;
                    return Resources.Load<AudioClip>(path);
                },
                _ => releaseCount++);
            AudioKit.SetResourceLoader(loader);

            AudioVoiceHandle handle = AudioKit.PlaySfx(AUDIO_PATH);
            Assert.IsTrue(handle.IsValid, "自定义 Loader 未返回可播放 AudioClip。");
            Assert.AreEqual("PlayMode.Delegate", AudioKit.ResourceLoaderName);
            Assert.AreEqual(1, loadCount);
            yield return null;

            AudioVoiceSnapshot voice = FindVoice(handle);
            Assert.IsNotNull(voice, "自定义 Loader 播放后未保留 active voice。");
            Assert.IsTrue(voice.IsPlaying, "自定义 Loader 返回的 AudioClip 未播放。");
            AudioKit.Reset();
            Assert.AreEqual(1, releaseCount, "资源没有交还实际加载它的自定义 Loader。");
        }

        /// <summary>按 generation 与 voice ID 查找当前播放快照。</summary>
        private static AudioVoiceSnapshot FindVoice(AudioVoiceHandle handle)
        {
            List<AudioVoiceSnapshot> voices = new();
            AudioKit.GetActiveVoices(voices);
            for (var index = 0; index < voices.Count; index++)
            {
                AudioVoiceSnapshot voice = voices[index];
                if (voice.BackendGeneration == handle.BackendGeneration && voice.VoiceId == handle.VoiceId)
                {
                    return voice;
                }
            }

            return null;
        }
    }
}
#endif
