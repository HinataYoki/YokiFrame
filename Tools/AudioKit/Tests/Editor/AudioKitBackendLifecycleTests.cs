using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEngine.TestTools;
#endif

namespace YokiFrame.Tests
{
    /// <summary>验证 AudioKit 默认后端、显式后端与 voice 代次的核心生命周期。</summary>
    public sealed class AudioKitBackendLifecycleTests
    {
        /// <summary>每个测试前移除上一用例的静态后端和默认工厂。</summary>
        [SetUp]
        public void SetUp() => AudioKit.ResetRuntimeDefaults();

        /// <summary>每个测试后释放仍由 AudioKit 持有的后端。</summary>
        [TearDown]
        public void TearDown() => AudioKit.ResetRuntimeDefaults();

        /// <summary>状态读取和音量配置不得创建默认后端，首次播放只能创建一次。</summary>
        [Test]
        public void DefaultBackendIsCreatedOnlyByFirstPlayback()
        {
            var createCount = 0;
            AudioKit.RegisterDefaultBackendFactory(() =>
            {
                createCount++;
                return new FakeAudioBackend();
            });

            Assert.AreEqual("None", AudioKit.BackendName);
            AudioKit.SetBusVolume(AudioBus.Music, 0.4f);
            Assert.AreEqual(0, createCount);

            AudioVoiceHandle first = AudioKit.PlayMusic("music/theme");
            AudioVoiceHandle second = AudioKit.PlaySfx("sfx/click");

            Assert.IsTrue(first.IsValid);
            Assert.IsTrue(second.IsValid);
            Assert.AreEqual(1, createCount);
        }

        /// <summary>非法预加载路径必须在创建默认后端之前被拒绝。</summary>
        [Test]
        public void PreloadRejectsInvalidPathBeforeCreatingDefaultBackend()
        {
            Assert.Throws<ArgumentException>(() => AudioKit.Preload("  "));
            Assert.IsFalse(AudioKit.HasBackend);
        }

        /// <summary>提前设置的项目后端必须跳过宿主默认工厂。</summary>
        [Test]
        public void ExplicitBackendSkipsDefaultFactory()
        {
            var createCount = 0;
            FakeAudioBackend explicitBackend = new();
            AudioKit.RegisterDefaultBackendFactory(() =>
            {
                createCount++;
                return new FakeAudioBackend();
            });
            AudioKit.SetBackend(explicitBackend);

            AudioVoiceHandle handle = AudioKit.PlaySfx("sfx/click");

            Assert.IsTrue(handle.IsValid);
            Assert.AreEqual(0, createCount);
            Assert.AreEqual(1, explicitBackend.PlayCount);
        }

        /// <summary>后端替换后旧代次 handle 不得停止新后端的同编号 voice。</summary>
        [Test]
        public void StaleHandleCannotStopReplacementBackendVoice()
        {
            FakeAudioBackend firstBackend = new();
            AudioKit.SetBackend(firstBackend);
            AudioVoiceHandle stale = AudioKit.PlaySfx("sfx/first");
            FakeAudioBackend secondBackend = new();
            AudioKit.SetBackend(secondBackend);
            AudioVoiceHandle current = AudioKit.PlaySfx("sfx/second");

            bool staleStopped = AudioKit.Stop(stale);
            bool currentStopped = AudioKit.Stop(current);

            Assert.IsFalse(staleStopped);
            Assert.IsTrue(currentStopped);
            Assert.IsTrue(firstBackend.Disposed);
        }

        /// <summary>同步播放期间若后端被替换，旧后端 voice 不得伪装为新代次有效句柄。</summary>
        [Test]
        public void PlayDoesNotBindHandleAfterBackendReplacement()
        {
            FakeAudioBackend replacement = new();
            AudioKit.SetBackend(new ReplacingFakeAudioBackend(replacement));

            AudioVoiceHandle stale = AudioKit.PlaySfx("sfx/replaced-during-play");
            AudioVoiceHandle current = AudioKit.PlaySfx("sfx/current");

            Assert.IsFalse(stale.IsValid);
            Assert.IsTrue(current.IsValid);
            Assert.IsFalse(AudioKit.Stop(stale));
            Assert.IsTrue(AudioKit.Stop(current));
        }

        /// <summary>显式注册必须同步已存在后端，并在会话 Reset 后清空声明。</summary>
        [Test]
        public void RegisteredBusSynchronizesBackendAndResetClearsCatalog()
        {
            FakeAudioBackend backend = new();
            AudioKit.SetBackend(backend);

            bool registered = AudioKit.RegisterBus("DialogueNpc");

            Assert.IsTrue(registered);
            Assert.IsTrue(backend.HasBus("DialogueNpc"));
            Assert.IsTrue(AudioKit.IsBusRegistered("dialoguenpc"));
            AudioKit.Reset();
            Assert.IsFalse(AudioKit.IsBusRegistered("DialogueNpc"));
        }

        /// <summary>宿主自然结束等非门面状态变化必须推进 Tools 状态版本。</summary>
        [Test]
        public void BackendDiagnosticNotificationAdvancesStateVersion()
        {
            long before = AudioKit.DiagnosticVersion;

            AudioKit.NotifyBackendDiagnosticStateChanged();

            Assert.Greater(AudioKit.DiagnosticVersion, before);
        }

        /// <summary>PlayAsync 完成前若后端被替换，不得用新代次包装旧 voiceId，并应清理孤儿 voice。</summary>
        [Test]
        public void PlayAsyncDoesNotBindHandleAfterBackendReplacement()
        {
            var firstBackend = new DelayedFakeAudioBackend();
            AudioKit.SetBackend(firstBackend);
            System.Threading.Tasks.Task<AudioVoiceHandle> pending =
                AudioKit.PlayAsync("sfx/async", AudioPlayOptions.Default);

            // 异步播放尚未完成时替换后端。
            Assert.IsTrue(firstBackend.WaitUntilPlayAsyncStarted(1000));
            var secondBackend = new FakeAudioBackend();
            AudioKit.SetBackend(secondBackend);
            firstBackend.CompletePendingPlay();

            AudioVoiceHandle handle = pending.GetAwaiter().GetResult();
            Assert.IsFalse(handle.IsValid);
            Assert.IsTrue(firstBackend.LastStoppedVoiceId > 0);
            Assert.IsFalse(AudioKit.Stop(handle));
        }

        /// <summary>后端在返回已创建 voice 后才收到取消时，门面必须回收该 voice 并把取消传给调用方。</summary>
        [Test]
        public void PlayAsyncCancellationAfterVoiceCreationStopsOrphanVoice()
        {
            using CancellationTokenSource cancellation = new();
            CancellingAfterStartBackend backend = new(cancellation);
            AudioKit.SetBackend(backend);

            Task<AudioVoiceHandle> pending = AudioKit.PlayAsync(
                "sfx/cancel-after-start",
                AudioPlayOptions.Default,
                cancellation.Token);

            Assert.Throws<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            Assert.AreEqual(1, backend.StopCount);
            Assert.IsEmpty(backend.ActiveVoiceIds);
        }

        /// <summary>后端某一清理阶段失败时仍必须继续卸载资源并调用 Dispose。</summary>
        [Test]
        public void BackendCleanupContinuesAfterStopFailure()
        {
            CleanupFailureBackend backend = new();
            AudioKit.SetBackend(backend);

#if UNITY_EDITOR
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                AudioKit.ClearBackend();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
#else
            AudioKit.ClearBackend();
#endif

            Assert.IsTrue(backend.UnloadAllCalled);
            Assert.IsTrue(backend.DisposeCalled);
            Assert.IsTrue(backend.Disposed);
        }

        /// <summary>后端回调重入替换时必须延迟释放当前实例，直到回调返回。</summary>
        [Test]
        public void BackendReplacementFromSyncCallbackIsDeferred()
        {
            FakeAudioBackend replacement = new();
            ReentrantReplacementBackend first = new(replacement);

            AudioKit.SetBackend(first);

            Assert.IsFalse(first.DisposedDuringCallback);
            Assert.IsTrue(first.Disposed);
            Assert.AreSame(replacement, AudioKit.GetBackend());
        }

        /// <summary>延迟完成 PlayAsync 的测试后端，用于验证代次竞态。</summary>
        private sealed class DelayedFakeAudioBackend : FakeAudioBackend
        {
            private readonly System.Threading.Tasks.TaskCompletionSource<int> mGate =
                new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly System.Threading.ManualResetEventSlim mStarted = new(false);
            private int mPendingVoiceId;

            public int LastStoppedVoiceId { get; private set; }

            public override System.Threading.Tasks.Task<int> PlayAsync(
                string path,
                AudioPlayOptions options,
                System.Threading.CancellationToken token)
            {
                mPendingVoiceId = Play(path, options);
                mStarted.Set();
                return WaitForGate(token);
            }

            public override bool Stop(int voiceId)
            {
                LastStoppedVoiceId = voiceId;
                return base.Stop(voiceId);
            }

            public bool WaitUntilPlayAsyncStarted(int millisecondsTimeout) =>
                mStarted.Wait(millisecondsTimeout);

            public void CompletePendingPlay() => mGate.TrySetResult(mPendingVoiceId);

            private async System.Threading.Tasks.Task<int> WaitForGate(System.Threading.CancellationToken token)
            {
                using (token.Register(() => mGate.TrySetCanceled(token)))
                {
                    return await mGate.Task.ConfigureAwait(false);
                }
            }
        }

        /// <summary>在同步 Play 返回前替换门面后端，用于固定 generation 竞态。</summary>
        private sealed class ReplacingFakeAudioBackend : FakeAudioBackend
        {
            private readonly IAudioBackend mReplacement;
            private bool mHasReplaced;

            /// <summary>创建只在第一次播放期间替换 AudioKit 后端的测试替身。</summary>
            internal ReplacingFakeAudioBackend(IAudioBackend replacement)
            {
                mReplacement = replacement;
            }

            /// <summary>先创建旧后端 voice，再在返回局部 ID 前完成后端替换。</summary>
            public override int Play(string path, AudioPlayOptions options)
            {
                int voiceId = base.Play(path, options);
                if (!mHasReplaced)
                {
                    mHasReplaced = true;
                    AudioKit.SetBackend(mReplacement);
                }

                return voiceId;
            }
        }

        /// <summary>在创建 voice 后取消令牌，用于固定门面取消清理竞态。</summary>
        private sealed class CancellingAfterStartBackend : FakeAudioBackend
        {
            private readonly CancellationTokenSource mCancellation;

            /// <summary>创建会在首次异步播放完成前触发取消的测试后端。</summary>
            internal CancellingAfterStartBackend(CancellationTokenSource cancellation)
            {
                mCancellation = cancellation;
            }

            /// <summary>记录停止次数，验证门面确实回收了不可见 voice。</summary>
            internal int StopCount { get; private set; }

            /// <summary>读取当前仍在后端中的 voice 数量，验证停止没有遗漏。</summary>
            internal System.Collections.Generic.IReadOnlyCollection<int> ActiveVoiceIds =>
                GetActiveVoiceIds();

            /// <summary>先创建 voice，再取消令牌并返回其局部 ID。</summary>
            public override Task<int> PlayAsync(string path, AudioPlayOptions options, CancellationToken token)
            {
                int voiceId = Play(path, options);
                mCancellation.Cancel();
                return Task.FromResult(voiceId);
            }

            /// <summary>记录门面回收并委托基础替身移除 voice。</summary>
            public override bool Stop(int voiceId)
            {
                StopCount++;
                return base.Stop(voiceId);
            }

            /// <summary>从诊断快照提取当前 active voice ID，避免测试替身暴露内部集合。</summary>
            private System.Collections.Generic.IReadOnlyCollection<int> GetActiveVoiceIds()
            {
                var voices = new System.Collections.Generic.List<AudioVoiceSnapshot>();
#if UNITY_EDITOR || (GODOT && TOOLS)
                GetActiveVoices(voices);
#endif
                var ids = new System.Collections.Generic.List<int>(voices.Count);
                for (var index = 0; index < voices.Count; index++) ids.Add(voices[index].VoiceId);
                return ids;
            }
        }

        /// <summary>让 StopAll 失败但记录后续清理阶段的测试后端。</summary>
        private sealed class CleanupFailureBackend : FakeAudioBackend
        {
            /// <summary>获取 UnloadAll 是否已执行。</summary>
            internal bool UnloadAllCalled { get; private set; }

            /// <summary>获取 Dispose 是否已执行。</summary>
            internal bool DisposeCalled { get; private set; }

            /// <summary>故意抛出异常验证门面继续清理。</summary>
            public override void StopAll() => throw new InvalidOperationException("stop failure");

            /// <summary>记录资源卸载阶段并保持测试替身状态。</summary>
            public override void UnloadAll() => UnloadAllCalled = true;

            /// <summary>记录最终释放阶段并委托基础替身标记状态。</summary>
            public override void Dispose()
            {
                DisposeCalled = true;
                base.Dispose();
            }
        }

        /// <summary>在首次总线同步回调中请求替换，用于验证重入转换队列。</summary>
        private sealed class ReentrantReplacementBackend : FakeAudioBackend
        {
            private readonly IAudioBackend mReplacement;
            private bool mRequested;
            private bool mInsideCallback;

            /// <summary>创建会在同步总线状态期间请求后端替换的测试替身。</summary>
            internal ReentrantReplacementBackend(IAudioBackend replacement)
            {
                mReplacement = replacement;
            }

            /// <summary>获取是否曾在总线回调仍执行时被错误释放。</summary>
            internal bool DisposedDuringCallback { get; private set; }

            /// <summary>重入请求替换后继续完成当前回调，替换应由门面延迟处理。</summary>
            public override void SetBusVolume(string bus, float volume)
            {
                mInsideCallback = true;
                try
                {
                    if (!mRequested)
                    {
                        mRequested = true;
                        AudioKit.SetBackend(mReplacement);
                    }

                    base.SetBusVolume(bus, volume);
                }
                finally
                {
                    mInsideCallback = false;
                }
            }

            /// <summary>记录若门面在回调栈内提前释放当前实例。</summary>
            public override void Dispose()
            {
                if (mInsideCallback) DisposedDuringCallback = true;
                base.Dispose();
            }
        }
    }
}
