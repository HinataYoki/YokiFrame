#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using JsonDocument = YokiFrame.Json.JsonDocument;
using JsonElement = YokiFrame.Json.JsonElement;

namespace YokiFrame.Tests
{
    /// <summary>验证 Unity Editor 公共上下文的稳定 ID、revision 和 Host Provider 注册。</summary>
    public sealed class UnityEditorContextBridgeTests
    {
        /// <summary>验证 Provider 只提供无参 get_context，并拒绝带字段的 payload。</summary>
        [Test]
        public void Handle_get_context_只接受空参数的只读请求()
        {
            UnityEditorContextInteractionProvider provider = new();
            CollectionAssert.AreEqual(new[] { "state" }, provider.SnapshotNames);
            Assert.AreEqual(1, provider.Commands.Count);
            Assert.AreEqual("get_context", provider.Commands[0].Action);
            Assert.AreEqual(YokiFrameCommandKind.ReadOnly, provider.Commands[0].Kind);

            YokiFrameCommandResult valid = provider.Handle(CreateRequest("{}"));
            YokiFrameCommandResult invalid = provider.Handle(CreateRequest("{\"write\":true}"));
            Assert.IsTrue(valid.IsSuccess);
            Assert.AreEqual("InvalidPayload", invalid.ErrorCode);
        }

        /// <summary>验证选择场景对象后 revision 推进，序列化结果使用稳定标识且不写出绝对 Assets 路径。</summary>
        [Test]
        public void Capture_选择场景对象_发布稳定标识且不含绝对资源路径()
        {
            UnityEngine.Object[] previousSelection = Selection.objects;
            GameObject root = new("ContextRoot");
            GameObject child = new("ContextChild");
            child.transform.SetParent(root.transform, false);
            try
            {
                long before = UnityEditorContextService.Revision;
                Selection.activeGameObject = child;
                long after = UnityEditorContextService.Revision;
                UnityEditorContextSnapshot snapshot = UnityEditorContextService.Capture();
                string json = new UnityEditorContextInteractionProvider().CreateSnapshot("state");

                Assert.Greater(after, before);
                Assert.AreEqual(after, snapshot.revision);
                Assert.AreEqual("ContextChild", snapshot.selection.activeObject.name);
                Assert.AreEqual("ContextRoot/ContextChild", snapshot.selection.activeObject.hierarchyPath);
                Assert.IsNotEmpty(snapshot.selection.activeObject.globalObjectId);
                Assert.IsTrue(snapshot.selection.activeObject.isGameObject);
                Assert.IsFalse(snapshot.selection.activeObject.isAsset);
                AssertSerializedContextOmitsDataPath(json);
                Assert.LessOrEqual(
                    Encoding.UTF8.GetByteCount(json),
                    YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
            }
            finally
            {
                Selection.objects = previousSelection;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证 Unity FileBridge 组合根追加 Context Provider，Core 默认 Registry 不承担宿主依赖。</summary>
        [Test]
        public void CreateKitInteractions_Unity宿主_注册上下文且Core默认不包含()
        {
            YokiFrameKitInteractionRegistry registry = YokiFrameEditorFileBridgePump.CreateKitInteractions();

            Assert.AreEqual(
                1,
                registry.Providers.Count(static provider => provider.Kit == "UnityEditor"));
            Assert.IsFalse(
                YokiFrameCoreKitInteractions.CreateDefault().Providers.Any(
                    static provider => provider.Kit == "UnityEditor"));
        }

        /// <summary>确认序列化后的路径字段不包含绝对 Assets 目录，且场景对象没有资产路径。</summary>
        /// <param name="json">state 快照 JSON。</param>
        private static void AssertSerializedContextOmitsDataPath(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                JsonElement selection = root.GetProperty("selection");
                JsonElement active = selection.GetProperty("activeObject");
                Assert.AreEqual(string.Empty, ReadString(active, "assetPath"));
                AssertStringsOmitDataPath(active, "globalObjectId", "assetGuid", "assetPath", "name", "type", "hierarchyPath");
                AssertStringsOmitDataPath(selection, "activeGlobalObjectId");
                JsonElement objects = selection.GetProperty("objects");
                for (int index = 0; index < objects.GetArrayLength(); index++)
                {
                    Assert.AreEqual(string.Empty, ReadString(objects[index], "assetPath"));
                    AssertStringsOmitDataPath(objects[index], "globalObjectId", "assetGuid", "assetPath", "name", "type", "hierarchyPath");
                }

                AssertStringsOmitDataPath(root.GetProperty("scene"), "path", "name");
                AssertStringsOmitDataPath(root.GetProperty("prefabStage"), "assetPath", "scenePath", "rootName");
                AssertStringsOmitDataPath(root.GetProperty("editor"), "mode");
            }
        }

        /// <summary>确认一组已解析字符串字段都不包含绝对 Assets 目录。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyNames">字符串字段名。</param>
        private static void AssertStringsOmitDataPath(JsonElement element, params string[] propertyNames)
        {
            string dataPath = Application.dataPath;
            for (int index = 0; index < propertyNames.Length; index++)
            {
                string value = ReadString(element, propertyNames[index]);
                Assert.IsTrue(
                    string.IsNullOrEmpty(value) || value.IndexOf(dataPath, StringComparison.OrdinalIgnoreCase) < 0,
                    propertyNames[index]);
            }
        }

        /// <summary>读取 JSON 字符串字段。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段文本；JSON null 时返回空字符串。</returns>
        private static string ReadString(JsonElement element, string propertyName)
        {
            string value = element.GetProperty(propertyName).GetString();
            return value ?? string.Empty;
        }

        /// <summary>创建精确 UTF-8 长度的 UnityEditor/get_context 请求。</summary>
        /// <param name="payload">请求 payload。</param>
        /// <returns>命令请求。</returns>
        private static YokiFrameCommandRequest CreateRequest(string payload)
        {
            return new YokiFrameCommandRequest(
                "context-test",
                "UnityEditor",
                "get_context",
                payload,
                1000,
                Encoding.UTF8.GetByteCount(payload));
        }
    }
}
#endif
