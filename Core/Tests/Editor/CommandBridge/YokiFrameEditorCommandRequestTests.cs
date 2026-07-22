using System;
using System.Reflection;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity Editor FileBridge pump 创建 Runtime 命令请求时保留策略证据。
    /// </summary>
    public sealed class YokiFrameEditorCommandRequestTests
    {
        /// <summary>
        /// 验证 Editor pump 会把真实命令文件大小传入 Runtime policy 请求，避免大小证据在分发层丢失。
        /// </summary>
        [Test]
        public void CreateCommandRequestCarriesCommandFileBytes()
        {
            const long COMMAND_FILE_BYTES = 4096L;
            MethodInfo createRequest = GetCreateCommandRequestMethod();
            YokiFrameEditorCommandEnvelope envelope = CreateEnvelope();

            YokiFrameCommandRequest request = (YokiFrameCommandRequest)createRequest.Invoke(
                null,
                new object[] { envelope, COMMAND_FILE_BYTES });

            Assert.AreEqual(COMMAND_FILE_BYTES, request.CommandFileBytes);
            Assert.AreEqual("cli", request.Source);
            Assert.AreEqual("System", request.Kit);
            Assert.AreEqual("ping", request.Action);
        }

        /// <summary>
        /// 定位携带文件大小参数的私有创建方法，确保测试锁定真实转换入口。
        /// </summary>
        /// <returns>命令请求创建方法。</returns>
        private static MethodInfo GetCreateCommandRequestMethod()
        {
            MethodInfo method = typeof(YokiFrameEditorFileBridgePump).GetMethod(
                "CreateCommandRequest",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(YokiFrameEditorCommandEnvelope), typeof(long) },
                null);

            Assert.IsNotNull(method);
            return method;
        }

        /// <summary>
        /// 创建最小合法命令信封，用于验证转换结果。
        /// </summary>
        /// <returns>测试命令信封。</returns>
        private static YokiFrameEditorCommandEnvelope CreateEnvelope()
        {
            return new YokiFrameEditorCommandEnvelope
            {
                source = "cli",
                kit = "System",
                action = "ping",
                payloadJson = "{}",
                timeoutMs = YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS
            };
        }
    }
}
