#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.Tests
{
    /// <summary>验证 Unity Editor 公共上下文的稳定 ID、revision 和 Host Provider 注册。</summary>
    public sealed class UnityEditorContextBridgeTests
    {
        /// <summary>验证 Provider 只声明 state 与 get_context，并拒绝带参数的只读请求。</summary>
        [Test]
        public void ProviderDeclaresOnlyReadOnlyContext()
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

        /// <summary>验证 Selection 变化推进 revision，并输出 GlobalObjectId 而非 Unity 引用。</summary>
        [Test]
        public void SelectionChangePublishesStableObjectContext()
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
                StringAssert.DoesNotContain(Application.dataPath, json);
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

        /// <summary>验证 Unity FileBridge 组合根自动追加 Context Provider，Core 默认 Registry 不承担宿主依赖。</summary>
        [Test]
        public void UnityHostRegistryIncludesContextProvider()
        {
            MethodInfo method = typeof(YokiFrameEditorFileBridgePump).GetMethod(
                "CreateKitInteractions",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            Assert.IsNotNull(method);
            YokiFrameKitInteractionRegistry registry =
                (YokiFrameKitInteractionRegistry)method.Invoke(null, null);

            Assert.AreEqual(
                1,
                registry.Providers.Count(static provider => provider.Kit == "UnityEditor"));
            Assert.IsFalse(
                YokiFrameCoreKitInteractions.CreateDefault().Providers.Any(
                    static provider => provider.Kit == "UnityEditor"));
        }

        /// <summary>创建精确 UTF-8 长度的 UnityEditor/get_context 请求。</summary>
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
