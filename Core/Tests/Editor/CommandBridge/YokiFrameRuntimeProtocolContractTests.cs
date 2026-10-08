using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证共享 Editor 程序集提供 Unity 与 Tool SDK 共用的权威工具协议契约。
    /// </summary>
    public sealed class YokiFrameRuntimeProtocolContractTests
    {
        /// <summary>
        /// 验证 Runtime assembly 暴露稳定的 FileBridge 版本和命令限制。
        /// </summary>
        [Test]
        public void RuntimeAssemblyExposesFileBridgeContract()
        {
            Assert.AreEqual(2, YokiFrameFileBridgeContract.PROTOCOL_VERSION);
            Assert.AreEqual(1000, YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MIN_MS);
            Assert.AreEqual(30000, YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MAX_MS);
            Assert.AreEqual(64 * 1024, YokiFrameFileBridgeContract.PAYLOAD_MAX_BYTES);
            Assert.AreEqual(128 * 1024, YokiFrameFileBridgeContract.COMMAND_FILE_MAX_BYTES);
        }

        /// <summary>
        /// 验证命令来源契约使用产品中立的外部自动化名称，并保持来源集合精确。
        /// </summary>
        [Test]
        public void RuntimeAssemblyExposesProductNeutralCommandSources()
        {
            Assert.AreEqual("external-automation", YokiFrameCommandSourceContract.EXTERNAL_AUTOMATION);
            Assert.AreEqual("cli", YokiFrameCommandSourceContract.CLI);
            Assert.AreEqual("workbench", YokiFrameCommandSourceContract.WORKBENCH);
            Assert.AreEqual("codex", YokiFrameCommandSourceContract.CODEX);
        }

        /// <summary>
        /// 验证 Runtime SafeId predicate 与路径安全规则一致。
        /// </summary>
        [Test]
        public void RuntimeAssemblyExposesSafeIdContract()
        {
            Assert.IsTrue(YokiFrameSafeIdContract.IsSafeId("unity-editor"));
            Assert.IsTrue(YokiFrameSafeIdContract.IsSafeId(new string('a', 128)));
            Assert.IsFalse(YokiFrameSafeIdContract.IsSafeId(new string('a', 129)));
            Assert.IsFalse(YokiFrameSafeIdContract.IsSafeId(".hidden"));
            Assert.IsFalse(YokiFrameSafeIdContract.IsSafeId("bad..name"));
            Assert.IsFalse(YokiFrameSafeIdContract.IsSafeId("bad/name"));
            Assert.IsFalse(YokiFrameSafeIdContract.IsSafeId("中文"));
        }

        /// <summary>
        /// 验证 Unity Editor 动态 FileBridge 路径在 containment 检查前拒绝目录分隔符，保持各宿主 SafeId 语义一致。
        /// </summary>
        [Test]
        public void UnityDynamicFileBridgePathsRejectUnsafeIds()
        {
            string[] unsafeIds = { "bad/name", "../bad", @"bad\name", string.Empty, new string('a', 129) };
            foreach (string unsafeId in unsafeIds)
            {
                Assert.Throws<ArgumentException>(() =>
                    YokiFrameEditorFileBridgePaths.GetSnapshotPath(unsafeId, "state"));
                Assert.Throws<ArgumentException>(() =>
                    YokiFrameEditorFileBridgePaths.GetSnapshotPath("FsmKit", unsafeId));
                Assert.Throws<ArgumentException>(() =>
                    YokiFrameEditorFileBridgePaths.GetResponsePath(unsafeId));
                Assert.Throws<ArgumentException>(() =>
                    YokiFrameEditorFileBridgePaths.GetDeadletterInfoPath(unsafeId));
                Assert.Throws<ArgumentException>(() =>
                    YokiFrameEditorFileBridgePaths.GetDeadletterRequestPath(unsafeId));
            }
        }

        /// <summary>
        /// 验证 Runtime assembly 暴露 Shared Memory v1 header 与 write state 契约。
        /// </summary>
        [Test]
        public void RuntimeAssemblyExposesTelemetryContract()
        {
            Assert.AreEqual(0x4D544659u, YokiFrameSharedMemoryTelemetryContract.MAGIC);
            Assert.AreEqual(1, YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION);
            Assert.AreEqual(52, YokiFrameSharedMemoryTelemetryContract.HEADER_SIZE);
            Assert.AreEqual(64 * 1024, YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
            Assert.AreEqual(0, YokiFrameSharedMemoryTelemetryContract.MAGIC_OFFSET);
            Assert.AreEqual(4, YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION_OFFSET);
            Assert.AreEqual(8, YokiFrameSharedMemoryTelemetryContract.ENGINE_ID_HASH_OFFSET);
            Assert.AreEqual(16, YokiFrameSharedMemoryTelemetryContract.GENERATION_OFFSET);
            Assert.AreEqual(24, YokiFrameSharedMemoryTelemetryContract.SEQUENCE_OFFSET);
            Assert.AreEqual(32, YokiFrameSharedMemoryTelemetryContract.WRITTEN_AT_UTC_TICKS_OFFSET);
            Assert.AreEqual(40, YokiFrameSharedMemoryTelemetryContract.PAYLOAD_LENGTH_OFFSET);
            Assert.AreEqual(44, YokiFrameSharedMemoryTelemetryContract.PAYLOAD_CRC32_OFFSET);
            Assert.AreEqual(48, YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_OFFSET);
            Assert.AreEqual(52, YokiFrameSharedMemoryTelemetryContract.PAYLOAD_OFFSET);
            Assert.AreEqual(0, YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_EMPTY);
            Assert.AreEqual(1, YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_WRITING);
            Assert.AreEqual(2, YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_COMMITTED);
        }

        /// <summary>
        /// 验证原子写入覆盖已有文件后，目标字节与新内容一致，且同目录不残留临时文件或备份。
        /// </summary>
        [Test]
        public void WriteAtomic_覆盖已有JSON_只保留新文件且不留临时文件()
        {
            const string REPLACEMENT = "{\"generation\":2}";
            string directoryPath = Path.Combine(
                Path.GetTempPath(),
                "yokiframe-filebridge-atomic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            try
            {
                string targetPath = Path.Combine(directoryPath, "state.json");
                File.WriteAllText(targetPath, "{\"generation\":1}");

                YokiFrameEditorFileBridgeJson.WriteAtomic(targetPath, REPLACEMENT);

                CollectionAssert.AreEqual(
                    new UTF8Encoding(false).GetBytes(REPLACEMENT),
                    File.ReadAllBytes(targetPath));
                Assert.AreEqual(1, Directory.GetFiles(directoryPath).Length);
            }
            finally
            {
                Directory.Delete(directoryPath, true);
            }
        }
    }
}