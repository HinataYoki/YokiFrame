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

            object plan = CreateLaunchPlan(projectRoot, packageRoot);

            Assert.AreEqual(true, GetProperty(plan, "CanLaunch"));
            Assert.AreEqual(Path.GetFullPath(executablePath), GetProperty(plan, "ExecutablePath"));
            Assert.AreEqual(Path.GetFullPath(projectRoot), GetProperty(plan, "WorkingDirectory"));
            Assert.AreEqual(false, GetProperty(plan, "RequiresBootstrap"));
            StringAssert.Contains("--project", (string)GetProperty(plan, "Arguments"));
            StringAssert.Contains(Path.GetFullPath(projectRoot), (string)GetProperty(plan, "Arguments"));
            StringAssert.Contains("--source", (string)GetProperty(plan, "Arguments"));
            StringAssert.Contains(Path.GetFullPath(packageRoot), (string)GetProperty(plan, "Arguments"));
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

            object plan = CreateLaunchPlan(projectRoot, packageRoot);

            Assert.AreEqual(true, GetProperty(plan, "CanLaunch"));
            Assert.AreEqual(Path.GetFullPath(executablePath), GetProperty(plan, "ExecutablePath"));
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

            object plan = CreateLaunchPlan(projectRoot, packageRoot);

            Assert.AreEqual(true, GetProperty(plan, "CanLaunch"));
            Assert.AreEqual(true, GetProperty(plan, "RequiresBootstrap"));
            Assert.AreEqual(string.Empty, GetProperty(plan, "ExecutablePath"));
            StringAssert.Contains(Path.Combine(".yokiframe", "runtime"), (string)GetProperty(plan, "RuntimeRoot"));
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

            object plan = CreateLaunchPlan(projectRoot, packageRoot);

            Assert.AreEqual(true, GetProperty(plan, "CanLaunch"));
            Assert.AreEqual(false, GetProperty(plan, "RequiresBootstrap"));
            Assert.AreEqual(Path.GetFullPath(executablePath), GetProperty(plan, "ExecutablePath"));
        }

        /// <summary>
        /// 验证 Unity 直接激活使用与 Workbench owner 相同的项目身份哈希，避免两端管道名漂移。
        /// </summary>
        [Test]
        public void ActivationPipeNameMatchesWorkbenchProjectIdentity()
        {
            string projectRoot = CreateProjectRoot() + Path.DirectorySeparatorChar;
            MethodInfo method = GetLauncherType().GetMethod(
                "CreateActivationPipeName",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

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
            Assert.AreEqual(expected, method.Invoke(null, new object[] { projectRoot }));
        }

        /// <summary>
        /// 验证 Unity Mono 激活客户端不使用会访问未实现 WindowsIdentity.Owner 的 CurrentUserOnly，并将该兼容失败降级为普通启动。
        /// </summary>
        [Test]
        public void ActivationClientAvoidsMonoUnsupportedCurrentUserOnly()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "YokiFrame",
                "Core",
                "Adapters",
                "Unity",
                "Editor",
                "WorkbenchLauncher",
                "YokiFrameWorkbenchLauncher.Activation.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("PipeOptions.Asynchronous))", source);
            StringAssert.DoesNotContain("PipeOptions.CurrentUserOnly", source);
            StringAssert.Contains("exception is NotImplementedException", source);
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

            object plan = CreateLaunchPlan(projectRoot, packageRoot, 4660L);

            string arguments = (string)GetProperty(plan, "Arguments");
            StringAssert.Contains("--parent-hwnd", arguments);
            StringAssert.Contains("4660", arguments);
        }

        /// <summary>
        /// 验证包根从 launcher 脚本所在路径反推，而不是固定写死为 `Assets/YokiFrame`。
        /// </summary>
        [Test]
        public void ResolvePackageRootUsesLauncherAssetPath()
        {
            string projectRoot = CreateProjectRoot();
            string assetPath = "Packages/com.hinatayoki.yokiframe/Core/Adapters/Unity/Editor/WorkbenchLauncher/YokiFrameWorkbenchLauncher.cs";

            string packageRoot = ResolvePackageRootFromAssetPath(projectRoot, assetPath);

            Assert.AreEqual(Path.GetFullPath(Path.Combine(projectRoot, "Packages", "com.hinatayoki.yokiframe")), packageRoot);
        }

        /// <summary>
        /// 验证平台选择同时考虑 Editor 平台和进程架构，避免 macOS x64 误选 arm64 产物。
        /// </summary>
        [Test]
        public void GetRuntimeIdentifierUsesEditorPlatformAndArchitecture()
        {
            Assert.AreEqual("win-x64", GetRuntimeIdentifier(RuntimePlatform.WindowsEditor, Architecture.X64));
            Assert.AreEqual("linux-x64", GetRuntimeIdentifier(RuntimePlatform.LinuxEditor, Architecture.X64));
            Assert.AreEqual("osx-arm64", GetRuntimeIdentifier(RuntimePlatform.OSXEditor, Architecture.Arm64));
            Assert.AreEqual("osx-x64", GetRuntimeIdentifier(RuntimePlatform.OSXEditor, Architecture.X64));
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
            object plan = CreateLaunchPlan(projectRoot, packageRoot);
            int startCount = 0;
            List<string> logs = new();

            bool launched = LaunchWithLog(
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
            object plan = CreateLaunchPlan(projectRoot, packageRoot);
            int startCount = 0;
            List<string> logs = new();

            bool launched = LaunchWithLog(
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
        /// 验证 Unity 菜单方法声明在 `YokiFrame/Workbench/Open`，并提供 Ctrl/Cmd+E 快捷键。
        /// </summary>
        [Test]
        public void OpenWorkbenchMenuItemUsesYokiFrameWorkbenchShortcut()
        {
            MethodInfo method = GetLauncherType().GetMethod("OpenWorkbenchFromMenu", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method);
            MenuItem menuItem = method.GetCustomAttribute<MenuItem>();

            Assert.IsNotNull(menuItem);
            Assert.AreEqual("YokiFrame/Workbench/Open %e", menuItem.menuItem);
        }

        /// <summary>
        /// 验证同项目已有 Workbench 时优先发送激活请求，不会因过期缓存先启动 Runtime bootstrap。
        /// </summary>
        [Test]
        public void OpenWorkbenchActivatesExistingInstanceBeforeInspectingRuntimeCache()
        {
            string source = ReadLauncherSource();
            int activationIndex = source.IndexOf("if (TryActivateExistingWorkbench(projectRoot))", StringComparison.Ordinal);
            int bootstrapIndex = source.IndexOf("if (sRuntimeBootstrapInFlight)", StringComparison.Ordinal);
            int packageRootIndex = source.IndexOf("var packageRoot = GetPackageRoot(projectRoot);", StringComparison.Ordinal);

            Assert.GreaterOrEqual(activationIndex, 0);
            Assert.GreaterOrEqual(bootstrapIndex, 0);
            Assert.GreaterOrEqual(packageRootIndex, 0);
            Assert.Less(activationIndex, packageRootIndex);
            Assert.Less(bootstrapIndex, packageRootIndex);
        }

        /// <summary>
        /// 验证 Ctrl+E 定位包根时优先使用当前脚本 GUID，避免每次按键都全局扫描 AssetDatabase。
        /// </summary>
        [Test]
        public void LauncherAssetPathUsesGuidFastPathBeforeGlobalSearch()
        {
            string source = ReadLauncherSource();

            StringAssert.Contains("LAUNCHER_SCRIPT_GUID", source);
            Assert.Less(
                source.IndexOf("AssetDatabase.GUIDToAssetPath(LAUNCHER_SCRIPT_GUID)", StringComparison.Ordinal),
                source.IndexOf("AssetDatabase.FindAssets", StringComparison.Ordinal));
        }

        /// <summary>
        /// 通过反射调用启动计划创建方法，让红灯来自功能缺失而不是测试编译失败。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根目录。</param>
        /// <param name="packageRoot">YokiFrame 包根目录。</param>
        /// <returns>启动计划对象。</returns>
        private static object CreateLaunchPlan(string projectRoot, string packageRoot)
        {
            MethodInfo method = GetLauncherType().GetMethod(
                "CreateLaunchPlan",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(string) },
                null);
            Assert.IsNotNull(method);
            return method.Invoke(null, new object[] { projectRoot, packageRoot });
        }

        /// <summary>
        /// 通过反射调用带父窗口句柄的启动计划创建方法，避免测试中真实依赖 Unity 主窗口。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根目录。</param>
        /// <param name="packageRoot">YokiFrame 包根目录。</param>
        /// <param name="parentWindowHandle">模拟的父窗口 HWND。</param>
        /// <returns>启动计划对象。</returns>
        private static object CreateLaunchPlan(string projectRoot, string packageRoot, long parentWindowHandle)
        {
            MethodInfo method = GetLauncherType().GetMethod(
                "CreateLaunchPlan",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(string), typeof(long) },
                null);
            Assert.IsNotNull(method);
            return method.Invoke(null, new object[] { projectRoot, packageRoot, parentWindowHandle });
        }

        /// <summary>
        /// 通过反射调用包根解析方法，确保测试在方法缺失时给出明确红灯。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根目录。</param>
        /// <param name="assetPath">launcher 脚本资源路径。</param>
        /// <returns>解析出的包根绝对路径。</returns>
        private static string ResolvePackageRootFromAssetPath(string projectRoot, string assetPath)
        {
            MethodInfo method = GetLauncherType().GetMethod(
                "ResolvePackageRootFromAssetPath",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method);
            return (string)method.Invoke(null, new object[] { projectRoot, assetPath });
        }

        /// <summary>
        /// 通过反射调用 runtime identifier 解析方法，避免测试依赖实现可见性。
        /// </summary>
        /// <param name="platform">Unity Editor 平台。</param>
        /// <param name="architecture">当前进程架构。</param>
        /// <returns>runtime identifier。</returns>
        private static string GetRuntimeIdentifier(RuntimePlatform platform, Architecture architecture)
        {
            MethodInfo method = GetLauncherType().GetMethod(
                "GetRuntimeIdentifier",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method);
            return (string)method.Invoke(null, new object[] { platform, architecture });
        }

        /// <summary>
        /// 通过反射调用带日志注入的 Launch 方法。
        /// </summary>
        /// <param name="plan">启动计划对象。</param>
        /// <param name="startProcess">模拟进程启动动作。</param>
        /// <param name="logMessage">日志收集动作。</param>
        /// <returns>是否执行了新进程启动。</returns>
        private static bool LaunchWithLog(
            object plan,
            Action<ProcessStartInfo> startProcess,
            Action<string> logMessage)
        {
            MethodInfo method = GetLauncherType().GetMethod(
                "Launch",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[]
                {
                    plan.GetType(),
                    typeof(Action<ProcessStartInfo>),
                    typeof(Action<string>)
                },
                null);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, new object[] { plan, startProcess, logMessage });
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
            string runtimeIdentifier = GetRuntimeIdentifier(Application.platform, RuntimeInformation.ProcessArchitecture);
            Assert.IsFalse(string.IsNullOrWhiteSpace(runtimeIdentifier), "Unsupported Unity Editor Runtime profile.");
            return string.Equals(runtimeIdentifier, "win-x64", StringComparison.Ordinal)
                ? "win-x64-aot"
                : runtimeIdentifier;
        }

        /// <summary>
        /// 读取启动计划公开属性，避免测试直接依赖生产类型定义。
        /// </summary>
        /// <param name="plan">启动计划对象。</param>
        /// <param name="propertyName">属性名。</param>
        /// <returns>属性值。</returns>
        private static object GetProperty(object plan, string propertyName)
        {
            PropertyInfo property = plan.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "Missing launch plan property: " + propertyName);
            return property.GetValue(plan);
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
        /// 读取 Workbench launcher 源码，检查 Unity 启动路径是否保留快速定位策略。
        /// </summary>
        /// <returns>Workbench launcher 源码文本。</returns>
        private static string ReadLauncherSource()
        {
            string path = Path.Combine(
                Application.dataPath,
                "YokiFrame",
                "Core",
                "Adapters",
                "Unity",
                "Editor",
                "WorkbenchLauncher",
                "YokiFrameWorkbenchLauncher.cs");
            Assert.IsTrue(File.Exists(path), path);
            return File.ReadAllText(path);
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
