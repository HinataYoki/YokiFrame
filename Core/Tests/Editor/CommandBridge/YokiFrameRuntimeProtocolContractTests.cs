using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证共享 Editor 程序集提供 Unity 与 Tool SDK 共用的权威工具协议契约。
    /// </summary>
    public sealed class YokiFrameRuntimeProtocolContractTests
    {
        private const string FILE_BRIDGE_CONTRACT_TYPE = "YokiFrame.YokiFrameFileBridgeContract";
        private const string COMMAND_SOURCE_CONTRACT_TYPE = "YokiFrame.YokiFrameCommandSourceContract";
        private const string SAFE_ID_CONTRACT_TYPE = "YokiFrame.YokiFrameSafeIdContract";
        private const string TELEMETRY_CONTRACT_TYPE = "YokiFrame.YokiFrameSharedMemoryTelemetryContract";

        /// <summary>
        /// 验证 Runtime assembly 暴露稳定的 FileBridge 版本和命令限制。
        /// </summary>
        [Test]
        public void RuntimeAssemblyExposesFileBridgeContract()
        {
            Type contractType = GetRuntimeType(FILE_BRIDGE_CONTRACT_TYPE);

            Assert.AreEqual(2, ReadConstant<int>(contractType, "PROTOCOL_VERSION"));
            Assert.AreEqual(1000, ReadConstant<int>(contractType, "COMMAND_TIMEOUT_MIN_MS"));
            Assert.AreEqual(30000, ReadConstant<int>(contractType, "COMMAND_TIMEOUT_MAX_MS"));
            Assert.AreEqual(64 * 1024, ReadConstant<int>(contractType, "PAYLOAD_MAX_BYTES"));
            Assert.AreEqual(128 * 1024, ReadConstant<int>(contractType, "COMMAND_FILE_MAX_BYTES"));
        }

        /// <summary>
        /// 验证命令来源契约使用产品中立的外部自动化名称，并保持来源集合精确。
        /// </summary>
        [Test]
        public void RuntimeAssemblyExposesProductNeutralCommandSources()
        {
            Type contractType = GetRuntimeType(COMMAND_SOURCE_CONTRACT_TYPE);

            Assert.AreEqual(
                "external-automation",
                ReadConstant<string>(contractType, "EXTERNAL_AUTOMATION"));
            Assert.IsNotNull(contractType.GetField("CLI", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(contractType.GetField("WORKBENCH", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(contractType.GetField("CODEX", BindingFlags.Public | BindingFlags.Static));
            Assert.AreEqual(4, contractType.GetFields(BindingFlags.Public | BindingFlags.Static).Length);
        }

        /// <summary>
        /// 验证 Runtime SafeId predicate 与路径安全规则一致。
        /// </summary>
        [Test]
        public void RuntimeAssemblyExposesSafeIdContract()
        {
            Type contractType = GetRuntimeType(SAFE_ID_CONTRACT_TYPE);
            MethodInfo method = contractType.GetMethod("IsSafeId", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method);

            Assert.IsTrue(InvokePredicate(method, "unity-editor"));
            Assert.IsTrue(InvokePredicate(method, new string('a', 128)));
            Assert.IsFalse(InvokePredicate(method, new string('a', 129)));
            Assert.IsFalse(InvokePredicate(method, ".hidden"));
            Assert.IsFalse(InvokePredicate(method, "bad..name"));
            Assert.IsFalse(InvokePredicate(method, "bad/name"));
            Assert.IsFalse(InvokePredicate(method, "中文"));
        }

        /// <summary>
        /// 验证 Runtime assembly 暴露 Shared Memory v1 header 与 write state 契约。
        /// </summary>
        [Test]
        public void RuntimeAssemblyExposesTelemetryContract()
        {
            Type contractType = GetRuntimeType(TELEMETRY_CONTRACT_TYPE);

            Assert.AreEqual(0x4D544659u, ReadConstant<uint>(contractType, "MAGIC"));
            Assert.AreEqual(1, ReadConstant<int>(contractType, "PROTOCOL_VERSION"));
            Assert.AreEqual(52, ReadConstant<int>(contractType, "HEADER_SIZE"));
            Assert.AreEqual(64 * 1024, ReadConstant<int>(contractType, "DEFAULT_MAX_PAYLOAD_BYTES"));
            Assert.AreEqual(0, ReadConstant<int>(contractType, "MAGIC_OFFSET"));
            Assert.AreEqual(4, ReadConstant<int>(contractType, "PROTOCOL_VERSION_OFFSET"));
            Assert.AreEqual(8, ReadConstant<int>(contractType, "ENGINE_ID_HASH_OFFSET"));
            Assert.AreEqual(16, ReadConstant<int>(contractType, "GENERATION_OFFSET"));
            Assert.AreEqual(24, ReadConstant<int>(contractType, "SEQUENCE_OFFSET"));
            Assert.AreEqual(32, ReadConstant<int>(contractType, "WRITTEN_AT_UTC_TICKS_OFFSET"));
            Assert.AreEqual(40, ReadConstant<int>(contractType, "PAYLOAD_LENGTH_OFFSET"));
            Assert.AreEqual(44, ReadConstant<int>(contractType, "PAYLOAD_CRC32_OFFSET"));
            Assert.AreEqual(48, ReadConstant<int>(contractType, "WRITE_STATE_OFFSET"));
            Assert.AreEqual(52, ReadConstant<int>(contractType, "PAYLOAD_OFFSET"));
            Assert.AreEqual(0, ReadConstant<int>(contractType, "WRITE_STATE_EMPTY"));
            Assert.AreEqual(1, ReadConstant<int>(contractType, "WRITE_STATE_WRITING"));
            Assert.AreEqual(2, ReadConstant<int>(contractType, "WRITE_STATE_COMMITTED"));
        }

        /// <summary>
        /// 验证 Unity FileBridge 覆盖已有 JSON 后只保留完整新文件，不遗留临时文件或回滚备份。
        /// </summary>
        [Test]
        public void UnityFileBridgeAtomicallyReplacesExistingJson()
        {
            string directoryPath = Path.Combine(
                Path.GetTempPath(),
                "yokiframe-filebridge-atomic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            try
            {
                string targetPath = Path.Combine(directoryPath, "state.json");
                File.WriteAllText(targetPath, "{\"generation\":1}");

                YokiFrameEditorFileBridgeJson.WriteAtomic(targetPath, "{\"generation\":2}");

                Assert.AreEqual("{\"generation\":2}", File.ReadAllText(targetPath));
                Assert.AreEqual(1, Directory.GetFiles(directoryPath).Length);
            }
            finally
            {
                Directory.Delete(directoryPath, true);
            }
        }

        /// <summary>
        /// 验证权威 contract 物理位于共享 Core Editor，不依赖具体宿主 SDK。
        /// </summary>
        [Test]
        public void EditorContractSourcesStayHostIndependent()
        {
            string[] roots =
            {
                Path.Combine(Application.dataPath, "YokiFrame", "Core", "Editor", "CommandBridge", "Protocol"),
                Path.Combine(Application.dataPath, "YokiFrame", "Core", "Editor", "Telemetry", "SharedMemory")
            };
            string[] forbiddenTokens =
            {
                "UnityEngine",
                "UnityEditor",
                "Godot",
                "Avalonia",
                "MemoryMappedFiles",
                "System.Text.Json",
                "System.IO"
            };

            foreach (string root in roots)
            {
                string[] sourcePaths = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                Assert.Greater(sourcePaths.Length, 0, "Editor contract 目录必须包含真实源码: " + root);
                foreach (string sourcePath in sourcePaths)
                {
                    string source = File.ReadAllText(sourcePath);
                    foreach (string forbiddenToken in forbiddenTokens)
                    {
                        Assert.IsFalse(source.Contains(forbiddenToken), "共享 Editor contract 禁止具体宿主依赖: " + sourcePath);
                    }
                }
            }
        }

        /// <summary>
        /// 从 YokiFrame Runtime assembly 获取权威 contract 类型。
        /// </summary>
        /// <param name="typeName">完整类型名。</param>
        /// <returns>已确认存在的类型。</returns>
        private static Type GetRuntimeType(string typeName)
        {
            Type contractType = typeof(YokiFrameCommandPolicy).Assembly.GetType(typeName);
            Assert.IsNotNull(contractType, "Runtime assembly 缺少 contract 类型: " + typeName);
            return contractType;
        }

        /// <summary>
        /// 读取公开常量字段的原始值。
        /// </summary>
        /// <typeparam name="T">常量类型。</typeparam>
        /// <param name="type">声明常量的类型。</param>
        /// <param name="fieldName">常量字段名。</param>
        /// <returns>常量值。</returns>
        private static T ReadConstant<T>(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(field, "缺少 Runtime contract 常量: " + fieldName);
            return (T)field.GetRawConstantValue();
        }

        /// <summary>
        /// 调用 Runtime SafeId predicate。
        /// </summary>
        /// <param name="method">IsSafeId 方法。</param>
        /// <param name="value">待检查值。</param>
        /// <returns>predicate 结果。</returns>
        private static bool InvokePredicate(MethodInfo method, string value)
        {
            return (bool)method.Invoke(null, new object[] { value });
        }
    }
}
