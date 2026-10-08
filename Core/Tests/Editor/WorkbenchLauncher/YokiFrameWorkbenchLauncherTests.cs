using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity 菜单打开 Avalonia Workbench 的启动计划，不在测试中真实启动桌面窗口。
    /// </summary>
    public sealed partial class YokiFrameWorkbenchLauncherTests
    {
        private const string LAUNCHER_TYPE_NAME = "YokiFrame.YokiFrameWorkbenchLauncher";

        /// <summary>
        /// 验证启动计划从项目 Runtime 缓存 manifest 读取入口，并向 Workbench 传入当前项目根目录。
        /// </summary>
        [Test]
        public void CreateLaunchPlanUsesProjectRuntimeCacheManifestEntrypoint()
        {
            string projectRoot = CreateProjectRoot();
            string packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
            string executablePath = CreateProjectRuntime(projectRoot, packageRoot, preferGuiEntry: false);

            YokiFrameWorkbenchLaunchPlan plan = YokiFrameWorkbenchLauncher.CreateLaunchPlan(projectRoot, packageRoot);

            Assert.IsTrue(plan.CanLaunch);
            Assert.AreEqual(Path.GetFullPath(executablePath), plan.ExecutablePath);
            Assert.AreEqual(Path.GetFullPath(projectRoot), plan.WorkingDirectory);
            Assert.IsFalse(plan.RequiresBootstrap);
            CollectionAssert.AreEqual(
                new[]
                {
                    "--project", Path.GetFullPath(projectRoot),
                    "--source", Path.GetFullPath(packageRoot)
                },
                SplitArguments(plan.Arguments));
        }

        /// <summary>
        /// 验证项目 Runtime 缓存 manifest 使用 guiEntry 时，Unity 菜单仍能定位 Workbench GUI 入口。
        /// </summary>
        [Test]
        public void CreateLaunchPlanPrefersGuiEntryFromSharedRuntimeManifest()
        {
            string projectRoot = CreateProjectRoot();
            string packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
            string executablePath = CreateProjectRuntime(projectRoot, packageRoot, preferGuiEntry: true);

            YokiFrameWorkbenchLaunchPlan plan = YokiFrameWorkbenchLauncher.CreateLaunchPlan(projectRoot, packageRoot);

            Assert.IsTrue(plan.CanLaunch);
            Assert.AreEqual(Path.GetFullPath(executablePath), plan.ExecutablePath);
        }

        /// <summary>
        /// 验证缓存缺失时 Ctrl+E 创建 bootstrap 计划，且不会把包内目录作为隐式回退。
        /// </summary>
        [Test]
        public void CreateLaunchPlanRequestsBootstrapWhenProjectRuntimeCacheIsMissing()
        {
            string projectRoot = CreateProjectRoot();
            string packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
            CreateWorkbenchSourceInput(packageRoot);

            YokiFrameWorkbenchLaunchPlan plan = YokiFrameWorkbenchLauncher.CreateLaunchPlan(projectRoot, packageRoot);

            Assert.IsTrue(plan.CanLaunch);
            Assert.IsTrue(plan.RequiresBootstrap);
            Assert.AreEqual(string.Empty, plan.ExecutablePath);
            string expectedRuntimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(
                Path.GetFullPath(projectRoot),
                YokiFrameWorkbenchSourceFingerprint.Compute(Path.GetFullPath(packageRoot)));
            Assert.AreEqual(expectedRuntimeRoot, plan.RuntimeRoot);
        }

        /// <summary>
        /// 验证项目已有可启动 Runtime 时不因源码后续变化阻塞 Ctrl+E；新版由 Workbench 后台检测。
        /// </summary>
        [Test]
        public void CreateLaunchPlanUsesCurrentRuntimeBeforeCheckingSourceUpdates()
        {
            string projectRoot = CreateProjectRoot();
            string packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
            string executablePath = CreateProjectRuntime(projectRoot, packageRoot, preferGuiEntry: false);
            File.AppendAllText(
                Path.Combine(packageRoot, "YokiFrameWorkbench~", "src", "Workbench.cs"),
                "// changed after publish");

            YokiFrameWorkbenchLaunchPlan plan = YokiFrameWorkbenchLauncher.CreateLaunchPlan(projectRoot, packageRoot);

            Assert.IsTrue(plan.CanLaunch);
            Assert.IsFalse(plan.RequiresBootstrap);
            Assert.AreEqual(Path.GetFullPath(executablePath), plan.ExecutablePath);
        }

        /// <summary>
        /// 验证 Unity 直接激活使用与 Workbench owner 相同的项目身份哈希，避免两端管道名漂移。
        /// </summary>
        [Test]
        public void ActivationPipeNameMatchesWorkbenchProjectIdentity()
        {
            string projectRoot = CreateProjectRoot() + Path.DirectorySeparatorChar;
            string normalizedRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string identity = Application.platform == RuntimePlatform.WindowsEditor
                ? normalizedRoot.ToUpperInvariant()
                : normalizedRoot;
            byte[] hash;
            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity));
            }

            string expected = "yokiframe-workbench-"
                + BitConverter.ToString(hash, 0, 16).Replace("-", string.Empty).ToLowerInvariant();
            Assert.AreEqual(expected, YokiFrameWorkbenchLauncher.CreateActivationPipeName(projectRoot));
        }

        /// <summary>
        /// 验证 Unity 侧拿到 Editor 主窗口句柄后，会把它传给 Workbench 作为 owned 顶层窗口 owner。
        /// </summary>
        [Test]
        public void CreateLaunchPlanIncludesParentWindowHandleWhenProvided()
        {
            string projectRoot = CreateProjectRoot();
            string packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
            CreateProjectRuntime(projectRoot, packageRoot, preferGuiEntry: false);

            YokiFrameWorkbenchLaunchPlan plan = YokiFrameWorkbenchLauncher.CreateLaunchPlan(projectRoot, packageRoot, 4660L);

            CollectionAssert.IsSubsetOf(new[] { "--parent-hwnd", "4660" }, SplitArguments(plan.Arguments));
        }

        /// <summary>
        /// 验证包根从 launcher 脚本所在路径反推，而不是固定写死为 `Assets/YokiFrame`。
        /// </summary>
        [Test]
        public void ResolvePackageRootUsesLauncherAssetPath()
        {
            string projectRoot = CreateProjectRoot();
            string assetPath = "Packages/com.hinatayoki.yokiframe/Core/Adapters/Unity/Editor/WorkbenchLauncher/YokiFrameWorkbenchLauncher.cs";

            string packageRoot = YokiFrameWorkbenchLauncher.ResolvePackageRootFromAssetPath(projectRoot, assetPath);

            Assert.AreEqual(Path.GetFullPath(Path.Combine(projectRoot, "Packages", "com.hinatayoki.yokiframe")), packageRoot);
        }

        /// <summary>
        /// 验证平台选择同时考虑 Editor 平台和进程架构，避免 macOS x64 误选 arm64 产物。
        /// </summary>
        [Test]
        public void GetRuntimeIdentifierUsesEditorPlatformAndArchitecture()
        {
            Assert.AreEqual("win-x64", YokiFrameWorkbenchLauncher.GetRuntimeIdentifier(RuntimePlatform.WindowsEditor, Architecture.X64));
            Assert.AreEqual("linux-x64", YokiFrameWorkbenchLauncher.GetRuntimeIdentifier(RuntimePlatform.LinuxEditor, Architecture.X64));
            Assert.AreEqual("osx-arm64", YokiFrameWorkbenchLauncher.GetRuntimeIdentifier(RuntimePlatform.OSXEditor, Architecture.Arm64));
            Assert.AreEqual("osx-x64", YokiFrameWorkbenchLauncher.GetRuntimeIdentifier(RuntimePlatform.OSXEditor, Architecture.X64));
        }

        /// <summary>
        /// 验证 Unity 启动器始终传递项目启动请求，由 Workbench 自己完成项目级复用。
        /// </summary>
        [Test]
        public void LaunchDelegatesProjectScopedReuseToWorkbenchProcess()
        {
            string projectRoot = CreateProjectRoot();
            string packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
            CreateProjectRuntime(projectRoot, packageRoot, preferGuiEntry: false);
            YokiFrameWorkbenchLaunchPlan plan = YokiFrameWorkbenchLauncher.CreateLaunchPlan(projectRoot, packageRoot);
            int startCount = 0;
            List<string> logs = new();

            bool launched = YokiFrameWorkbenchLauncher.Launch(
                plan,
                _ => startCount++,
                logs.Add);

            Assert.IsTrue(launched);
            Assert.AreEqual(1, startCount);
            Assert.IsFalse(logs.Exists(log => log.Contains("already running", StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// 验证 Workbench 首次启动时会输出启动信息，便于 Unity Console 诊断实际入口和参数。
        /// </summary>
        [Test]
        public void LaunchLogsStartupInformationBeforeStartingProcess()
        {
            string projectRoot = CreateProjectRoot();
            string packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
            string executablePath = CreateProjectRuntime(projectRoot, packageRoot, preferGuiEntry: false);
            YokiFrameWorkbenchLaunchPlan plan = YokiFrameWorkbenchLauncher.CreateLaunchPlan(projectRoot, packageRoot);
            int startCount = 0;
            List<string> logs = new();

            bool launched = YokiFrameWorkbenchLauncher.Launch(
                plan,
                _ => startCount++,
                logs.Add);

            Assert.IsTrue(launched);
            Assert.AreEqual(1, startCount);
            Assert.IsTrue(logs.Exists(log => log.Contains("Starting YokiFrame Workbench", StringComparison.Ordinal)));
            Assert.IsTrue(logs.Exists(log => log.Contains("Launch timings", StringComparison.Ordinal)));
            Assert.IsTrue(logs.Exists(log => log.Contains(Path.GetFullPath(executablePath), StringComparison.Ordinal)));
            Assert.IsTrue(logs.Exists(log => log.Contains("--project", StringComparison.Ordinal)));
        }

        /// <summary>
        /// 验证菜单入口声明 YokiFrame/Workbench/Open 与 Ctrl/Cmd+E。
        /// 通过 internal 方法组读取 Unity MenuItem，不使用 NonPublic 查找，也不调用该方法。
        /// </summary>
        [Test]
        public void OpenWorkbenchFromMenu_只读取MenuItem特性_声明Workbench快捷键()
        {
            Action openMenu = YokiFrameWorkbenchLauncher.OpenWorkbenchFromMenu;
            MenuItem menuItem = openMenu.Method.GetCustomAttribute<MenuItem>();

            Assert.IsNotNull(menuItem);
            Assert.AreEqual("YokiFrame/Workbench/Open %e", menuItem.menuItem);
        }

        /// <summary>
        /// 按引号和空白拆分启动参数，避免路径中的空格或引号造成误判。
        /// </summary>
        /// <param name="arguments">启动计划参数。</param>
        /// <returns>参数数组。</returns>
        private static string[] SplitArguments(string arguments)
        {
            var values = new System.Collections.Generic.List<string>();
            var current = new System.Text.StringBuilder();
            var quoted = false;
            for (var index = 0; index < arguments.Length; index++)
            {
                char character = arguments[index];
                if (character == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (character == ' ' && !quoted)
                {
                    if (current.Length > 0)
                    {
                        values.Add(current.ToString());
                        current.Clear();
                    }

                    continue;
                }

                current.Append(character);
            }

            if (current.Length > 0)
            {
                values.Add(current.ToString());
            }

            return values.ToArray();
        }

        /// <summary>
        /// 创建与当前源码指纹绑定的项目 Runtime 缓存 manifest 和占位 GUI 入口。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根目录。</param>
        /// <param name="packageRoot">YokiFrame 包根目录。</param>
        /// <param name="preferGuiEntry">是否写入优先级高于 entrypoint 的 guiEntry。</param>
        /// <returns>Workbench 可执行文件路径。</returns>
        private static string CreateProjectRuntime(string projectRoot, string packageRoot, bool preferGuiEntry)
        {
            CreateWorkbenchSourceInput(packageRoot);
            string sourceFingerprint = YokiFrameWorkbenchSourceFingerprint.Compute(packageRoot);
            string runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, sourceFingerprint);
            string runtimeProfile = GetCurrentRuntimeProfile();
            string guiEntry = runtimeProfile + "/Shared.Workbench.exe";
            string executablePath = Path.Combine(runtimeRoot, guiEntry.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(executablePath));
            File.WriteAllText(executablePath, "placeholder");
            string fileHash = ComputeFileSha256(executablePath);
            string platformEntry = preferGuiEntry
                ? "{\"platform\":\"" + runtimeProfile + "\",\"runtimeIdentifier\":\"" + runtimeProfile + "\",\"entrypoint\":\"missing/entry.exe\",\"guiEntry\":\"" + guiEntry + "\",\"fileCount\":1,\"totalBytes\":11,\"files\":[{\"relativePath\":\"" + guiEntry + "\",\"sizeBytes\":11,\"sha256\":\"" + fileHash + "\"}]}"
                : "{\"platform\":\"" + runtimeProfile + "\",\"runtimeIdentifier\":\"" + runtimeProfile + "\",\"entrypoint\":\"" + guiEntry + "\",\"fileCount\":1,\"totalBytes\":11,\"files\":[{\"relativePath\":\"" + guiEntry + "\",\"sizeBytes\":11,\"sha256\":\"" + fileHash + "\"}]}";
            File.WriteAllText(
                Path.Combine(runtimeRoot, "tool-manifest.json"),
                "{\"manifestVersion\":1,\"layoutVersion\":2,\"runtimeRoot\":\".\",\"product\":\"Workbench\",\"platforms\":[" + platformEntry + "]}");
            File.WriteAllText(
                YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(projectRoot),
                "{\"layoutVersion\":1,\"sourceFingerprint\":\"" + sourceFingerprint + "\"}");
            return executablePath;
        }

        /// <summary>
        /// 计算测试入口文件的 SHA-256，生成与实际发布 manifest 相同的文件摘要。
        /// </summary>
        /// <param name="path">待计算文件。</param>
        /// <returns>小写 SHA-256 文本。</returns>
        private static string ComputeFileSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(stream);
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>
        /// 创建最小 Workbench 构建输入，使测试可计算与项目缓存一致的源码指纹。
        /// </summary>
        /// <param name="packageRoot">YokiFrame 包根目录。</param>
        private static void CreateWorkbenchSourceInput(string packageRoot)
        {
            string sourcePath = Path.Combine(packageRoot, "YokiFrameWorkbench~", "src", "FixtureBuildInput.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllText(sourcePath, "namespace Fixture { internal sealed class BuildInput { } }");
        }

        /// <summary>
        /// 根据当前 Unity Editor 宿主解析 bootstrap 实际生成的 Runtime profile。
        /// </summary>
        /// <returns>当前宿主对应的缓存 profile 标识。</returns>
        private static string GetCurrentRuntimeProfile()
        {
            string runtimeIdentifier = YokiFrameWorkbenchLauncher.GetRuntimeIdentifier(Application.platform, RuntimeInformation.ProcessArchitecture);
            Assert.IsFalse(string.IsNullOrWhiteSpace(runtimeIdentifier), "Unsupported Unity Editor Runtime profile.");
            return string.Equals(runtimeIdentifier, "win-x64", StringComparison.Ordinal)
                ? "win-x64-aot"
                : runtimeIdentifier;
        }

        /// <summary>
        /// 获取 Workbench launcher 类型；类型不存在时测试红灯说明菜单启动能力尚未落地。
        /// </summary>
        /// <returns>Workbench launcher 类型。</returns>
        private static Type GetLauncherType()
        {
            Type type = typeof(YokiFrameEditorFileBridgePump).Assembly.GetType(LAUNCHER_TYPE_NAME);
            Assert.IsNotNull(type, "Missing type: " + LAUNCHER_TYPE_NAME);
            return type;
        }

        /// <summary>
        /// 创建测试项目根目录。
        /// </summary>
        /// <returns>测试项目根目录。</returns>
        private static string CreateProjectRoot()
        {
            return Path.Combine(Path.GetTempPath(), "yokiframe-workbench-launcher-tests", Guid.NewGuid().ToString("N"));
        }
    }
}
