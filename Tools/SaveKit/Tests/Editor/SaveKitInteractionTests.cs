#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using JsonDocument = YokiFrame.Json.JsonDocument;
using JsonElement = YokiFrame.Json.JsonElement;
using JsonValueKind = YokiFrame.Json.JsonValueKind;

namespace YokiFrame.Tests
{
    /// <summary>验证 SaveKit Editor Interaction 不创建默认后端、只输出头部并保持查询边界。</summary>
    public sealed class SaveKitInteractionTests
    {
        /// <summary>每个测试重置 SaveKit 静态状态，避免默认工厂和内存文档跨用例泄漏。</summary>
        [SetUp]
        public void SetUp()
        {
            SaveKit.Reset();
            SaveKitEditorInstaller.EnsureInstalled();
        }

        /// <summary>测试后清除本用例创建的 Storage、Serializer、Encryptor 和自动保存状态。</summary>
        [TearDown]
        public void TearDown()
        {
            SaveKit.Reset();
        }

        /// <summary>验证纯状态读取不会调用默认工厂或创建内存后端。</summary>
        [Test]
        public void CreateDiagnosticsSnapshot_未配置后端_不创建默认工厂()
        {
            var storageCreated = false;
            var serializerCreated = false;
            SaveKit.RegisterDefaultBackendFactory(
                () =>
                {
                    storageCreated = true;
                    return new MemorySaveStorage();
                },
                () =>
                {
                    serializerCreated = true;
                    return new TestSaveSerializer();
                });

            SaveKitDiagnosticsSnapshot snapshot = SaveKit.CreateDiagnosticsSnapshot();
            string json = GetProvider().CreateSnapshot("state");

            Assert.IsFalse(storageCreated);
            Assert.IsFalse(serializerCreated);
            Assert.IsFalse(snapshot.StorageConfigured);
            Assert.IsFalse(snapshot.SerializerConfigured);
            Assert.IsFalse(snapshot.MetadataAvailable);
            Assert.AreEqual(1, ReadSchemaVersion(json));
        }

        /// <summary>验证 Provider 只声明两个只读命令，且结果 schema 版本为 1。</summary>
        [Test]
        public void Handle_只读命令_返回固定schema()
        {
            IYokiFrameKitInteractionProvider provider = GetProvider();
            CollectionAssert.AreEqual(
                new[] { "stats", "get_workbench_snapshot" },
                provider.Commands.Select(static command => command.Action).ToArray());
            CollectionAssert.AreEqual(
                new[] { YokiFrameCommandKind.ReadOnly, YokiFrameCommandKind.ReadOnly },
                provider.Commands.Select(static command => command.Kind).ToArray());

            YokiFrameCommandResult stats = provider.Handle(CreateRequest("stats"));
            YokiFrameCommandResult snapshot = provider.Handle(CreateRequest("get_workbench_snapshot"));

            Assert.IsTrue(stats.IsSuccess);
            Assert.IsTrue(snapshot.IsSuccess);
            Assert.AreEqual(1, ReadSchemaVersion(stats.ResultJson));
            Assert.AreEqual(1, ReadSchemaVersion(snapshot.ResultJson));
        }

        /// <summary>验证配置变化只推进 Snapshot 版本，不把 SaveKit 提升为 Telemetry Provider。</summary>
        [Test]
        public void SetStorage_配置变化_只推进Snapshot版本()
        {
            var provider = GetProvider() as IYokiFrameSnapshotVersionedKitInteractionProvider;
            Assert.IsNotNull(provider);
            long initialVersion = provider.StateVersion;

            SaveKit.SetStorage(new MemorySaveStorage());
            SaveKit.SetSerializer(new TestSaveSerializer());

            Assert.Greater(provider.StateVersion, initialVersion);
            Assert.IsFalse(provider is IYokiFrameVersionedKitInteractionProvider);
        }

        /// <summary>验证诊断快照使用头部读取契约，不调用 Storage.Read。</summary>
        [Test]
        public void CreateDiagnosticsSnapshot_仅有头部存储_不读取完整文档()
        {
            var storage = new HeaderOnlyStorage(SaveTarget.Slot(3));
            SaveKit.SetStorage(storage);
            SaveKit.SetSerializer(new TestSaveSerializer());

            SaveKitDiagnosticsSnapshot snapshot = SaveKit.CreateDiagnosticsSnapshot();

            Assert.IsFalse(storage.ReadWasCalled);
            Assert.AreEqual(1, snapshot.Slots.Count);
            Assert.AreEqual(1, snapshot.SlotTotal);
            Assert.AreEqual("Header only", snapshot.Slots[0].DisplayName);
        }

        /// <summary>验证超过 32 个槽时快照只公开有界头部，且模块文本不进入元数据字段。</summary>
        [Test]
        public void CreateSnapshot_超过32个槽_只公开有界头部且不含模块文本()
        {
            SaveKit.SetStorage(new MemorySaveStorage());
            SaveKit.SetSerializer(new TestSaveSerializer());
            const string SECRET = "savekit-private-module-payload";
            for (var index = 0; index < 64; index++)
            {
                SaveData data = SaveKit.CreateSaveData();
                data.RegisterModule(new TestModule { Text = SECRET + index });
                SaveKit.Save(SaveTarget.Slot(index), data, new string('名', 1024));
            }

            SaveKitDiagnosticsSnapshot snapshot = SaveKit.CreateDiagnosticsSnapshot();
            string json = GetProvider().CreateSnapshot("state");

            Assert.AreEqual(64, snapshot.SlotTotal);
            Assert.AreEqual(32, snapshot.Slots.Count);
            Assert.IsTrue(snapshot.SlotsTruncated);
            AssertSnapshotOmitsModuleText(json, SECRET);
            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
        }

        /// <summary>验证 Exists 和 GetMeta 走头部契约，不触发完整 Read。</summary>
        [Test]
        public void Exists_仅有头部存储_不读取完整文档()
        {
            var storage = new HeaderOnlyStorage(SaveTarget.Slot(3));
            SaveKit.SetStorage(storage);
            SaveKit.SetSerializer(new TestSaveSerializer());

            var exists = SaveKit.Exists(SaveTarget.Slot(3));
            var meta = SaveKit.GetMeta(SaveTarget.Slot(3));

            Assert.IsTrue(exists);
            Assert.AreEqual(SaveTarget.Slot(3), meta.Target);
            Assert.IsFalse(storage.ReadWasCalled, "Exists/GetMeta must use TryReadMetadata, not Read.");
        }

        /// <summary>确认 state JSON 的已知字符串字段都不包含模块文本。</summary>
        /// <param name="json">state 快照 JSON。</param>
        /// <param name="secret">只应存在于模块 payload 的文本。</param>
        private static void AssertSnapshotOmitsModuleText(string json, string secret)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                Assert.AreEqual(1, ReadInt32(root, "schemaVersion"));
                AssertStringsOmit(root.GetProperty("backend"), secret, "storageType", "serializerId", "encryptorId");
                AssertCollectionOmits(root.GetProperty("slots"), secret);
                AssertCollectionOmits(root.GetProperty("globals"), secret);
                JsonElement autoSaveTarget;
                if (root.GetProperty("autoSave").TryGetProperty("target", out autoSaveTarget)
                    && autoSaveTarget.ValueKind == JsonValueKind.Object)
                {
                    AssertStringsOmit(autoSaveTarget, secret, "kind", "name");
                }
            }
        }

        /// <summary>确认容器元数据数组中的显示名、序列化器和目标名不包含模块文本。</summary>
        /// <param name="entries">slots 或 globals 数组。</param>
        /// <param name="secret">模块文本。</param>
        private static void AssertCollectionOmits(JsonElement entries, string secret)
        {
            Assert.AreEqual(JsonValueKind.Array, entries.ValueKind);
            for (int index = 0; index < entries.GetArrayLength(); index++)
            {
                JsonElement entry = entries[index];
                AssertStringsOmit(entry, secret, "displayName", "serializerId");
                AssertStringsOmit(entry.GetProperty("target"), secret, "kind", "name");
            }
        }

        /// <summary>确认一组已解析字符串字段都不包含模块文本。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="secret">模块文本。</param>
        /// <param name="propertyNames">字符串字段名。</param>
        private static void AssertStringsOmit(JsonElement element, string secret, params string[] propertyNames)
        {
            for (int index = 0; index < propertyNames.Length; index++)
            {
                string value = ReadString(element, propertyNames[index]);
                Assert.IsTrue(value.IndexOf(secret, StringComparison.Ordinal) < 0, propertyNames[index]);
            }
        }

        /// <summary>读取 state JSON 的 schemaVersion。</summary>
        /// <param name="json">命令或快照 JSON。</param>
        /// <returns>schema 版本。</returns>
        private static int ReadSchemaVersion(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                return ReadInt32(document.RootElement, "schemaVersion");
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

        /// <summary>读取 JSON 整数字段。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段整数。</returns>
        private static int ReadInt32(JsonElement element, string propertyName)
        {
            int value;
            Assert.IsTrue(element.GetProperty(propertyName).TryGetInt32(out value), propertyName);
            return value;
        }

        /// <summary>从 Tool catalog 组合后的 Registry 获取 SaveKit Provider，覆盖真实安装路径。</summary>
        private static IYokiFrameKitInteractionProvider GetProvider()
        {
            IYokiFrameKitInteractionProvider provider = YokiFrameCoreKitInteractions.CreateDefault()
                .Providers.First(item => string.Equals(item.Kit, "SaveKit", StringComparison.Ordinal));
            Assert.IsInstanceOf<IYokiFrameSnapshotVersionedKitInteractionProvider>(provider);
            Assert.IsFalse(provider is IYokiFrameVersionedKitInteractionProvider);
            return provider;
        }

        /// <summary>创建无 payload 的 SaveKit ReadOnly 请求。</summary>
        private static YokiFrameCommandRequest CreateRequest(string action)
        {
            return new YokiFrameCommandRequest("savekit-test", "SaveKit", action, "{}", 1000, 2L);
        }

        /// <summary>承载测试用模块文本，确保 Snapshot 不泄漏其序列化 payload。</summary>
        private sealed class TestModule
        {
            /// <summary>获取或设置仅用于 payload 隔离断言的文本。</summary>
            public string Text { get; set; }
        }

        /// <summary>提供只覆盖本测试写入路径的最小 UTF-8 Serializer。</summary>
        private sealed class TestSaveSerializer : ISaveSerializer
        {
            /// <summary>获取测试 Serializer 的稳定标识。</summary>
            public string SerializerId { get { return "savekit-interaction-test"; } }

            /// <summary>按泛型入口序列化测试模块。</summary>
            public byte[] Serialize<T>(T data)
            {
                return Serialize((object)data);
            }

            /// <summary>反序列化测试模块；本测试不依赖读取路径。</summary>
            public T Deserialize<T>(byte[] bytes)
            {
                return (T)(object)new TestModule { Text = Encoding.UTF8.GetString(bytes) };
            }

            /// <summary>把测试模块文本转为 UTF-8 payload。</summary>
            public byte[] Serialize(object data)
            {
                return Encoding.UTF8.GetBytes(((TestModule)data).Text);
            }

            /// <summary>确认测试 payload 非空，保持契约完整。</summary>
            public void ValidatePayload(string moduleId, byte[] bytes)
            {
                if (bytes == null)
                {
                    throw new ArgumentNullException(nameof(bytes));
                }
            }

            /// <summary>把 UTF-8 payload 覆盖回现有测试模块。</summary>
            public void DeserializeOverwrite(byte[] bytes, object target)
            {
                ((TestModule)target).Text = Encoding.UTF8.GetString(bytes);
            }
        }

        /// <summary>提供只能读取容器头的 Storage 桩，完整 Read 被调用时会使测试失败。</summary>
        private sealed class HeaderOnlyStorage : ISaveStorage, ISaveMetadataStorage
        {
            private readonly SaveTarget mTarget;
            private readonly SaveMeta mMeta;

            /// <summary>创建包含一个 Slot 容器头的测试存储。</summary>
            /// <param name="target">需要公开头部的目标。</param>
            public HeaderOnlyStorage(SaveTarget target)
            {
                mTarget = target;
                mMeta = SaveMeta.Create(target, 1, "savekit-interaction-test", "Header only");
            }

            /// <summary>获取是否有人错误调用了完整文档读取。</summary>
            public bool ReadWasCalled { get; private set; }

            /// <inheritdoc />
            public bool Exists(SaveTarget target)
            {
                return target == mTarget;
            }

            /// <inheritdoc />
            public void Write(SaveTarget target, byte[] bytes)
            {
                throw new NotSupportedException();
            }

            /// <inheritdoc />
            public byte[] Read(SaveTarget target)
            {
                ReadWasCalled = true;
                throw new AssertionException("Interaction must not read full SaveKit payloads.");
            }

            /// <inheritdoc />
            public bool TryReadMetadata(SaveTarget target, out SaveMeta meta)
            {
                meta = mMeta;
                return target == mTarget;
            }

            /// <inheritdoc />
            public bool Delete(SaveTarget target)
            {
                throw new NotSupportedException();
            }

            /// <inheritdoc />
            public IReadOnlyList<SaveTarget> GetTargets(SaveTargetKind kind)
            {
                return kind == mTarget.Kind ? new[] { mTarget } : Array.Empty<SaveTarget>();
            }

            /// <inheritdoc />
            public void Clear(SaveTargetKind kind)
            {
                throw new NotSupportedException();
            }
        }
    }
}
#endif
