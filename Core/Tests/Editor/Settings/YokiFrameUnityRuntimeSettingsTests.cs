using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 验证 Unity Resources JSON 解析和 Runtime Settings 惰性宿主工厂。
    /// </summary>
    public sealed class YokiFrameUnityRuntimeSettingsTests
    {
        /// <summary>
        /// 每个测试前清理 Core 全局状态，避免宿主工厂和 logger 跨测试保留。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            KitSettings.Reset();
            LogKit.Reset();
        }

        /// <summary>
        /// 验证 Unity JSON 使用稳定格式并按数组顺序让重复键最后值生效。
        /// </summary>
        [Test]
        public void RuntimeSettingsJsonUsesLastValidEntry()
        {
            const string json = "{\"formatVersion\":1,\"settings\":["
                                + "{\"kit\":\"LogKit\",\"key\":\"enabled\",\"value\":\"true\"},"
                                + "{\"kit\":\"LogKit\",\"key\":\"enabled\",\"value\":\"false\"}]}";

            bool parsed = UnityYokiFrameRuntimeSettingsLoader.TryParse(json, out var store, out var errorMessage);

            Assert.IsTrue(parsed, errorMessage);
            Assert.IsTrue(store.TryGetValue("LogKit", "enabled", out var value));
            Assert.AreEqual("false", value);
        }

        /// <summary>
        /// 验证未知格式版本会被明确拒绝，避免静默按错误结构应用真机配置。
        /// </summary>
        [Test]
        public void RuntimeSettingsJsonRejectsUnknownFormatVersion()
        {
            bool parsed = UnityYokiFrameRuntimeSettingsLoader.TryParse(
                "{\"formatVersion\":2,\"settings\":[]}",
                out _,
                out var errorMessage);

            Assert.IsFalse(parsed);
            StringAssert.Contains("formatVersion", errorMessage);
        }

        /// <summary>
        /// 验证 Unity Adapter 只注册默认工厂，首次写日志时才安装宿主后端。
        /// </summary>
        [Test]
        public void FirstLogWriteInstallsUnityRuntimeAdapterLazily()
        {
            UnityLogKitRuntimeInstaller.RegisterDefaultFactories();

            Assert.IsFalse(UnityLogKitRuntimeInstaller.IsInstalled);
            LogAssert.Expect(LogType.Warning, "lazy-unity-adapter-warning");
            LogKit.Warning("lazy-unity-adapter-warning");
            Assert.IsTrue(UnityLogKitRuntimeInstaller.IsInstalled);
        }

        /// <summary>
        /// 验证 Runtime Adapter 源码不再提供需要调用方选择引擎的 YokiFrameKit 初始化入口。
        /// </summary>
        [Test]
        public void RuntimeAdapterDoesNotExposeGlobalInitializationEntry()
        {
            Type initializationType = Type.GetType("YokiFrame.YokiFrameKit, YokiFrame.Unity.Runtime", false);

            Assert.IsNull(initializationType);
        }

        /// <summary>
        /// 验证 Unity Editor 保存路径固定落在当前项目 Assets 下，不会复用其它项目或全局配置。
        /// </summary>
        [Test]
        public void RuntimeSettingsPathIsContainedByCurrentProjectAssets()
        {
            MethodInfo getAbsolutePath = typeof(UnityYokiFrameRuntimeSettingsFile).GetMethod(
                "GetAbsolutePath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(getAbsolutePath);

            string absolutePath = (string)getAbsolutePath.Invoke(null, Array.Empty<object>());
            string assetsRoot = Path.GetFullPath(Application.dataPath)
                                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
            StringAssert.StartsWith(assetsRoot, absolutePath);
            Assert.AreEqual(
                "Assets/Settings/Resources/YokiFrame/runtime-settings.json",
                UnityYokiFrameRuntimeSettingsFile.ASSET_PATH);
        }

        /// <summary>
        /// 验证 Runtime Settings Loader 只读取 Resources；ProjectSettings 文件 IO 必须由 Editor Adapter 拥有。
        /// </summary>
        [Test]
        public void RuntimeSettingsLoaderDoesNotReadEditorProjectFiles()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "YokiFrame",
                "Core",
                "Adapters",
                "Unity",
                "Runtime",
                "Settings",
                "UnityYokiFrameRuntimeSettingsLoader.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.DoesNotContain("using System.IO", source);
            StringAssert.DoesNotContain("ProjectSettings/", source);
            StringAssert.DoesNotContain("File.Read", source);
            StringAssert.Contains("UnityYokiFrameRuntimeSettingsEditorOverlay.TryApply", source);
        }

        /// <summary>
        /// 验证 Editor Adapter 已安装项目配置 overlay，并能把独立文件中的字段合并到工具会话 Store。
        /// </summary>
        [Test]
        public void EditorSettingsOverlayLoadsProjectScopedFields()
        {
            YokiFrameRuntimeSettingsStore store = new();

            bool applied = UnityYokiFrameRuntimeSettingsEditorOverlay.TryApply(
                store,
                out string errorMessage);

            Assert.IsTrue(applied, errorMessage);
            Assert.IsTrue(store.TryGetValue(
                LogKitSettings.KIT_NAME,
                LogKitSettings.SAVE_LOG_IN_EDITOR_KEY,
                out _));
            Assert.IsTrue(store.TryGetValue(
                LogKitSettings.KIT_NAME,
                LogKitSettings.EDITOR_FILE_NAME_KEY,
                out _));
        }

        /// <summary>
        /// 验证 Player Resources 配置只保留运行时字段，不携带 Unity Editor 文件写入设置。
        /// </summary>
        [Test]
        public void RuntimeSettingsResourceExcludesEditorOnlyLogKitFields()
        {
            string settingsPath = Path.Combine(
                Application.dataPath,
                "Settings",
                "Resources",
                "YokiFrame",
                "runtime-settings.json");
            string json = File.ReadAllText(settingsPath);

            StringAssert.DoesNotContain("saveLogInEditor", json);
            StringAssert.DoesNotContain("editorFileName", json);
            StringAssert.Contains("saveLogInPlayer", json);
            StringAssert.Contains("playerFileName", json);
        }

        /// <summary>
        /// 验证 Unity Editor 保存入口拒绝把 Editor 专属字段重新写入 Player Resources。
        /// </summary>
        [Test]
        public void RuntimeSettingsSaveRejectsEditorOnlyLogKitFields()
        {
            const string json = "{\"formatVersion\":1,\"settings\":["
                                + "{\"kit\":\"LogKit\",\"key\":\"saveLogInEditor\",\"value\":\"true\"}]}";

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => UnityYokiFrameRuntimeSettingsFile.Save(json));

            StringAssert.Contains("Editor", exception.Message);
        }

        /// <summary>
        /// 验证 Unity Runtime Adapter 不再包含旧 ScriptableObject Settings 类型。
        /// </summary>
        [Test]
        public void LegacyScriptableObjectSettingsTypeIsRemoved()
        {
            Type legacyType = Type.GetType(
                "YokiFrame.Unity.YokiFrameRuntimeSettings, YokiFrame.Unity.Runtime",
                false);

            Assert.IsNull(legacyType);
        }
    }
}
