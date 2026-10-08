using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 验证 Unity Resources JSON 解析、固定保存路径和 Runtime Settings 惰性宿主工厂。
    /// </summary>
    public sealed class YokiFrameUnityRuntimeSettingsTests
    {
        private const string UNKNOWN_FORMAT_MESSAGE = "YokiFrame runtime settings formatVersion 必须为 1。";
        private const string EDITOR_FIELD_REJECTION =
            "Unity Editor 专属 LogKit 设置必须保存到 Editor 项目配置，不能写入 Player Resources。 (Parameter 'json')";

        /// <summary>
        /// 每个测试前清理 Core 与 Unity 安装器状态，避免宿主工厂和 logger 跨测试保留。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            UnityLogKitRuntimeInstaller.Shutdown();
            KitSettings.Reset();
            LogKit.Reset();
        }

        /// <summary>
        /// 恢复 Editor 域加载时登记的默认工厂，避免本夹具把后续测试留在无宿主后端的状态。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityLogKitRuntimeInstaller.Shutdown();
            KitSettings.Reset();
            LogKit.Reset();
            UnityLogKitRuntimeInstaller.RegisterDefaultFactories();
        }

        /// <summary>
        /// 验证重复 Kit/key 按数组顺序以后值为准。
        /// </summary>
        [Test]
        public void TryParse_重复键_后值覆盖前值()
        {
            const string json = "{\"formatVersion\":1,\"settings\":["
                                + "{\"kit\":\"LogKit\",\"key\":\"enabled\",\"value\":\"true\"},"
                                + "{\"kit\":\"LogKit\",\"key\":\"enabled\",\"value\":\"false\"}]}";

            bool parsed = UnityYokiFrameRuntimeSettingsLoader.TryParse(json, out var store, out var errorMessage);

            Assert.IsTrue(parsed, errorMessage);
            Assert.AreEqual(string.Empty, errorMessage);
            Assert.IsTrue(store.TryGetValue("LogKit", "enabled", out var value));
            Assert.AreEqual("false", value);
        }

        /// <summary>
        /// 验证未知格式版本被拒绝，并返回固定格式诊断。
        /// </summary>
        [Test]
        public void TryParse_未知格式版本_拒绝并返回格式诊断()
        {
            bool parsed = UnityYokiFrameRuntimeSettingsLoader.TryParse(
                "{\"formatVersion\":2,\"settings\":[]}",
                out _,
                out var errorMessage);

            Assert.IsFalse(parsed);
            Assert.AreEqual(UNKNOWN_FORMAT_MESSAGE, errorMessage);
        }

        /// <summary>
        /// 验证 Unity Adapter 只注册默认工厂，首次写日志时才安装宿主后端。
        /// </summary>
        [Test]
        public void RegisterDefaultFactories_首次写日志_才安装Unity后端()
        {
            UnityLogKitRuntimeInstaller.RegisterDefaultFactories();

            Assert.IsFalse(UnityLogKitRuntimeInstaller.IsInstalled);
            LogAssert.Expect(LogType.Warning, "lazy-unity-adapter-warning");
            LogKit.Warning("lazy-unity-adapter-warning");
            Assert.IsTrue(UnityLogKitRuntimeInstaller.IsInstalled);
        }

        /// <summary>
        /// 验证绝对路径等于当前项目 Assets 下的固定 Runtime Settings 资源。
        /// </summary>
        [Test]
        public void GetAbsolutePath_当前项目_固定位于Assets运行时配置()
        {
            string absolutePath = UnityYokiFrameRuntimeSettingsFile.GetAbsolutePath();
            string expectedPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Settings",
                "Resources",
                "YokiFrame",
                "runtime-settings.json"));

            Assert.AreEqual(expectedPath, absolutePath);
            Assert.AreEqual(
                "Assets/Settings/Resources/YokiFrame/runtime-settings.json",
                UnityYokiFrameRuntimeSettingsFile.ASSET_PATH);
        }

        /// <summary>
        /// 验证 Editor 配置叠加只复制 Editor 专属 LogKit 字段，不把同文件里的 Player 字段写进工具会话。
        /// </summary>
        [Test]
        public void CopyEditorOnlyLogKitFields_同时存在Player字段_只复制Editor字段()
        {
            const string json = "{\"formatVersion\":1,\"settings\":["
                                + "{\"kit\":\"LogKit\",\"key\":\"saveLogInEditor\",\"value\":\"false\"},"
                                + "{\"kit\":\"LogKit\",\"key\":\"editorFileName\",\"value\":\"editor.log\"},"
                                + "{\"kit\":\"LogKit\",\"key\":\"saveLogInPlayer\",\"value\":\"true\"}]}";

            bool parsed = UnityYokiFrameRuntimeSettingsLoader.TryParse(json, out var editorStore, out var errorMessage);
            Assert.IsTrue(parsed, errorMessage);

            YokiFrameRuntimeSettingsStore runtimeStore = new();
            UnityYokiFrameEditorSettingsOverlay.CopyEditorOnlyLogKitFields(editorStore, runtimeStore);
            Assert.IsTrue(runtimeStore.TryGetValue(
                LogKitSettings.KIT_NAME,
                LogKitSettings.SAVE_LOG_IN_EDITOR_KEY,
                out var saveInEditor));
            Assert.AreEqual("false", saveInEditor);
            Assert.IsTrue(runtimeStore.TryGetValue(
                LogKitSettings.KIT_NAME,
                LogKitSettings.EDITOR_FILE_NAME_KEY,
                out var editorFileName));
            Assert.AreEqual("editor.log", editorFileName);
            Assert.IsFalse(runtimeStore.TryGetValue(
                LogKitSettings.KIT_NAME,
                LogKitSettings.SAVE_LOG_IN_PLAYER_KEY,
                out _));
        }

        /// <summary>
        /// 验证保存入口拒绝 Editor 专属字段，并且不改写当前项目的 Player 配置文件。
        /// </summary>
        [Test]
        public void Save_包含Editor专属字段_拒绝写入Player配置()
        {
            const string json = "{\"formatVersion\":1,\"settings\":["
                                + "{\"kit\":\"LogKit\",\"key\":\"saveLogInEditor\",\"value\":\"true\"}]}";
            string absolutePath = UnityYokiFrameRuntimeSettingsFile.GetAbsolutePath();
            bool existed = File.Exists(absolutePath);
            byte[] original = existed ? File.ReadAllBytes(absolutePath) : null;
            try
            {
                ArgumentException exception = Assert.Throws<ArgumentException>(
                    () => UnityYokiFrameRuntimeSettingsFile.Save(json));

                Assert.AreEqual("json", exception.ParamName);
                Assert.AreEqual(EDITOR_FIELD_REJECTION, exception.Message);
                AssertRuntimeSettingsFileUnchanged(absolutePath, existed, original);
            }
            finally
            {
                RestoreRuntimeSettingsFile(absolutePath, existed, original);
            }
        }

        /// <summary>
        /// 确认拒绝保存后，当前项目 Runtime Settings 文件的存在性和字节都没有变化。
        /// </summary>
        /// <param name="absolutePath">当前项目 Runtime Settings 绝对路径。</param>
        /// <param name="existed">测试开始前文件是否存在。</param>
        /// <param name="original">测试开始前的文件字节；文件不存在时为 null。</param>
        private static void AssertRuntimeSettingsFileUnchanged(
            string absolutePath,
            bool existed,
            byte[] original)
        {
            if (!existed)
            {
                Assert.IsFalse(File.Exists(absolutePath));
                return;
            }

            Assert.IsTrue(File.Exists(absolutePath));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(absolutePath));
        }

        /// <summary>
        /// 测试失败导致文件被写入时，把当前项目 Runtime Settings 恢复到测试前的字节。
        /// </summary>
        /// <param name="absolutePath">当前项目 Runtime Settings 绝对路径。</param>
        /// <param name="existed">测试开始前文件是否存在。</param>
        /// <param name="original">测试开始前的文件字节；文件不存在时为 null。</param>
        private static void RestoreRuntimeSettingsFile(
            string absolutePath,
            bool existed,
            byte[] original)
        {
            bool existsNow = File.Exists(absolutePath);
            bool changed = existed
                ? !existsNow || !BytesEqual(original, File.ReadAllBytes(absolutePath))
                : existsNow;
            if (!changed)
            {
                return;
            }

            if (existed)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
                File.WriteAllBytes(absolutePath, original);
            }
            else
            {
                File.Delete(absolutePath);
            }

            AssetDatabase.ImportAsset(
                UnityYokiFrameRuntimeSettingsFile.ASSET_PATH,
                ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// 按字节比较两段文件内容，供清理逻辑判断 Player 配置是否被改写。
        /// </summary>
        /// <param name="left">比较左侧字节。</param>
        /// <param name="right">比较右侧字节。</param>
        /// <returns>两侧长度和每个字节都相同时返回 true。</returns>
        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}