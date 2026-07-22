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

        /// <summary>
        /// 获取 Unity 项目根目录；FileBridge 所有路径都必须位于该目录内。
        /// </summary>
        /// <returns>项目根目录绝对路径。</returns>
        public static string GetProjectRoot()
        {
            var assetsDirectory = new DirectoryInfo(Application.dataPath);
            return assetsDirectory.Parent != null ? assetsDirectory.Parent.FullName : assetsDirectory.FullName;
        }

        /// <summary>
        /// 获取 `.yokiframe` 根目录。
        /// </summary>
        /// <returns>`.yokiframe` 绝对路径。</returns>
        public static string GetYokiFrameRoot()
        {
            return CombineInsideProject(YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY);
        }

        /// <summary>
        /// 获取当前 Unity Editor engine 根目录。
        /// </summary>
        /// <returns>engine 根目录绝对路径。</returns>
        public static string GetEngineRoot()
        {
            return CombineInsideProject(
                YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY,
                YokiFrameFileBridgeLayout.ENGINES_DIRECTORY,
                ENGINE_ID);
        }

        /// <summary>
        /// 获取待处理命令目录。
        /// </summary>
        /// <returns>commands 目录绝对路径。</returns>
        public static string GetCommandsRoot()
        {
            return EnsureSafeProjectPath(Path.Combine(GetEngineRoot(), YokiFrameFileBridgeLayout.COMMANDS_DIRECTORY));
        }

        /// <summary>
        /// 获取命令归档目录。
        /// </summary>
        /// <returns>archive 目录绝对路径。</returns>
        public static string GetArchiveRoot()
        {
            return EnsureSafeProjectPath(Path.Combine(GetCommandsRoot(), YokiFrameFileBridgeLayout.ARCHIVE_DIRECTORY));
        }

        /// <summary>
        /// 获取命令死信目录。
        /// </summary>
        /// <returns>deadletter 目录绝对路径。</returns>
        public static string GetDeadletterRoot()
        {
            return EnsureSafeProjectPath(Path.Combine(GetCommandsRoot(), YokiFrameFileBridgeLayout.DEADLETTER_DIRECTORY));
        }

        /// <summary>
        /// 获取命令响应目录。
        /// </summary>
        /// <returns>results 目录绝对路径。</returns>
        public static string GetResultsRoot()
        {
            return EnsureSafeProjectPath(Path.Combine(GetEngineRoot(), YokiFrameFileBridgeLayout.RESULTS_DIRECTORY));
        }

        /// <summary>
        /// 获取 engine registry 文件路径。
        /// </summary>
        /// <returns>engine.json 绝对路径。</returns>
        public static string GetEngineRegistryPath()
        {
            return EnsureSafeProjectPath(Path.Combine(GetEngineRoot(), YokiFrameFileBridgeLayout.ENGINE_REGISTRY_FILE_NAME));
        }

        /// <summary>
        /// 获取 heartbeat 文件路径。
        /// </summary>
        /// <returns>heartbeat.json 绝对路径。</returns>
        public static string GetHeartbeatPath()
        {
            return EnsureSafeProjectPath(Path.Combine(
                GetEngineRoot(),
                YokiFrameFileBridgeLayout.STATUS_DIRECTORY,
                YokiFrameFileBridgeLayout.HEARTBEAT_FILE_NAME));
        }

        /// <summary>
        /// 获取指定 snapshot 文件路径。
        /// </summary>
        /// <param name="kit">安全 Kit 标识。</param>
        /// <param name="name">安全 snapshot 名称。</param>
        /// <returns>snapshot 文件绝对路径。</returns>
        public static string GetSnapshotPath(string kit, string name)
        {
            return EnsureSafeProjectPath(Path.Combine(
                GetEngineRoot(),
                YokiFrameFileBridgeLayout.SNAPSHOTS_DIRECTORY,
                kit,
                name + YokiFrameFileBridgeLayout.JSON_EXTENSION));
        }

        /// <summary>
        /// 获取指定请求的 response 文件路径。
        /// </summary>
        /// <param name="requestId">安全请求标识。</param>
        /// <returns>response 文件绝对路径。</returns>
        public static string GetResponsePath(string requestId)
        {
            return EnsureSafeProjectPath(Path.Combine(
                GetResultsRoot(),
                requestId + YokiFrameFileBridgeLayout.RESPONSE_FILE_SUFFIX));
        }

        /// <summary>
        /// 获取归档后的命令文件路径。
        /// </summary>
        /// <param name="commandPath">原始命令文件路径。</param>
        /// <returns>archive 中的目标路径。</returns>
        public static string GetArchivePath(string commandPath)
        {
            return EnsureSafeProjectPath(Path.Combine(GetArchiveRoot(), Path.GetFileName(commandPath)));
        }

        /// <summary>
        /// 获取 deadletter 诊断文件路径。
        /// </summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        /// <returns>deadletter 诊断文件绝对路径。</returns>
        public static string GetDeadletterInfoPath(string deadletterId)
        {
            return EnsureSafeProjectPath(Path.Combine(GetDeadletterRoot(), deadletterId + "-deadletter.json"));
        }

        /// <summary>
        /// 获取 deadletter 原始请求文件路径。
        /// </summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        /// <returns>deadletter 原始请求文件绝对路径。</returns>
        public static string GetDeadletterRequestPath(string deadletterId)
        {
            return EnsureSafeProjectPath(Path.Combine(GetDeadletterRoot(), deadletterId + "-request.json"));
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
            var fullPath = Path.GetFullPath(path);
            var projectRoot = Path.GetFullPath(GetProjectRoot());
            var relativePath = Path.GetRelativePath(projectRoot, fullPath);
            if (Path.IsPathRooted(relativePath)
                || relativePath == ".."
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, System.StringComparison.Ordinal))
            {
                throw new IOException("FileBridge path escaped the Unity project root.");
            }

            EnsureNoReparsePoint(projectRoot, fullPath);
            return fullPath;
        }

        /// <summary>拒绝项目根到候选路径的现存组件包含符号链接、Junction 或其它重解析点。</summary>
        private static void EnsureNoReparsePoint(string root, string path)
        {
            var current = root;
            EnsurePathComponentIsNotReparsePoint(current);
            var relativePath = Path.GetRelativePath(root, path);
            foreach (var segment in relativePath.Split(
                         new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         System.StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                EnsurePathComponentIsNotReparsePoint(current);
            }
        }

        /// <summary>校验单个现存文件系统组件不是重解析点。</summary>
        private static void EnsurePathComponentIsNotReparsePoint(string path)
        {
            if ((File.Exists(path) || Directory.Exists(path))
                && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("FileBridge path contains a symbolic link or junction: " + path);
            }
        }
    }
}

#endif
