#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

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
        public void Snapshot_DoesNotInitializeDefaultBackend()
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

            string json = GetProvider().CreateSnapshot("state");

            Assert.IsFalse(storageCreated);
            Assert.IsFalse(serializerCreated);
            StringAssert.Contains("\"storageConfigured\":false", json);
            StringAssert.Contains("\"serializerConfigured\":false", json);
            StringAssert.Contains("\"metadataAvailable\":false", json);
        }

        /// <summary>验证 Provider 只声明两个只读命令，并能返回完整且安全的查询结果。</summary>
        [Test]
        public void Provider_DeclaresAndExecutesOnlyReadOnlyCommands()
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
            StringAssert.Contains("\"schemaVersion\":1", stats.ResultJson);
            StringAssert.Contains("\"schemaVersion\":1", snapshot.ResultJson);
        }

        /// <summary>验证 SaveKit 只跟踪 FileBridge Snapshot 版本，配置变化不会把它提升为 Telemetry Provider。</summary>
        [Test]
        public void Provider_TracksSnapshotChangesWithoutPublishingTelemetry()
        {
            var provider = GetProvider() as IYokiFrameSnapshotVersionedKitInteractionProvider;
            Assert.IsNotNull(provider);
            long initialVersion = provider.StateVersion;

            SaveKit.SetStorage(new MemorySaveStorage());
            SaveKit.SetSerializer(new TestSaveSerializer());

            Assert.Greater(provider.StateVersion, initialVersion);
            Assert.IsFalse(provider is IYokiFrameVersionedKitInteractionProvider);
        }

        /// <summary>验证 Interaction 优先使用可选头部读取契约，不会调用 Storage.Read 取得完整存档。</summary>
        [Test]
        public void Snapshot_UsesMetadataReaderWithoutReadingPayload()
        {
            var storage = new HeaderOnlyStorage(SaveTarget.Slot(3));
            SaveKit.SetStorage(storage);
            SaveKit.SetSerializer(new TestSaveSerializer());

            string json = GetProvider().CreateSnapshot("state");

            Assert.IsFalse(storage.ReadWasCalled);
            StringAssert.Contains("\"slotCount\":1", json);
            StringAssert.Contains("\"slotTotal\":1", json);
            StringAssert.Contains("\"displayName\":\"Header only\"", json);
        }

        /// <summary>验证大量容器仅公开有界头部，不会将模块内容放入 Snapshot。</summary>
        [Test]
        public void Snapshot_IsBoundedAndDoesNotExposeModulePayload()
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

            string json = GetProvider().CreateSnapshot("state");

            StringAssert.Contains("\"slotTotal\":64", json);
            StringAssert.Contains("\"slotCount\":32", json);
            StringAssert.Contains("\"slotsTruncated\":true", json);
            StringAssert.DoesNotContain(SECRET, json);
            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
        }

        /// <summary>验证 Exists 和 GetMeta 走 ISaveMetadataStorage 契约，不触发完整 Read。</summary>
        [Test]
        public void ExistsAndGetMeta_UsesMetadataReaderWithoutReadingPayload()
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
