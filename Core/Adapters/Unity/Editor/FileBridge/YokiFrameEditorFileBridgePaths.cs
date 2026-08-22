#if UNITY_EDITOR

using System.IO;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 解析 Unity Editor 侧 FileBridge 的项目内路径。
    /// </summary>
    internal static class YokiFrameEditorFileBridgePaths
    {
        public const string ENGINE_ID = "unity-editor";

        // 以下固定路径在同一 Editor 会话内不变（Application.dataPath 恒定），首次通过逃逸与重解析点校验后缓存；
        // 域重载会重置静态字段，因而项目目录变更后必然重新计算。全部访问均在 Editor 主线程。
        private static string sProjectRoot;
        private static string sYokiFrameRoot;
        private static string sEngineRoot;
        private static string sCommandsRoot;
        private static string sProcessingRoot;
        private static string sArchiveRoot;
        private static string sDeadletterRoot;
        private static string sResultsRoot;
        private static string sSnapshotsRoot;
        private static string sEngineRegistryPath;
        private static string sHeartbeatPath;

        /// <summary>
        /// 获取 Unity 项目根目录；FileBridge 所有路径都必须位于该目录内。
        /// </summary>
        /// <returns>项目根目录绝对路径。</returns>
        public static string GetProjectRoot()
        {
            if (sProjectRoot == null)
            {
                var assetsDirectory = new DirectoryInfo(Application.dataPath);
                sProjectRoot = Path.GetFullPath(assetsDirectory.Parent != null
                    ? assetsDirectory.Parent.FullName
                    : assetsDirectory.FullName);
            }

            return sProjectRoot;
        }

        /// <summary>
        /// 获取 `.yokiframe` 根目录。
        /// </summary>
        /// <returns>`.yokiframe` 绝对路径。</returns>
        public static string GetYokiFrameRoot()
        {
            if (sYokiFrameRoot == null)
            {
                sYokiFrameRoot = CombineInsideProject(YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY);
            }

            return sYokiFrameRoot;
        }

        /// <summary>
        /// 获取当前 Unity Editor engine 根目录。
        /// </summary>
        /// <returns>engine 根目录绝对路径。</returns>
        public static string GetEngineRoot()
        {
            if (sEngineRoot == null)
            {
                sEngineRoot = CombineInsideProject(
                    YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY,
                    YokiFrameFileBridgeLayout.ENGINES_DIRECTORY,
                    ENGINE_ID);
            }

            return sEngineRoot;
        }

        /// <summary>
        /// 获取待处理命令目录。
        /// </summary>
        /// <returns>commands 目录绝对路径。</returns>
        public static string GetCommandsRoot()
        {
            if (sCommandsRoot == null)
            {
                sCommandsRoot = EnsureSafeProjectPath(
                    Path.Combine(GetEngineRoot(), YokiFrameFileBridgeLayout.COMMANDS_DIRECTORY));
            }

            return sCommandsRoot;
        }

        /// <summary>
        /// 获取命令归档目录。
        /// </summary>
        /// <returns>archive 目录绝对路径。</returns>
        public static string GetArchiveRoot()
        {
            if (sArchiveRoot == null)
            {
                sArchiveRoot = EnsureSafeProjectPath(
                    Path.Combine(GetCommandsRoot(), YokiFrameFileBridgeLayout.ARCHIVE_DIRECTORY));
            }

            return sArchiveRoot;
        }

        /// <summary>
        /// 获取跨进程 command claim 目录。
        /// </summary>
        /// <returns>processing 目录绝对路径。</returns>
        public static string GetProcessingRoot()
        {
            if (sProcessingRoot == null)
            {
                sProcessingRoot = EnsureSafeProjectPath(
                    Path.Combine(GetCommandsRoot(), YokiFrameFileBridgeLayout.PROCESSING_DIRECTORY));
            }

            return sProcessingRoot;
        }

        /// <summary>
        /// 获取命令死信目录。
        /// </summary>
        /// <returns>deadletter 目录绝对路径。</returns>
        public static string GetDeadletterRoot()
        {
            if (sDeadletterRoot == null)
            {
                sDeadletterRoot = EnsureSafeProjectPath(
                    Path.Combine(GetCommandsRoot(), YokiFrameFileBridgeLayout.DEADLETTER_DIRECTORY));
            }

            return sDeadletterRoot;
        }

        /// <summary>
        /// 获取命令响应目录。
        /// </summary>
        /// <returns>results 目录绝对路径。</returns>
        public static string GetResultsRoot()
        {
            if (sResultsRoot == null)
            {
                sResultsRoot = EnsureSafeProjectPath(
                    Path.Combine(GetEngineRoot(), YokiFrameFileBridgeLayout.RESULTS_DIRECTORY));
            }

            return sResultsRoot;
        }

        /// <summary>
        /// 获取 engine registry 文件路径。
        /// </summary>
        /// <returns>engine.json 绝对路径。</returns>
        public static string GetEngineRegistryPath()
        {
            if (sEngineRegistryPath == null)
            {
                sEngineRegistryPath = EnsureSafeProjectPath(
                    Path.Combine(GetEngineRoot(), YokiFrameFileBridgeLayout.ENGINE_REGISTRY_FILE_NAME));
            }

            return sEngineRegistryPath;
        }

        /// <summary>
        /// 获取 heartbeat 文件路径。
        /// </summary>
        /// <returns>heartbeat.json 绝对路径。</returns>
        public static string GetHeartbeatPath()
        {
            if (sHeartbeatPath == null)
            {
                sHeartbeatPath = EnsureSafeProjectPath(Path.Combine(
                    GetEngineRoot(),
                    YokiFrameFileBridgeLayout.STATUS_DIRECTORY,
                    YokiFrameFileBridgeLayout.HEARTBEAT_FILE_NAME));
            }

            return sHeartbeatPath;
        }

        /// <summary>获取同一项目和 unity-editor Host 的 admission 锁路径。</summary>
        public static string GetAdmissionLockPath()
        {
            return EnsureSafeProjectPath(Path.Combine(GetEngineRoot(), "host.lock"));
        }

        /// <summary>
        /// 获取指定 snapshot 文件路径。
        /// </summary>
        /// <param name="kit">安全 Kit 标识。</param>
        /// <param name="name">安全 snapshot 名称。</param>
        /// <returns>snapshot 文件绝对路径。</returns>
        public static string GetSnapshotPath(string kit, string name)
        {
            return EnsureSafePathBelowVerifiedRoot(
                GetSnapshotsRoot(),
                Path.Combine(GetSnapshotsRoot(), kit, name + YokiFrameFileBridgeLayout.JSON_EXTENSION));
        }

        /// <summary>
        /// 获取指定请求的 response 文件路径。
        /// </summary>
        /// <param name="requestId">安全请求标识。</param>
        /// <returns>response 文件绝对路径。</returns>
        public static string GetResponsePath(string requestId)
        {
            return EnsureSafePathBelowVerifiedRoot(
                GetResultsRoot(),
                Path.Combine(GetResultsRoot(), requestId + YokiFrameFileBridgeLayout.RESPONSE_FILE_SUFFIX));
        }

        /// <summary>
        /// 获取归档后的命令文件路径。
        /// </summary>
        /// <param name="commandPath">原始命令文件路径。</param>
        /// <returns>archive 中的目标路径。</returns>
        public static string GetArchivePath(string commandPath)
        {
            return EnsureSafePathBelowVerifiedRoot(
                GetArchiveRoot(),
                Path.Combine(GetArchiveRoot(), Path.GetFileName(commandPath)));
        }

        /// <summary>
        /// 获取 deadletter 诊断文件路径。
        /// </summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        /// <returns>deadletter 诊断文件绝对路径。</returns>
        public static string GetDeadletterInfoPath(string deadletterId)
        {
            return EnsureSafePathBelowVerifiedRoot(
                GetDeadletterRoot(),
                Path.Combine(GetDeadletterRoot(), deadletterId + "-deadletter.json"));
        }

        /// <summary>
        /// 获取 deadletter 原始请求文件路径。
        /// </summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        /// <returns>deadletter 原始请求文件绝对路径。</returns>
        public static string GetDeadletterRequestPath(string deadletterId)
        {
            return EnsureSafePathBelowVerifiedRoot(
                GetDeadletterRoot(),
                Path.Combine(GetDeadletterRoot(), deadletterId + "-request.json"));
        }

        /// <summary>
        /// 复核全部 FileBridge 固定根仍未被替换为符号链接或 Junction。
        /// </summary>
        /// <remarks>
        /// 固定根路径缓存后不再逐个 getter 重走全链，由命令轮询入口与心跳写入前各调用一次本方法维持防护面；
        /// 单轮内的 TOCTOU 窗口与原实现同为 best-effort。
        /// </remarks>
        public static void EnsureBridgeRootsAreSafe()
        {
            var engineRoot = GetEngineRoot();
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(GetProjectRoot(), engineRoot);
            YokiFrameFilePathPolicy.EnsureNoReparsePointBelow(engineRoot, GetProcessingRoot());
            YokiFrameFilePathPolicy.EnsureNoReparsePointBelow(engineRoot, GetArchiveRoot());
            YokiFrameFilePathPolicy.EnsureNoReparsePointBelow(engineRoot, GetDeadletterRoot());
            YokiFrameFilePathPolicy.EnsureNoReparsePointBelow(engineRoot, GetResultsRoot());
            YokiFrameFilePathPolicy.EnsureNoReparsePointBelow(engineRoot, GetSnapshotsRoot());
            YokiFrameFilePathPolicy.EnsureNoReparsePointBelow(engineRoot, GetHeartbeatPath());
            YokiFrameFilePathPolicy.EnsureNoReparsePointBelow(engineRoot, GetAdmissionLockPath());
        }

        /// <summary>获取 snapshot 根目录。</summary>
        /// <returns>snapshots 目录绝对路径。</returns>
        private static string GetSnapshotsRoot()
        {
            if (sSnapshotsRoot == null)
            {
                sSnapshotsRoot = EnsureSafeProjectPath(
                    Path.Combine(GetEngineRoot(), YokiFrameFileBridgeLayout.SNAPSHOTS_DIRECTORY));
            }

            return sSnapshotsRoot;
        }

        /// <summary>
        /// 组合项目内路径，并阻止相对片段逃逸到项目外。
        /// </summary>
        /// <param name="segments">项目内路径片段。</param>
        /// <returns>项目内绝对路径。</returns>
        private static string CombineInsideProject(params string[] segments)
        {
            return EnsureSafeProjectPath(Path.Combine(GetProjectRoot(), Path.Combine(segments)));
        }

        /// <summary>
        /// 校验候选路径仍在项目根内，且现存路径链不含符号链接或 Junction。
        /// </summary>
        /// <param name="path">待返回给 FileBridge IO 的候选路径。</param>
        /// <returns>已规范化且可安全访问的项目内路径。</returns>
        private static string EnsureSafeProjectPath(string path)
        {
            var fullPath = EnsureInsideProject(path);
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(GetProjectRoot(), fullPath);
            return fullPath;
        }

        /// <summary>
        /// 校验动态路径仍在项目根内，并只对已验证根之下的新增组件检查重解析点。
        /// </summary>
        /// <param name="verifiedRoot">同轮已完成全链校验的固定根。</param>
        /// <param name="path">待返回给 FileBridge IO 的候选路径。</param>
        /// <returns>已规范化且可安全访问的项目内路径。</returns>
        private static string EnsureSafePathBelowVerifiedRoot(string verifiedRoot, string path)
        {
            var fullPath = EnsureInsideProject(path);
            YokiFrameFilePathPolicy.EnsureNoReparsePointBelow(verifiedRoot, fullPath);
            return fullPath;
        }

        /// <summary>规范化候选路径并拒绝逃逸到项目根之外。</summary>
        /// <param name="path">候选路径。</param>
        /// <returns>项目内绝对路径。</returns>
        private static string EnsureInsideProject(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var relativePath = Path.GetRelativePath(GetProjectRoot(), fullPath);
            if (Path.IsPathRooted(relativePath)
                || relativePath == ".."
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, System.StringComparison.Ordinal))
            {
                throw new IOException("FileBridge path escaped the Unity project root.");
            }

            return fullPath;
        }

    }
}

#endif
