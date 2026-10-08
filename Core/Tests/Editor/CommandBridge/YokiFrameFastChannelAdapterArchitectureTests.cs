using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using JsonDocument = YokiFrame.Json.JsonDocument;
using JsonElement = YokiFrame.Json.JsonElement;
using JsonValueKind = YokiFrame.Json.JsonValueKind;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity FastChannel 的可观察身份、Pipe 名称、注册表 endpoint 和 Windows listener。
    /// </summary>
    public sealed class YokiFrameFastChannelAdapterArchitectureTests
    {
        private const string NAMED_PIPE_HOST_TYPE_NAME = "YokiFrame.YokiFrameEditorNamedPipeFastChannelHost";
        private const string PIPE_NAME_PREFIX = "YokiFrame.FastChannel.unity-editor.";
        private const string NAMED_PIPE_TRANSPORT = "namedPipe";
        private const string DISABLED_TRANSPORT = "none";
        private const string FILE_BRIDGE_FALLBACK = "filebridge";

        /// <summary>
        /// 验证 AssetImportWorker 不拥有项目 FileBridge，普通 Editor 进程拥有。
        /// </summary>
        [Test]
        public void ShouldOwnBridge_AssetImportWorker_不拥有项目FileBridge()
        {
            Assert.IsTrue(YokiFrameEditorFileBridgePump.ShouldOwnBridge(false), "普通 Editor 进程必须拥有 FileBridge。");
            Assert.IsFalse(YokiFrameEditorFileBridgePump.ShouldOwnBridge(true), "AssetImportWorker 禁止拥有项目 FileBridge。");
        }

        /// <summary>
        /// 验证已发布的 registry 与 heartbeat 使用当前主 Editor Pump 的 session 和 generation。
        /// </summary>
        [Test]
        public void 发布身份_主EditorPump_registry与heartbeat一致()
        {
            string sessionId = YokiFrameEditorFileBridgePump.SessionIdForTests;
            long generation = YokiFrameEditorFileBridgePump.GenerationForTests;
            Assert.IsFalse(string.IsNullOrEmpty(sessionId), "主 Editor Pump session 不能为空。");

            JsonElement registry = ReadPublishedRoot(YokiFrameEditorFileBridgePaths.GetEngineRegistryPath());
            JsonElement heartbeat = ReadPublishedRoot(YokiFrameEditorFileBridgePaths.GetHeartbeatPath());
            Assert.AreEqual(sessionId, ReadRequiredString(registry, "sessionId"), "engine.json 不是当前主 Editor Pump 身份。");
            Assert.AreEqual(generation, ReadRequiredInt64(registry, "generation"), "engine.json generation 已被其它进程覆盖。");
            Assert.AreEqual(sessionId, ReadRequiredString(heartbeat, "sessionId"), "heartbeat 不是当前主 Editor Pump 身份。");
            Assert.AreEqual(generation, ReadRequiredInt64(heartbeat, "generation"), "heartbeat generation 已被其它进程覆盖。");
        }

        /// <summary>
        /// 验证 Pipe 名称只由固定前缀、项目作用域哈希、session 和 generation 组成，不写入项目绝对路径。
        /// </summary>
        [Test]
        public void CreateFastChannelPipeName_当前项目_只包含作用域哈希()
        {
            string projectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(
                YokiFrameEditorFileBridgePaths.GetProjectRoot());
            string expected = PIPE_NAME_PREFIX
                + projectScopeId
                + "."
                + YokiFrameEditorFileBridgePump.SessionIdForTests
                + "."
                + YokiFrameEditorFileBridgePump.GenerationForTests;

            Assert.AreEqual(expected, YokiFrameEditorFileBridgePump.CreateFastChannelPipeName());
        }

        /// <summary>
        /// 验证已发布 registry 的 FastChannel endpoint 与当前 listener 状态一致。
        /// </summary>
        [Test]
        public void 发布注册表_FastChannelEndpoint_与当前监听状态一致()
        {
            string sessionId = YokiFrameEditorFileBridgePump.SessionIdForTests;
            long generation = YokiFrameEditorFileBridgePump.GenerationForTests;
            JsonElement registry = ReadPublishedRoot(YokiFrameEditorFileBridgePaths.GetEngineRegistryPath());
            JsonElement channels;
            Assert.IsTrue(registry.TryGetProperty("fastChannels", out channels), "engine.json 缺少 fastChannels。");
            Assert.AreEqual(JsonValueKind.Array, channels.ValueKind, "fastChannels 必须是数组。");
            Assert.AreEqual(1, channels.GetArrayLength(), "当前 Pump 必须发布一个 FastChannel endpoint。");
            AssertPublishedFastChannelEndpoint(channels[0], sessionId, generation);
        }

        /// <summary>
        /// 验证 Windows Editor 程序集实际编入 Named Pipe host。源码宏不能代替这个程序集证据。
        /// </summary>
        [Test]
        public void AssemblyBoundary_WindowsEditor_编入NamedPipeHost()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("当前不是 Windows Unity Editor，Named Pipe host 类型不应参与本平台断言。");
            }

            Assembly assembly = Assembly.Load("YokiFrame.Unity.Editor");
            Assert.IsNotNull(assembly.GetType(NAMED_PIPE_HOST_TYPE_NAME), "Windows Editor 程序集缺少 Named Pipe host。");
        }

        /// <summary>
        /// 验证当前 Windows Editor 的 Named Pipe listener 已创建并进入 ready。
        /// </summary>
        [Test]
        public void NamedPipeHost_WindowsEditor_监听已Ready()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("当前不是 Windows Unity Editor，Named Pipe listener 不应启动。");
            }

            YokiFrameEditorNamedPipeFastChannelHost host = YokiFrameEditorFileBridgePump.FastChannelHostForTests;
            Assert.IsNotNull(
                host,
                "Windows Editor 应创建 FastChannel Named Pipe Host: "
                + YokiFrameEditorFileBridgePump.FastChannelStartErrorForTests);
            Assert.IsTrue(host.IsReady, "FastChannel listener 未 ready: " + host.LastError);
        }

        /// <summary>
        /// 断言单个已发布 endpoint 的身份、回退通道和当前平台监听状态。
        /// </summary>
        /// <param name="channel">registry 中的 fastChannels 元素。</param>
        /// <param name="sessionId">当前 Pump session。</param>
        /// <param name="generation">当前 Pump generation。</param>
        private static void AssertPublishedFastChannelEndpoint(JsonElement channel, string sessionId, long generation)
        {
            Assert.AreEqual(sessionId, ReadRequiredString(channel, "sessionId"), "FastChannel endpoint session 与 Pump 不一致。");
            Assert.AreEqual(generation, ReadRequiredInt64(channel, "generation"), "FastChannel endpoint generation 与 Pump 不一致。");
            Assert.AreEqual(FILE_BRIDGE_FALLBACK, ReadRequiredString(channel, "fallback"), "FastChannel 失败后必须回退 FileBridge。");
            string transport = ReadRequiredString(channel, "transport");
            bool enabled = ReadRequiredBoolean(channel, "enabled");
            string endpoint = ReadRequiredString(channel, "endpoint");
            YokiFrameEditorNamedPipeFastChannelHost host = YokiFrameEditorFileBridgePump.FastChannelHostForTests;
            if (host != null && host.IsReady)
            {
                Assert.AreEqual(NAMED_PIPE_TRANSPORT, transport, "ready listener 必须发布 namedPipe。");
                Assert.IsTrue(enabled, "ready listener 的 endpoint 必须启用。");
                Assert.AreEqual(host.PipeName, endpoint, "registry endpoint 必须等于当前 Pipe 名称。");
                return;
            }

            Assert.AreEqual(DISABLED_TRANSPORT, transport, "listener 未 ready 时不得发布可用传输。");
            Assert.IsFalse(enabled, "listener 未 ready 时 endpoint 必须关闭。");
            Assert.AreEqual(string.Empty, endpoint, "listener 未 ready 时不得发布 endpoint。");
        }

        /// <summary>
        /// 读取已发布 JSON 文件的根对象。文档由调用方持有，避免元素在释放后失效。
        /// </summary>
        /// <param name="path">JSON 绝对路径。</param>
        /// <returns>根对象。返回值对应的文档在进程内保持可达。</returns>
        private static JsonElement ReadPublishedRoot(string path)
        {
            Assert.IsTrue(File.Exists(path), "缺少已发布 FileBridge 文件: " + path);
            JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.AreEqual(JsonValueKind.Object, document.RootElement.ValueKind, "已发布文件不是 JSON 对象: " + path);
            return document.RootElement;
        }

        /// <summary>
        /// 读取必需字符串字段。
        /// </summary>
        /// <param name="element">JSON 对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段文本。</returns>
        private static string ReadRequiredString(JsonElement element, string propertyName)
        {
            JsonElement value = ReadRequiredProperty(element, propertyName);
            Assert.AreEqual(JsonValueKind.String, value.ValueKind, propertyName + " 必须是字符串。");
            return value.GetString();
        }

        /// <summary>
        /// 读取必需 64 位整数字段。
        /// </summary>
        /// <param name="element">JSON 对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段整数。</returns>
        private static long ReadRequiredInt64(JsonElement element, string propertyName)
        {
            JsonElement value = ReadRequiredProperty(element, propertyName);
            long parsed;
            Assert.IsTrue(value.TryGetInt64(out parsed), propertyName + " 必须是整数。");
            return parsed;
        }

        /// <summary>
        /// 读取必需布尔字段。
        /// </summary>
        /// <param name="element">JSON 对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段布尔值。</returns>
        private static bool ReadRequiredBoolean(JsonElement element, string propertyName)
        {
            JsonElement value = ReadRequiredProperty(element, propertyName);
            Assert.IsTrue(
                value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False,
                propertyName + " 必须是布尔值。");
            return value.GetBoolean();
        }

        /// <summary>
        /// 读取必需属性。
        /// </summary>
        /// <param name="element">JSON 对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>属性值。</returns>
        private static JsonElement ReadRequiredProperty(JsonElement element, string propertyName)
        {
            JsonElement value;
            Assert.IsTrue(element.TryGetProperty(propertyName, out value), "JSON 缺少字段: " + propertyName);
            return value;
        }
    }
}
