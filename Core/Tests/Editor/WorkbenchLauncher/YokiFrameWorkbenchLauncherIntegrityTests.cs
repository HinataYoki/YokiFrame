using System;
using System.IO;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity Workbench Launcher 在启动前拒绝损坏或越界的项目 Runtime 缓存。
    /// </summary>
    public sealed partial class YokiFrameWorkbenchLauncherTests
    {
        /// <summary>
        /// 验证 Windows Ctrl+E 使用项目缓存中的 Native AOT profile，而不回退包内 managed 目录。
        /// </summary>
        [Test]
        public void CreateLaunchPlanUsesNativeAotProjectRuntimeProfile()
        {
            if (!string.Equals(GetCurrentRuntimeProfile(), "win-x64-aot", StringComparison.Ordinal))
            {
                Assert.Ignore("Native AOT profile selection is specific to Windows x64 Unity Editor.");
            }

            string projectRoot = CreateProjectRoot();
            string packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
            string executablePath = CreateProjectRuntime(projectRoot, packageRoot, preferGuiEntry: true);

            object plan = CreateLaunchPlan(projectRoot, packageRoot);

            Assert.AreEqual(true, GetProperty(plan, "CanLaunch"));
            Assert.AreEqual(Path.GetFullPath(executablePath), GetProperty(plan, "ExecutablePath"));
        }

        /// <summary>
        /// 验证入口文件被篡改后启动计划转为重新 bootstrap，而不是继续启动损坏缓存。
        /// </summary>
        [Test]
        public void CreateLaunchPlanRejectsTamperedRuntimeEntrypoint()
        {
            string projectRoot = CreateProjectRoot();
            string packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
            string executablePath = CreateProjectRuntime(projectRoot, packageRoot, preferGuiEntry: false);
            File.AppendAllText(executablePath, "tampered");

            object plan = CreateLaunchPlan(projectRoot, packageRoot);

            Assert.AreEqual(true, GetProperty(plan, "CanLaunch"));
            Assert.AreEqual(true, GetProperty(plan, "RequiresBootstrap"));
        }

        /// <summary>
        /// 验证文件清单完全有效时，manifest 入口仍不能通过父目录路径跳出项目 Runtime 缓存。
        /// </summary>
        [Test]
        public void CreateLaunchPlanRejectsRuntimeEntrypointTraversal()
        {
            string projectRoot = CreateProjectRoot();
            string packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
            string executablePath = CreateProjectRuntime(projectRoot, packageRoot, preferGuiEntry: false);
            string runtimeRoot = Path.GetDirectoryName(Path.GetDirectoryName(executablePath));
            string runtimeProfile = GetCurrentRuntimeProfile();
            string listedEntry = runtimeProfile + "/Shared.Workbench.exe";
            string manifestPath = Path.Combine(runtimeRoot, "tool-manifest.json");
            string outsidePath = Path.Combine(Path.GetDirectoryName(runtimeRoot), "outside.exe");
            File.WriteAllText(outsidePath, "outside");
            File.WriteAllText(
                manifestPath,
                "{\"manifestVersion\":1,\"layoutVersion\":2,\"runtimeRoot\":\".\",\"platforms\":[{\"platform\":\""
                + runtimeProfile
                + "\",\"runtimeIdentifier\":\""
                + runtimeProfile
                + "\",\"entrypoint\":\"../outside.exe\",\"guiEntry\":\"../outside.exe\",\"fileCount\":1,\"totalBytes\":11,\"files\":[{\"relativePath\":\""
                + listedEntry
                + "\",\"sizeBytes\":11,\"sha256\":\""
                + ComputeFileSha256(executablePath)
                + "\"}]}]}");

            object plan = CreateLaunchPlan(projectRoot, packageRoot);

            Assert.AreEqual(true, GetProperty(plan, "CanLaunch"));
            Assert.AreEqual(true, GetProperty(plan, "RequiresBootstrap"));
        }

        /// <summary>
        /// 验证将 Assets/YokiFrame 完整导出为 Unitypackage 产物，确保发布包无损坏依赖并生成合法交付物。
        /// </summary>
        [Test]
        public void ExportUnityPackage_SucceedsAndGeneratesExpectedArtifact()
        {
            string outputDir = Path.GetFullPath("Releases");
            Directory.CreateDirectory(outputDir);
            string packagePath = Path.Combine(outputDir, "YokiFrame_v2.0.0.unitypackage");
            UnityEditor.AssetDatabase.ExportPackage("Assets/YokiFrame", packagePath, UnityEditor.ExportPackageOptions.Recurse);
            Assert.IsTrue(File.Exists(packagePath), "Unitypackage should be exported successfully.");
            FileInfo fileInfo = new FileInfo(packagePath);
            Assert.Greater(fileInfo.Length, 1024, "Unitypackage size should be greater than 1KB.");
        }
    }
}
