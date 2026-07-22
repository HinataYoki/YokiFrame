using System;
using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity Editor Named Pipe FastChannel 在真实 Pipe 上完成握手、主线程队列分发与只读命令响应。
    /// </summary>
    public sealed class YokiFrameFastChannelAdapterIntegrationTests
    {
        private const int CONNECT_TIMEOUT_MS = 5000;
        private const int EXCHANGE_TIMEOUT_MS = 5000;
        private const int PUMP_INTERVAL_MS = 10;

        /// <summary>
        /// 验证当前 Unity FastChannel endpoint 能在同一受保护 Named Pipe 上完成 Hello 与 System/ping 往返。
        /// </summary>
        [Test]
        public void WindowsEditorFastChannelCompletesHelloAndPing()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("当前不是 Windows Unity Editor，不执行 Named Pipe FastChannel 往返测试。");
            }

            var context = ReadFastChannelContext();
            var responseTask = ExchangeFramesAsync(context.PipeName, CreateHelloAndPingFrames(context));
            var responses = PumpUntilResponsesComplete(context.PumpType, responseTask);

            AssertHelloAndPingResponses(responses);
        }

        /// <summary>
        /// 验证未发送首帧的客户端不会永久占住唯一 Pipe server，后续合法客户端仍可完成完整的 Hello 与 ping 往返。
        /// </summary>
        [Test]
        public void WindowsEditorFastChannelReleasesSilentClientBeforeServingNextClient()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("当前不是 Windows Unity Editor，不执行静默 Named Pipe 客户端超时测试。");
            }

            var context = ReadFastChannelContext();
            using (var silentClient = new NamedPipeClientStream(".", context.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                silentClient.Connect(CONNECT_TIMEOUT_MS);
                var responseTask = ExchangeFramesAsync(context.PipeName, CreateHelloAndPingFrames(context));
                var responses = PumpUntilResponsesComplete(context.PumpType, responseTask);

                AssertHelloAndPingResponses(responses);
            }
        }

        /// <summary>
        /// 断言同一连接上的 HelloAck 与 ping Response 均为当前 FastChannel 的成功终态。
        /// </summary>
        /// <param name="responses">Client 按请求顺序收到的两个终态响应。</param>
        private static void AssertHelloAndPingResponses(YokiFrameFastChannelFrame[] responses)
        {
            Assert.AreEqual(2, responses.Length);
            Assert.AreEqual(YokiFrameFastChannelMessageKind.HelloAck, responses[0].MessageKind);
            Assert.AreEqual(
                YokiFrameFastChannelMessageKind.Response,
                responses[1].MessageKind,
                responses[1].PayloadJson);
            StringAssert.Contains("\"status\":\"Success\"", responses[1].PayloadJson);
            StringAssert.Contains("pong", responses[1].PayloadJson);
        }

        /// <summary>
        /// 从已初始化的 Unity Editor pump 读取活跃 Pipe 与握手身份，避免测试依赖测试专用 listener。
        /// </summary>
        /// <returns>当前运行中 FastChannel 的连接与身份上下文。</returns>
        private static FastChannelContext ReadFastChannelContext()
        {
            var assembly = Assembly.Load("YokiFrame.Unity.Editor");
            var pumpType = assembly.GetType("YokiFrame.YokiFrameEditorFileBridgePump");
            Assert.IsNotNull(pumpType, "YokiFrame Unity Editor pump 类型不存在。");

            var hostField = pumpType.GetField("sFastChannelHost", BindingFlags.Static | BindingFlags.NonPublic);
            object host = hostField.GetValue(null);
            Assert.IsNotNull(host, "Unity FastChannel host 未创建。");

            var hostType = host.GetType();
            bool isReady = (bool)hostType.GetProperty("IsReady").GetValue(host, null);
            Assert.IsTrue(isReady, "Unity FastChannel host 未 ready。");

            return new FastChannelContext
            {
                PumpType = pumpType,
                PipeName = (string)hostType.GetProperty("PipeName").GetValue(host, null),
                SessionId = (string)pumpType.GetField("sSessionId", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null),
                Generation = (long)pumpType.GetField("sGeneration", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)
            };
        }

        /// <summary>
        /// 创建与当前 Unity session 严格匹配的 Hello 和只读 System/ping 命令，覆盖同一连接的两阶段协议状态。
        /// </summary>
        /// <param name="context">当前 FastChannel session 与 generation 信息。</param>
        /// <returns>按连接发送顺序排列的 Hello 和 Command frame。</returns>
        private static YokiFrameFastChannelFrame[] CreateHelloAndPingFrames(FastChannelContext context)
        {
            var helloJson = "{\"engineId\":\"unity-editor\",\"sessionId\":\"" + context.SessionId
                + "\",\"generation\":" + context.Generation.ToString(CultureInfo.InvariantCulture) + "}";
            var requestId = "fastchannelping" + Guid.NewGuid().ToString("N");
            var commandJson = "{\"protocolVersion\":" + YokiFrameFileBridgeContract.PROTOCOL_VERSION
                + ",\"engineId\":\"unity-editor\",\"source\":\"codex\",\"createdAtUtc\":\""
                + DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                + "\",\"requestId\":\"" + requestId
                + "\",\"kit\":\"System\",\"action\":\"ping\",\"payloadJson\":\"{}\",\"timeoutMs\":1000}";
            return new[]
            {
                new YokiFrameFastChannelFrame(YokiFrameFastChannelMessageKind.Hello, 0, helloJson),
                new YokiFrameFastChannelFrame(YokiFrameFastChannelMessageKind.Command, 0, commandJson)
            };
        }

        /// <summary>
        /// 在后台连接 Named Pipe 并按顺序写入请求、读取响应，避免在 Unity 主线程阻塞连接或读操作。
        /// </summary>
        /// <param name="pipeName">当前 session 专属 Pipe 名称。</param>
        /// <param name="requests">同一连接上需要发送的 frame。</param>
        /// <returns>每个请求对应的终态响应 frame。</returns>
        private static Task<YokiFrameFastChannelFrame[]> ExchangeFramesAsync(
            string pipeName,
            YokiFrameFastChannelFrame[] requests)
        {
            return Task.Run(async () =>
            {
                using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
                {
                    client.Connect(CONNECT_TIMEOUT_MS);
                    var responses = new YokiFrameFastChannelFrame[requests.Length];
                    for (var index = 0; index < requests.Length; index++)
                    {
                        await YokiFrameFastChannelFrameStream.WriteAsync(client, requests[index], CancellationToken.None);
                        responses[index] = await YokiFrameFastChannelFrameStream.ReadAsync(client, CancellationToken.None);
                    }

                    return responses;
                }
            });
        }

        /// <summary>
        /// 在 Unity 测试主线程主动 drain FastChannel 请求队列，直到后台 Client 收齐响应或达到超时。
        /// </summary>
        /// <param name="pumpType">承载私有主线程 drain 方法的 Unity pump 类型。</param>
        /// <param name="responseTask">等待 Hello 和 Command response 的后台 Client 任务。</param>
        /// <returns>后台 Client 收到的响应数组。</returns>
        private static YokiFrameFastChannelFrame[] PumpUntilResponsesComplete(
            Type pumpType,
            Task<YokiFrameFastChannelFrame[]> responseTask)
        {
            var processMethod = pumpType.GetMethod("ProcessFastChannelRequestsSafely", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(processMethod, "Unity pump 缺少 FastChannel 主线程 drain 方法。");

            var deadlineUtc = DateTime.UtcNow.AddMilliseconds(EXCHANGE_TIMEOUT_MS);
            while (!responseTask.IsCompleted && DateTime.UtcNow < deadlineUtc)
            {
                processMethod.Invoke(null, null);
                Thread.Sleep(PUMP_INTERVAL_MS);
            }

            Assert.IsTrue(responseTask.IsCompleted, "FastChannel Hello/ping 往返超时。");
            Assert.IsFalse(responseTask.IsFaulted, "FastChannel Client 连接或读取失败: " + responseTask.Exception);
            return responseTask.GetAwaiter().GetResult();
        }

        /// <summary>
        /// 收纳当前 Unity FastChannel endpoint 的反射访问结果，避免测试在多次读取静态状态时混入代际变化。
        /// </summary>
        private sealed class FastChannelContext
        {
            public Type PumpType { get; set; }
            public string PipeName { get; set; }
            public string SessionId { get; set; }
            public long Generation { get; set; }
        }
    }
}
