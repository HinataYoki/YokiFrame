#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
namespace YokiFrame.Tests
{
    /// <summary>覆盖 SaveKit 核心目标、容器、JSON 迁移和加密边界。</summary>
    public sealed class SaveKitCoreTests
    {
        /// <summary>每个测试使用内存后端和稳定测试序列化器。</summary>
        [SetUp]
        public void SetUp()
        {
            SaveKit.Reset();
            SaveKit.SetStorage(new MemorySaveStorage());
            SaveKit.SetSerializer(new TestSaveSerializer());
        }

        /// <summary>用例结束后覆盖默认工厂，避免闭包和捕获变量泄漏到后续 fixture。</summary>
        [TearDown]
        public void TearDown()
        {
            SaveKit.Reset();
            SaveKit.RegisterDefaultBackendFactory(
                static () => new MemorySaveStorage(),
                static () => new TestSaveSerializer());
        }

        /// <summary>验证槽位和 Global 文档互不混淆。</summary>
        [Test]
        public void SaveAndLoad_SeparatesSlotsAndGlobals()
        {
            var slotData = SaveKit.CreateSaveData();
            slotData.RegisterModule(new TestModule { Value = 12 });
            var globalData = SaveKit.CreateSaveData();
            globalData.RegisterModule(new TestModule { Value = 99 });

            SaveKit.Save(SaveTarget.Slot(0), slotData, "Slot");
            SaveKit.Save(SaveTarget.Global("settings"), globalData);

            Assert.AreEqual(12, SaveKit.Load(SaveTarget.Slot(0)).GetModule<TestModule>().Value);
            Assert.AreEqual(99, SaveKit.Load(SaveTarget.Global("settings")).GetModule<TestModule>().Value);
            Assert.AreEqual(1, SaveKit.GetAllSlots().Count);
            Assert.AreEqual(1, SaveKit.GetAllGlobals().Count);
        }

        /// <summary>验证泛型注册可使用显式模块 ID，并可在读取时按泛型和 ID 获取。</summary>
        [Test]
        public void GenericModuleRegistration_UsesOptionalModuleId()
        {
            var data = SaveKit.CreateSaveData();
            data.RegisterModule(new TestModule { Value = 7 }, "tests.module");
            Assert.AreEqual(7, data.GetModule<TestModule>().Value);
            SaveKit.Save(0, data);

            var result = SaveKit.TryLoad(SaveTarget.Slot(0));
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(7, result.Data.GetModule<TestModule>("tests.module").Value);
        }

        /// <summary>验证未解码的原始模块被删除时也会报告实际删除成功。</summary>
        [Test]
        public void RemoveModule_ReturnsTrueForUnreadRawModule()
        {
            var data = SaveKit.CreateSaveData();
            data.RegisterModule(new TestModule { Value = 8 });
            SaveKit.Save(0, data);

            var loaded = SaveKit.Load(0);

            Assert.IsTrue(loaded.RemoveModule<TestModule>());
            Assert.IsFalse(loaded.HasModule<TestModule>());
        }

        /// <summary>验证默认后端工厂延迟到首次业务调用，并允许槽位编号自然增长。</summary>
        [Test]
        public void DefaultBackendFactory_IsLazyAndSlotsAreUnbounded()
        {
            SaveKit.Reset();
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

            Assert.IsFalse(storageCreated);
            Assert.IsFalse(serializerCreated);

            var data = SaveKit.CreateSaveData();
            data.RegisterModule(new TestModule { Value = 41 });
            Assert.IsTrue(storageCreated);
            Assert.IsTrue(serializerCreated);
            Assert.IsTrue(SaveKit.Save(1234, data));
            Assert.AreEqual(1, SaveKit.GetAllSlots().Count);
        }

        /// <summary>验证异步入口在当前宏模式下完成 Slot/Global 保存、读取和失败状态返回。</summary>
        [Test]
        public void AsyncApi_SavesLoadsAndReportsStatus()
        {
            var slotData = SaveKit.CreateSaveData();
            slotData.RegisterModule(new TestModule { Value = 17 });
            var globalData = SaveKit.CreateSaveData();
            globalData.RegisterModule(new TestModule { Value = 23 });

            Assert.IsTrue(SaveKit.SaveAsync(0, slotData).GetAwaiter().GetResult());
            Assert.IsTrue(SaveKit.SaveAsync(SaveTarget.Global("settings"), globalData).GetAwaiter().GetResult());

            var loaded = SaveKit.LoadAsync(0).GetAwaiter().GetResult();
            Assert.AreEqual(17, loaded.GetModule<TestModule>().Value);
            var missing = SaveKit.TryLoadAsync(SaveTarget.Global("missing")).GetAwaiter().GetResult();
            Assert.AreEqual(SaveLoadStatus.Missing, missing.Status);
        }

        /// <summary>验证异步入口预取消时不会写入目标，并以 OperationCanceledException 结束。</summary>
        [Test]
        public void AsyncApi_PreCancelledDoesNotWrite()
        {
            var data = SaveKit.CreateSaveData();
            data.RegisterModule(new TestModule { Value = 31 });
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.Throws<OperationCanceledException>(() =>
                    SaveKit.SaveAsync(0, data, source.Token).GetAwaiter().GetResult());
            }

            Assert.IsFalse(SaveKit.Exists(0));
        }

        /// <summary>验证极大时间增量不会让自动保存计时溢出为无效浮点值。</summary>
        [Test]
        public void AutoSave_LargeDeltaKeepsElapsedTimeFinite()
        {
            var data = SaveKit.CreateSaveData();
            data.RegisterModule(new TestModule { Value = 52 });
            SaveKit.EnableAutoSave(0, data, float.MaxValue);

            Assert.IsFalse(SaveKit.TickAutoSave(1f));
            Assert.IsTrue(SaveKit.TickAutoSave(float.MaxValue));

            var elapsed = SaveKit.GetAutoSaveElapsedSeconds();
            Assert.IsFalse(float.IsNaN(elapsed));
            Assert.IsFalse(float.IsInfinity(elapsed));
            Assert.GreaterOrEqual(elapsed, 0f);
            Assert.Less(elapsed, float.MaxValue);
        }

        /// <summary>验证 JSON 迁移缺少步骤时返回迁移失败。</summary>
        [Test]
        public void JsonMigration_MissingStepFailsWithoutOverwritingFile()
        {
            var registry = new JsonSaveMigrationRegistry();
            var serializer = new JsonSaveSerializer(new UnityJsonSaveCodec(), 2, registry);
            SaveKit.SetSerializer(new JsonSaveSerializer(new UnityJsonSaveCodec(), 1));
            var data = SaveKit.CreateSaveData();
            data.RegisterModule(new TestModule { Value = 3 });
            SaveKit.Save(0, data);
            SaveKit.SetSerializer(serializer);

            var result = SaveKit.TryLoad(0);

            Assert.AreEqual(SaveLoadStatus.MigrationFailed, result.Status);
            Assert.IsTrue(SaveKit.Exists(0));
        }

        /// <summary>验证容器尾部多余字节会被拒绝。</summary>
        [Test]
        public void CorruptContainer_TrailingBytesAreRejected()
        {
            var storage = (MemorySaveStorage)SaveKit.GetStorage();
            var data = SaveKit.CreateSaveData();
            data.RegisterModule(new TestModule { Value = 1 });
            SaveKit.Save(0, data);
            var bytes = storage.Read(SaveTarget.Slot(0));
            var corrupt = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, corrupt, 0, bytes.Length);
            corrupt[corrupt.Length - 1] = 0x5A;
            storage.Write(SaveTarget.Slot(0), corrupt);

            var result = SaveKit.TryLoad(0);

            Assert.AreEqual(SaveLoadStatus.Invalid, result.Status);
            Assert.IsNull(result.Data);
        }

        /// <summary>验证认证失败不会返回明文数据。</summary>
        [Test]
        public void AuthenticatedEncryptor_RejectsTampering()
        {
            var encryptor = new AesCbcHmacSaveEncryptor("project-secret");
            var encrypted = encryptor.Encrypt(Encoding.UTF8.GetBytes("payload"));
            encrypted[encrypted.Length - 1] ^= 0x01;

            Assert.Throws<System.Security.Cryptography.CryptographicException>(() => encryptor.Decrypt(encrypted));
        }

        /// <summary>验证文件后端隔离槽位目录和 Global 目录。</summary>
        [Test]
        public void FileStorage_PersistsBothTargetKinds()
        {
            var root = Path.Combine(Path.GetTempPath(), "YokiFrame_SaveKit_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileSaveStorage(root);
                storage.Write(SaveTarget.Slot(2), new byte[] { 2 });
                storage.Write(SaveTarget.Global("settings"), new byte[] { 9 });

                Assert.AreEqual(2, storage.Read(SaveTarget.Slot(2))[0]);
                Assert.AreEqual(9, storage.Read(SaveTarget.Global("settings"))[0]);
                Assert.AreEqual(1, storage.GetTargets(SaveTargetKind.Slot).Count);
                Assert.AreEqual(1, storage.GetTargets(SaveTargetKind.Global).Count);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        /// <summary>验证文件后端可仅从容器头恢复元数据，不要求诊断路径反序列化模块 payload。</summary>
        [Test]
        public void FileStorage_ReadsMetadataFromContainerHeader()
        {
            var root = Path.Combine(Path.GetTempPath(), "YokiFrame_SaveKit_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileSaveStorage(root);
                SaveTarget target = SaveTarget.Slot(4);
                SaveMeta meta = SaveMeta.Create(target, 1, "metadata-test", "Header");
                byte[] header = meta.SerializeHeader(256);
                byte[] container = new byte[header.Length + 256];
                Buffer.BlockCopy(header, 0, container, 0, header.Length);
                storage.Write(target, container);

                Assert.IsTrue(storage.TryReadMetadata(target, out SaveMeta actual));
                Assert.AreEqual(target, actual.Target);
                Assert.AreEqual("Header", actual.DisplayName);
                Assert.AreEqual("metadata-test", actual.SerializerId);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        /// <summary>验证多段自定义扩展名不会改变 Slot 或 Global 目标的反向解析结果。</summary>
        [Test]
        public void FileStorage_EnumeratesTargetsWithCompoundExtension()
        {
            var root = Path.Combine(Path.GetTempPath(), "YokiFrame_SaveKit_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileSaveStorage(root, ".save.data");
                SaveTarget slot = SaveTarget.Slot(7);
                SaveTarget global = SaveTarget.Global("settings.v2");
                storage.Write(slot, new byte[] { 7 });
                storage.Write(global, new byte[] { 2 });

                CollectionAssert.AreEqual(new[] { slot }, storage.GetTargets(SaveTargetKind.Slot));
                CollectionAssert.AreEqual(new[] { global }, storage.GetTargets(SaveTargetKind.Global));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        /// <summary>验证迁移注册表按模块 ID 和源版本精确查找，不依赖拼接字符串键。</summary>
        [Test]
        public void JsonMigrationRegistry_ResolvesModuleVersionSteps()
        {
            var registry = new JsonSaveMigrationRegistry();
            registry.Register(new TestJsonMigrator("tests.module", 0, "-v1"));
            registry.Register(new TestJsonMigrator("tests.module", 1, "-v2"));

            byte[] migrated = registry.Migrate("tests.module", 0, 2, Encoding.UTF8.GetBytes("payload"));

            Assert.AreEqual("payload-v1-v2", Encoding.UTF8.GetString(migrated));
        }

        /// <summary>验证文件扩展名不能改变 SaveKit 的目录层级或通配搜索语义。</summary>
        [TestCase(".")]
        [TestCase(".save*")]
        [TestCase("../save")]
        public void FileStorage_RejectsUnsafeFileExtensions(string extension)
        {
            var root = Path.Combine(Path.GetTempPath(), "YokiFrame_SaveKit_" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.Throws<ArgumentException>(() => new FileSaveStorage(root, extension));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        /// <summary>验证 GetAllSlots 按槽位编号升序排序而非按名称字典序，Slot(10) 应排在 Slot(2) 之后。</summary>
        [Test]
        public void GetAllSlots_SortsBySlotIdNotLexicographic()
        {
            var data10 = SaveKit.CreateSaveData();
            data10.RegisterModule(new TestModule { Value = 10 });
            SaveKit.Save(SaveTarget.Slot(10), data10);
            var data2 = SaveKit.CreateSaveData();
            data2.RegisterModule(new TestModule { Value = 2 });
            SaveKit.Save(SaveTarget.Slot(2), data2);

            var slots = SaveKit.GetAllSlots();

            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(2, slots[0].Target.SlotId);
            Assert.AreEqual(10, slots[1].Target.SlotId);
        }

        /// <summary>验证 GetModule 物化后修改字段，再次 Save 时持久化新值而非旧 raw 字节。</summary>
        [Test]
        public void GetModule_AfterMaterialization_SerializesLiveObjectNotStaleRawBytes()
        {
            var original = SaveKit.CreateSaveData();
            original.RegisterModule(new TestModule { Value = 1 });
            SaveKit.Save(0, original);

            var loaded = SaveKit.Load(0);
            var module = loaded.GetModule<TestModule>();
            module.Value = 99;
            SaveKit.Save(0, loaded);

            var reloaded = SaveKit.Load(0);
            Assert.AreEqual(99, reloaded.GetModule<TestModule>().Value);
            Assert.IsTrue(loaded.HasModule<TestModule>());
            Assert.AreEqual(1, loaded.ModuleCount);
        }

        /// <summary>验证手工拼接超 256 字符 ASCII 模块 ID 的容器被识别为 Invalid 而非 MigrationFailed。</summary>
        [Test]
        public void CorruptContainer_OversizedModuleIdReturnsInvalid()
        {
            var target = SaveTarget.Slot(0);
            var storage = (MemorySaveStorage)SaveKit.GetStorage();
            var serializer = SaveKit.GetSerializer();
            var id300 = new string('a', 300);
            var idBytes = Encoding.UTF8.GetBytes(id300);
            byte[] moduleTable;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(1);
                writer.Write(idBytes.Length);
                writer.Write(idBytes);
                writer.Write(0);
                writer.Flush();
                moduleTable = stream.ToArray();
            }

            var meta = SaveMeta.Create(target, 1, serializer.SerializerId, null);
            var header = meta.SerializeHeader(moduleTable.Length);
            var container = new byte[header.Length + moduleTable.Length];
            Buffer.BlockCopy(header, 0, container, 0, header.Length);
            Buffer.BlockCopy(moduleTable, 0, container, header.Length, moduleTable.Length);
            storage.Write(target, container);

            var result = SaveKit.TryLoad(target);

            Assert.AreEqual(SaveLoadStatus.Invalid, result.Status);
            Assert.IsNull(result.Data);
        }

        /// <summary>测试模块验证泛型类型全名和显式注册 ID 两种寻址方式。</summary>
        [Serializable]
        private sealed class TestModule
        {
            public int Value;
        }

        /// <summary>只用于覆盖容器稳定性的最小测试序列化器。</summary>
        private sealed class TestSaveSerializer : ISaveSerializer
        {
            /// <inheritdoc />
            public string SerializerId => "test";

            /// <inheritdoc />
            public byte[] Serialize<T>(T data) => Serialize((object)data);

            /// <inheritdoc />
            public T Deserialize<T>(byte[] bytes)
            {
                var module = new TestModule { Value = int.Parse(Encoding.UTF8.GetString(bytes)) };
                return (T)(object)module;
            }

            /// <inheritdoc />
            public byte[] Serialize(object data) => Encoding.UTF8.GetBytes(((TestModule)data).Value.ToString());

            /// <inheritdoc />
            public void ValidatePayload(string moduleId, byte[] bytes)
            {
                if (bytes == null)
                {
                    throw new InvalidDataException("Test payload cannot be null.");
                }
            }

            /// <inheritdoc />
            public void DeserializeOverwrite(byte[] bytes, object target)
            {
                ((TestModule)target).Value = int.Parse(Encoding.UTF8.GetString(bytes));
            }
        }

        /// <summary>为迁移注册表测试提供按版本追加稳定文本的最小迁移器。</summary>
        private sealed class TestJsonMigrator : IJsonSaveMigrator
        {
            private readonly string mModuleId;
            private readonly int mFromVersion;
            private readonly string mSuffix;

            /// <summary>创建指定模块和源版本的测试迁移器。</summary>
            /// <param name="moduleId">稳定模块 ID。</param>
            /// <param name="fromVersion">迁移起点版本。</param>
            /// <param name="suffix">迁移后追加的文本。</param>
            public TestJsonMigrator(string moduleId, int fromVersion, string suffix)
            {
                mModuleId = moduleId;
                mFromVersion = fromVersion;
                mSuffix = suffix;
            }

            /// <inheritdoc />
            public string ModuleId => mModuleId;

            /// <inheritdoc />
            public int FromVersion => mFromVersion;

            /// <inheritdoc />
            public int ToVersion => mFromVersion + 1;

            /// <inheritdoc />
            public byte[] Migrate(byte[] jsonUtf8)
            {
                return Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(jsonUtf8) + mSuffix);
            }
        }

        /// <summary>
        /// 测试用 JSON 编解码器，镜像 <see cref="YokiFrame.Unity.UnityJsonSaveCodec"/>；
        /// SDK 测试程序集不引用 Unity Adapter，须在测试内保留此副本。
        /// </summary>
        private sealed class UnityJsonSaveCodec : IJsonSaveCodec
        {
            /// <inheritdoc />
            public string Serialize<T>(T data) => JsonUtility.ToJson(data, false);

            /// <inheritdoc />
            public T Deserialize<T>(string json) => JsonUtility.FromJson<T>(json);

            /// <inheritdoc />
            public string Serialize(object data) => JsonUtility.ToJson(data, false);

            /// <inheritdoc />
            public void DeserializeOverwrite(string json, object target) => JsonUtility.FromJsonOverwrite(json, target);
        }
    }
}
#endif
