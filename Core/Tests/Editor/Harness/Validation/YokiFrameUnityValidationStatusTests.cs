using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>验证 Unity 编译状态查询只反映当前公开事实。</summary>
    public sealed class YokiFrameUnityValidationStatusTests
    {
        /// <summary>验证空闲状态携带当前 FileBridge 会话身份。</summary>
        [Test]
        public void InspectReportsIdleCompilationWithCurrentIdentity()
        {
            var result = YokiFrameUnityValidationStatus.Inspect(
                CreateContext(),
                new FakeProvider(new YokiFrameUnityCompilationProbe()));

            Assert.AreEqual("Ready", result.status);
            Assert.AreEqual("Idle", result.compilation.state);
            Assert.IsFalse(result.compilation.isCompiling);
            Assert.IsFalse(result.compilation.scriptCompilationFailed);
            Assert.AreEqual("test-session", result.sessionId);
            Assert.AreEqual(17L, result.generation);
        }

        /// <summary>验证正在编译时不会推断任何历史编译结果。</summary>
        [Test]
        public void InspectReportsCurrentCompilationInProgress()
        {
            var result = YokiFrameUnityValidationStatus.Inspect(
                CreateContext(),
                new FakeProvider(new YokiFrameUnityCompilationProbe
                {
                    IsCompiling = true,
                    IsUpdating = true
                }));

            Assert.AreEqual("Compiling", result.compilation.state);
            Assert.IsTrue(result.compilation.isCompiling);
            Assert.IsTrue(result.compilation.isUpdating);
        }

        /// <summary>验证 Unity 当前失败标记会明确投影为 Failed。</summary>
        [Test]
        public void InspectReportsCurrentCompilationFailure()
        {
            var result = YokiFrameUnityValidationStatus.Inspect(
                CreateContext(),
                new FakeProvider(new YokiFrameUnityCompilationProbe
                {
                    ScriptCompilationFailed = true
                }));

            Assert.AreEqual("Failed", result.compilation.state);
            Assert.IsTrue(result.compilation.scriptCompilationFailed);
        }

        /// <summary>创建稳定测试会话身份。</summary>
        /// <returns>测试上下文。</returns>
        private static YokiFrameUnityHarnessContext CreateContext()
        {
            return new YokiFrameUnityHarnessContext
            {
                engineId = "unity-editor",
                mode = "EditMode",
                sessionId = "test-session",
                generation = 17L,
                sequence = 3L
            };
        }

        /// <summary>提供可控的编译状态事实。</summary>
        private sealed class FakeProvider : IYokiFrameUnityValidationProbeProvider
        {
            private readonly YokiFrameUnityCompilationProbe mCompilation;

            /// <summary>创建固定编译事实源。</summary>
            /// <param name="compilation">编译事实。</param>
            public FakeProvider(YokiFrameUnityCompilationProbe compilation)
            {
                mCompilation = compilation;
            }

            /// <summary>返回固定编译事实。</summary>
            /// <returns>编译事实。</returns>
            public YokiFrameUnityCompilationProbe ReadCompilation()
            {
                return mCompilation;
            }
        }
    }
}
