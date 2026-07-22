using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>验证 Unity Console Error 证据的完整性、错误计数和明细裁剪语义。</summary>
    public sealed class YokiFrameUnityConsoleErrorsTests
    {
        /// <summary>验证完整扫描且零 Error 时可以形成 Ready 的零错误证据。</summary>
        [Test]
        public void InspectCompleteScanCanProveZeroErrors()
        {
            var result = Inspect(
                10,
                new YokiFrameUnityConsoleProbe
                {
                    TotalEntryCount = 1,
                    ScanComplete = true,
                    Entries = new[]
                    {
                        new YokiFrameUnityConsoleEntryFact { Index = 0, IsError = false, Message = "log" }
                    }
                });

            Assert.AreEqual("Ready", result.status);
            Assert.IsTrue(result.scanComplete);
            Assert.AreEqual(0, result.errorCount);
            Assert.IsFalse(result.truncated);
        }

        /// <summary>验证扫描会统计全部 Error，但只返回最后的固定数量明细。</summary>
        [Test]
        public void InspectRetainsLastBoundedErrorDetails()
        {
            var result = Inspect(
                2,
                new YokiFrameUnityConsoleProbe
                {
                    TotalEntryCount = 3,
                    ScanComplete = true,
                    Entries = new[]
                    {
                        Error(0, "first"),
                        Error(1, "second"),
                        Error(2, "third")
                    }
                });

            Assert.AreEqual(3, result.errorCount);
            Assert.AreEqual(2, result.returnedCount);
            Assert.IsTrue(result.truncated);
            Assert.AreEqual(1, result.errors[0].index);
            Assert.AreEqual("third", result.errors[1].message);
        }

        /// <summary>验证 Console 超过扫描上限时，即使窗口内零 Error 也不能宣称全局通过。</summary>
        [Test]
        public void InspectPartialScanCannotProveGlobalZeroErrors()
        {
            var result = Inspect(
                10,
                new YokiFrameUnityConsoleProbe
                {
                    TotalEntryCount = 5000,
                    ScanComplete = false,
                    Entries = new YokiFrameUnityConsoleEntryFact[4096]
                });

            Assert.AreEqual("Partial", result.status);
            Assert.IsFalse(result.scanComplete);
            Assert.AreEqual(0, result.errorCount);
            Assert.IsTrue(result.truncated);
        }

        /// <summary>使用固定事实源执行 Console Error 观察。</summary>
        /// <param name="maxCount">返回明细上限。</param>
        /// <param name="probe">固定 Console 事实。</param>
        /// <returns>观察结果。</returns>
        private static YokiFrameUnityConsoleErrorObservation Inspect(
            int maxCount,
            YokiFrameUnityConsoleProbe probe)
        {
            return YokiFrameUnityConsoleErrors.Inspect(
                new YokiFrameUnityHarnessContext
                {
                    engineId = "unity-editor",
                    mode = "EditMode",
                    sessionId = "test-session",
                    generation = 41L
                },
                maxCount,
                new FakeProvider(probe));
        }

        /// <summary>创建一条 Error 事实。</summary>
        /// <param name="index">Console 索引。</param>
        /// <param name="message">错误消息。</param>
        /// <returns>Error 事实。</returns>
        private static YokiFrameUnityConsoleEntryFact Error(int index, string message)
        {
            return new YokiFrameUnityConsoleEntryFact
            {
                Index = index,
                IsError = true,
                Message = message
            };
        }

        /// <summary>返回固定 Console 扫描事实。</summary>
        private sealed class FakeProvider : IYokiFrameUnityConsoleProbeProvider
        {
            private readonly YokiFrameUnityConsoleProbe mProbe;

            /// <summary>创建固定事实源。</summary>
            /// <param name="probe">待返回事实。</param>
            public FakeProvider(YokiFrameUnityConsoleProbe probe)
            {
                mProbe = probe;
            }

            /// <summary>返回固定事实，扫描上限由生产 provider 自行负责。</summary>
            /// <param name="_">协议扫描上限。</param>
            /// <returns>固定事实。</returns>
            public YokiFrameUnityConsoleProbe Read(int _)
            {
                return mProbe;
            }
        }
    }
}
