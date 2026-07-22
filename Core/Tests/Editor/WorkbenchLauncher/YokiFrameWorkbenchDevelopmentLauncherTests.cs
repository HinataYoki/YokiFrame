using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证项目专用开发菜单保持在发布包外，并把显式 Runtime 构建写入当前项目缓存。
    /// </summary>
    public sealed class YokiFrameWorkbenchDevelopmentLauncherTests
    {
        /// <summary>
        /// 验证开发菜单位于项目 Assets/Editor，不会随 YokiFrame Git URL 或 embedded package 交付。
        /// </summary>
        [Test]
        public void DevelopmentBuildMenuLivesUnderProjectAssetsEditor()
        {
            Assert.IsTrue(File.Exists(GetDevelopmentLauncherPath()), GetDevelopmentLauncherPath());
        }

        /// <summary>
        /// 验证开发菜单调用发布脚本时传入当前项目根，使脚本只能写入项目 `.yokiframe` Runtime 缓存。
        /// </summary>
        [Test]
        public void DevelopmentPublishPassesProjectRootToRuntimeCacheScript()
        {
            var source = ReadDevelopmentLauncherSource();

            StringAssert.Contains("RunRuntimePublish(scriptPath, projectRoot, mode)", source);
            StringAssert.Contains(" -ProjectRoot ", source);
            StringAssert.Contains("QuoteArgument(projectRoot)", source);
        }

        /// <summary>
        /// 验证开发菜单不再创建、引用或依赖包内 WorkbenchRuntime 目录。
        /// </summary>
        [Test]
        public void DevelopmentBuildMenuDoesNotUsePackageRuntimeDirectory()
        {
            var source = ReadDevelopmentLauncherSource();

            StringAssert.DoesNotContain("WorkbenchRuntime~", source);
            StringAssert.Contains("YokiFrameWorkbench~", source);
        }

        /// <summary>
        /// 读取项目专用开发菜单源码，供路径与参数边界测试复用。
        /// </summary>
        /// <returns>完整 C# 源码文本。</returns>
        private static string ReadDevelopmentLauncherSource()
        {
            return File.ReadAllText(GetDevelopmentLauncherPath());
        }

        /// <summary>
        /// 获取项目专用开发菜单的绝对路径。
        /// </summary>
        /// <returns>开发菜单 C# 文件路径。</returns>
        private static string GetDevelopmentLauncherPath()
        {
            return Path.Combine(Application.dataPath, "Editor", "YokiFrameWorkbenchDevelopmentLauncher.cs");
        }
    }
}
