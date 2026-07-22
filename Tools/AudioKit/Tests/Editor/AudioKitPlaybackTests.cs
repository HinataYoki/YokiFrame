using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>验证 AudioKit 播放参数规范化不破坏合法业务值。</summary>
    public sealed class AudioKitPlaybackTests
    {
        private FakeAudioBackend mBackend;

        /// <summary>每个测试安装新的可观察后端。</summary>
        [SetUp]
        public void SetUp()
        {
            AudioKit.ResetRuntimeDefaults();
            mBackend = new FakeAudioBackend();
            AudioKit.SetBackend(mBackend);
        }

        /// <summary>每个测试后释放静态后端。</summary>
        [TearDown]
        public void TearDown() => AudioKit.ResetRuntimeDefaults();

        /// <summary>零音量是合法播放参数，不得被旧版默认值修正改成一。</summary>
        [Test]
        public void ZeroVolumeIsPreserved()
        {
            AudioPlayOptions options = AudioPlayOptions.Default;
            options.Volume = 0f;

            AudioKit.Play("sfx/silent", options);

            Assert.AreEqual(0f, mBackend.LastOptions.Volume);
        }

        /// <summary>宿主统一帧派发器必须推进当前后端。</summary>
        [Test]
        public void FrameDispatcherUpdatesInstalledBackend()
        {
            AudioKit.PlaySfx("sfx/click");

            YokiFrameUpdateDispatcher.Tick(0.25f, 0.5f);

            Assert.AreEqual(1, mBackend.UpdateCount);
            Assert.AreEqual(0.25f, mBackend.LastDeltaTime);
        }
    }
}
