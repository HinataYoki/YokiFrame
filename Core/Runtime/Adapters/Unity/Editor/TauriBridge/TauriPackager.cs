#if !GODOT
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Tauri 发布打包工具 — 把开发期 release 二进制固化进 package 内 TauriRuntime~。
    ///
    /// 发布场景下用户项目只复制 Assets/YokiFrame，不含仓库根的 TauriEditor 工程，
    /// 也不应要求用户安装 Rust 工具链。框架开发者发版前执行本菜单：
    ///   cargo build --release → 拷贝二进制到 TauriRuntime~ → 随包分发，运行期零工具链。
    ///   前端与文档资源只维护 TauriRuntime~/dist，不在打包时从 YokiFrameTools/TauriEditor/dist 覆盖。
    ///
    /// TauriRuntime~ 的 ~ 后缀使 Unity AssetDatabase 不导入该目录（无 .meta、无编译开销），
    /// 但普通文件夹复制/Git/UPM 分发仍会带上，故是存放二进制产物的理想位置。
    /// </summary>
    public static class TauriPackager
    {
        private const string MENU_ROOT = TauriLauncher.MENU_ROOT;
        private const string MENU_PACKAGE_CURRENT = MENU_ROOT + "/打包/打包当前平台";
        private const string MENU_PACKAGE_WIN_X64 = MENU_ROOT + "/打包/打包 Windows (win-x64)";
        private const string MENU_PACKAGE_MACOS_ARM64 = MENU_ROOT + "/打包/打包 macOS (arm64)";
        private const string MENU_PACKAGE_MACOS_X64 = MENU_ROOT + "/打包/打包 macOS (x64)";
        private const string MENU_PACKAGE_LINUX_X64 = MENU_ROOT + "/打包/打包 Linux (linux-x64)";
        private const string MENU_PACKAGE_ALL = MENU_ROOT + "/打包/打包所有平台";

        [MenuItem(MENU_PACKAGE_CURRENT, false, 120)]
        public static async void PackageCurrentPlatform()
        {
            await PackageForPlatformAsync(TauriLauncher.CurrentRuntimePlatform);
        }

        [MenuItem(MENU_PACKAGE_WIN_X64, false, 121)]
        public static async void PackageWinX64()
        {
            await PackageForCrossPlatformAsync(TauriLauncher.CrossPlatformTarget.WinX64);
        }

        [MenuItem(MENU_PACKAGE_MACOS_ARM64, false, 122)]
        public static async void PackageMacosArm64()
        {
            await PackageForCrossPlatformAsync(TauriLauncher.CrossPlatformTarget.MacosArm64);
        }

        [MenuItem(MENU_PACKAGE_MACOS_X64, false, 123)]
        public static async void PackageMacosX64()
        {
            await PackageForCrossPlatformAsync(TauriLauncher.CrossPlatformTarget.MacosX64);
        }

        [MenuItem(MENU_PACKAGE_LINUX_X64, false, 124)]
        public static async void PackageLinuxX64()
        {
            await PackageForCrossPlatformAsync(TauriLauncher.CrossPlatformTarget.LinuxX64);
        }

        [MenuItem(MENU_PACKAGE_ALL, false, 125)]
        public static async void PackageAllPlatforms()
        {
            if (!Directory.Exists(TauriLauncher.SrcTauriPath))
            {
                EditorUtility.DisplayDialog("打包失败",
                    $"开发期 Tauri 工程不存在: {TauriLauncher.SrcTauriPath}\n" +
                    "本菜单仅供框架开发仓库使用。", "确定");
                return;
            }

            var targets = TauriLauncher.GetBuildableCrossPlatformTargets(TauriLauncher.CurrentRuntimePlatform);
            if (targets.Length == 0)
            {
                EditorUtility.DisplayDialog("打包失败",
                    "当前系统没有可直接打包的 Tauri 原生壳目标。请在对应操作系统或 CI runner 上构建平台产物。",
                    "确定");
                return;
            }

            var total = targets.Length;
            var succeeded = 0;
            var failed = new List<string>();

            for (var i = 0; i < total; i++)
            {
                var target = targets[i];
                var targetName = TauriLauncher.GetCrossPlatformTargetName(target);
                var progress = (float)i / total;
                EditorUtility.DisplayProgressBar("YokiFrame",
                    $"正在打包 {targetName} ({i + 1}/{total})...", progress);

                try
                {
                    var (success, _) = await TauriLauncher.BuildCrossPlatformAsync(target);
                    if (success)
                    {
                        CopyCrossPlatformArtifacts(target);
                        succeeded++;
                    }
                    else
                    {
                        failed.Add(targetName);
                    }
                }
                catch (Exception e)
                {
                    LogKit.Error($"[TauriPackager] 打包 {targetName} 异常: {e.Message}");
                    failed.Add(targetName);
                }
            }

            EditorUtility.ClearProgressBar();

            if (failed.Count == 0)
            {
                EditorUtility.DisplayDialog("打包完成",
                    $"所有平台打包成功。\n产物位置:\n{TauriLauncher.RuntimeDir}\n\n成功: {succeeded}/{total}", "确定");
            }
            else
            {
                var failedList = string.Join("\n", failed);
                EditorUtility.DisplayDialog("打包完成",
                    $"部分平台打包失败。\n成功: {succeeded}/{total}\n失败:\n{failedList}", "确定");
            }
        }

        [MenuItem(MENU_PACKAGE_CURRENT, true)]
        private static bool ValidatePackageCurrent() => Directory.Exists(TauriLauncher.SrcTauriPath);

        [MenuItem(MENU_PACKAGE_WIN_X64, true)]
        private static bool ValidatePackageWinX64() => Directory.Exists(TauriLauncher.SrcTauriPath);

        [MenuItem(MENU_PACKAGE_MACOS_ARM64, true)]
        private static bool ValidatePackageMacosArm64() => Directory.Exists(TauriLauncher.SrcTauriPath);

        [MenuItem(MENU_PACKAGE_MACOS_X64, true)]
        private static bool ValidatePackageMacosX64() => Directory.Exists(TauriLauncher.SrcTauriPath);

        [MenuItem(MENU_PACKAGE_LINUX_X64, true)]
        private static bool ValidatePackageLinuxX64() => Directory.Exists(TauriLauncher.SrcTauriPath);

        [MenuItem(MENU_PACKAGE_ALL, true)]
        private static bool ValidatePackageAll() => Directory.Exists(TauriLauncher.SrcTauriPath);

        /// <summary>打包指定运行平台到 TauriRuntime~。</summary>
        internal static async Task<bool> PackageForPlatformAsync(TauriLauncher.TauriRuntimePlatform platform)
        {
            if (!Directory.Exists(TauriLauncher.SrcTauriPath))
            {
                EditorUtility.DisplayDialog("打包失败",
                    $"开发期 Tauri 工程不存在: {TauriLauncher.SrcTauriPath}\n" +
                    "本菜单仅供框架开发仓库使用。", "确定");
                return false;
            }

            var platformName = TauriLauncher.GetPlatformDisplayName(platform);
            try
            {
                EditorUtility.DisplayProgressBar("YokiFrame", $"正在打包 {platformName}...", 0.4f);
                var (success, _) = await TauriLauncher.BuildForPlatformAsync(platform);
                if (!success)
                {
                    EditorUtility.ClearProgressBar();
                    return false;
                }

                CopyPlatformArtifacts(platform);
                EditorUtility.ClearProgressBar();

                EditorUtility.DisplayDialog("打包完成",
                    $"产物已固化到:\n{TauriLauncher.RuntimeDir}\n\n平台: {platformName}\n可随 package 分发，用户无需 Rust 工具链。",
                    "确定");
                return true;
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                LogKit.Error($"[TauriPackager] 打包 {platformName} 异常: {e.Message}");
                EditorUtility.DisplayDialog("打包失败", $"打包异常 ({platformName}):\n{e.Message}", "确定");
                return false;
            }
        }

        /// <summary>打包跨平台目标到 TauriRuntime~。</summary>
        internal static async Task<bool> PackageForCrossPlatformAsync(TauriLauncher.CrossPlatformTarget target)
        {
            if (!Directory.Exists(TauriLauncher.SrcTauriPath))
            {
                EditorUtility.DisplayDialog("打包失败",
                    $"开发期 Tauri 工程不存在: {TauriLauncher.SrcTauriPath}\n" +
                    "本菜单仅供框架开发仓库使用。", "确定");
                return false;
            }

            var targetName = TauriLauncher.GetCrossPlatformTargetName(target);
            try
            {
                EditorUtility.DisplayProgressBar("YokiFrame", $"正在打包 {targetName}...", 0.4f);
                var (success, _) = await TauriLauncher.BuildCrossPlatformAsync(target);
                if (!success)
                {
                    EditorUtility.ClearProgressBar();
                    return false;
                }

                CopyCrossPlatformArtifacts(target);
                EditorUtility.ClearProgressBar();

                EditorUtility.DisplayDialog("打包完成",
                    $"产物已固化到:\n{TauriLauncher.RuntimeDir}\n\n平台: {targetName}\n可随 package 分发，用户无需 Rust 工具链。",
                    "确定");
                return true;
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                LogKit.Error($"[TauriPackager] 打包 {targetName} 异常: {e.Message}");
                EditorUtility.DisplayDialog("打包失败", $"打包异常 ({targetName}):\n{e.Message}", "确定");
                return false;
            }
        }

        /// <summary>拷贝运行平台二进制到 TauriRuntime~。</summary>
        internal static void CopyPlatformArtifacts(TauriLauncher.TauriRuntimePlatform platform)
        {
            Directory.CreateDirectory(TauriLauncher.RuntimeDir);

            CopyPlatformBinaryArtifacts(platform);
        }

        /// <summary>拷贝跨平台目标二进制到 TauriRuntime~。</summary>
        internal static void CopyCrossPlatformArtifacts(TauriLauncher.CrossPlatformTarget target)
        {
            Directory.CreateDirectory(TauriLauncher.RuntimeDir);

            CopyCrossPlatformBinaryArtifacts(target);
        }

        private static void CopyPlatformBinaryArtifacts(TauriLauncher.TauriRuntimePlatform platform)
        {
            if (platform == TauriLauncher.TauriRuntimePlatform.MacOS)
            {
                var sourceAppPath = TauriLauncher.ResolveDevAppBundlePath(TauriLauncher.SrcTauriPath, platform);
                var targetAppPath = TauriLauncher.ResolvePublishedAppBundlePath(TauriLauncher.RuntimeDir, platform);
                if (!Directory.Exists(sourceAppPath))
                    throw new DirectoryNotFoundException($"macOS app bundle 缺失: {sourceAppPath}");

                if (Directory.Exists(targetAppPath))
                    Directory.Delete(targetAppPath, recursive: true);
                CopyDirectory(sourceAppPath, targetAppPath);
                return;
            }

            if (!File.Exists(TauriLauncher.DevBinaryPath))
                throw new FileNotFoundException($"release 产物缺失: {TauriLauncher.DevBinaryPath}");

            File.Copy(TauriLauncher.DevBinaryPath, TauriLauncher.PublishedBinaryPath, overwrite: true);
        }

        private static void CopyCrossPlatformBinaryArtifacts(TauriLauncher.CrossPlatformTarget target)
        {
            if (target == TauriLauncher.CrossPlatformTarget.MacosArm64 ||
                target == TauriLauncher.CrossPlatformTarget.MacosX64)
            {
                var sourceAppPath = ResolveCrossPlatformAppBundlePath(TauriLauncher.SrcTauriPath, target);
                var targetAppPath = TauriLauncher.ResolvePublishedAppBundlePath(
                    TauriLauncher.RuntimeDir,
                    TauriLauncher.TauriRuntimePlatform.MacOS);
                if (!Directory.Exists(sourceAppPath))
                    throw new DirectoryNotFoundException($"macOS app bundle 缺失: {sourceAppPath}");

                if (Directory.Exists(targetAppPath))
                    Directory.Delete(targetAppPath, recursive: true);
                CopyDirectory(sourceAppPath, targetAppPath);
                return;
            }

            var sourcePath = ResolveCrossPlatformBinaryPath(target);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"跨平台编译产物缺失: {sourcePath}");

            var targetPath = ResolveCrossPlatformPublishedBinaryPath(TauriLauncher.RuntimeDir, target);
            var targetParent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetParent))
                Directory.CreateDirectory(targetParent);
            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        /// <summary>解析跨平台编译产物写入 TauriRuntime~ 后的启动路径。</summary>
        internal static string ResolveCrossPlatformPublishedBinaryPath(
            string runtimeDir,
            TauriLauncher.CrossPlatformTarget target)
        {
            switch (target)
            {
                case TauriLauncher.CrossPlatformTarget.WinX64:
                    return TauriLauncher.ResolvePublishedBinaryPath(
                        runtimeDir,
                        TauriLauncher.TauriRuntimePlatform.Windows);
                case TauriLauncher.CrossPlatformTarget.MacosArm64:
                case TauriLauncher.CrossPlatformTarget.MacosX64:
                    return TauriLauncher.ResolvePublishedBinaryPath(
                        runtimeDir,
                        TauriLauncher.TauriRuntimePlatform.MacOS);
                case TauriLauncher.CrossPlatformTarget.LinuxX64:
                    return TauriLauncher.ResolvePublishedBinaryPath(
                        runtimeDir,
                        TauriLauncher.TauriRuntimePlatform.Linux);
                default:
                    throw new ArgumentOutOfRangeException(nameof(target));
            }
        }

        /// <summary>解析跨平台编译产物路径。</summary>
        internal static string ResolveCrossPlatformBinaryPath(TauriLauncher.CrossPlatformTarget target)
        {
            var targetDir = target switch
            {
                TauriLauncher.CrossPlatformTarget.WinX64 => "x86_64-pc-windows-msvc",
                TauriLauncher.CrossPlatformTarget.MacosArm64 => "aarch64-apple-darwin",
                TauriLauncher.CrossPlatformTarget.MacosX64 => "x86_64-apple-darwin",
                TauriLauncher.CrossPlatformTarget.LinuxX64 => "x86_64-unknown-linux-gnu",
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };

            var releaseDir = Path.Combine(
                TauriLauncher.SrcTauriPath,
                TauriLauncher.CARGO_TARGET_DIR_NAME,
                targetDir,
                "release");

            if (target == TauriLauncher.CrossPlatformTarget.WinX64)
                return Path.Combine(releaseDir, "yokiframe-tauri-editor.exe");

            return Path.Combine(releaseDir, "yokiframe-tauri-editor");
        }

        /// <summary>解析 macOS 跨平台构建生成的完整 .app bundle 路径。</summary>
        internal static string ResolveCrossPlatformAppBundlePath(
            string srcTauriPath,
            TauriLauncher.CrossPlatformTarget target)
        {
            var targetDir = target switch
            {
                TauriLauncher.CrossPlatformTarget.MacosArm64 => "aarch64-apple-darwin",
                TauriLauncher.CrossPlatformTarget.MacosX64 => "x86_64-apple-darwin",
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };

            return Path.Combine(
                srcTauriPath,
                TauriLauncher.CARGO_TARGET_DIR_NAME,
                targetDir,
                "release",
                "bundle",
                "macos",
                "YokiFrame Editor.app");
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);

            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }
}
#endif
