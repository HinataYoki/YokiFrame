#if UNITY_EDITOR

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证脚本图标维护不会在安装包导入或域加载期间改写 MonoImporter。
    /// </summary>
    public sealed class YokiFrameKitScriptIconPostprocessorTests
    {
        private const string ICON_PROCESSOR_RELATIVE_PATH =
            "YokiFrame/Core/Adapters/Unity/Editor/Icons/YokiFrameKitScriptIconPostprocessor.cs";

        /// <summary>
        /// 验证图标更新只保留源码开发菜单，不注册自动导入或延迟回调。
        /// </summary>
        [Test]
        public void InstalledPackagesDoNotRegisterAutomaticIconReimports()
        {
            var sourcePath = GetIconProcessorSourcePath();
            Assert.IsTrue(File.Exists(sourcePath), "缺少脚本图标处理器: " + sourcePath);
            var source = File.ReadAllText(sourcePath);

            StringAssert.Contains("[MenuItem(APPLY_SCRIPT_ICONS_MENU)]", source);
            StringAssert.Contains("[MenuItem(APPLY_SCRIPT_ICONS_MENU, true)]", source);
            StringAssert.Contains("SOURCE_ROOT = \"Assets/YokiFrame/\"", source);
            StringAssert.Contains("private static void ApplyExistingScriptIcons()", source);
            StringAssert.Contains("importer.SaveAndReimport();", source);
            StringAssert.DoesNotContain("AssetPostprocessor", source);
            StringAssert.DoesNotContain("OnPostprocessAllAssets", source);
            StringAssert.DoesNotContain("EditorApplication.delayCall", source);
            StringAssert.DoesNotContain("Packages/com.hinatayoki.yokiframe/", source);
        }

        /// <summary>
        /// 获取图标处理器生产源码的绝对路径。
        /// </summary>
        /// <returns>当前源码开发目录内的图标处理器路径。</returns>
        private static string GetIconProcessorSourcePath()
        {
            return Path.Combine(
                Application.dataPath,
                ICON_PROCESSOR_RELATIVE_PATH.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}

#endif
