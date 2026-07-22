#if GODOT && TOOLS
using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 解析并约束 `godot-editor` FileBridge 的全部项目内路径。
    /// </summary>
    internal sealed class GodotEditorFileBridgePaths
    {
        /// <summary>
        /// 创建路径集合，并把协议路径限制在目标 Godot 项目根内。
        /// </summary>
        /// <param name="projectRoot">Godot 项目根。</param>
        public GodotEditorFileBridgePaths(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Godot project root is required.", nameof(projectRoot));
            }

            ProjectRoot = Path.GetFullPath(projectRoot);
            if (!Directory.Exists(ProjectRoot))
            {
                throw new DirectoryNotFoundException("Godot project root was not found: " + ProjectRoot);
            }

            EngineRoot = CombineInsideProject(
                YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY,
                YokiFrameFileBridgeLayout.ENGINES_DIRECTORY,
                GodotEditorFileBridgeHost.ENGINE_ID);
            CommandsRoot = CombineInsideEngine(YokiFrameFileBridgeLayout.COMMANDS_DIRECTORY);
            ArchiveRoot = Path.Combine(CommandsRoot, YokiFrameFileBridgeLayout.ARCHIVE_DIRECTORY);
            DeadletterRoot = Path.Combine(CommandsRoot, YokiFrameFileBridgeLayout.DEADLETTER_DIRECTORY);
            ResultsRoot = CombineInsideEngine(YokiFrameFileBridgeLayout.RESULTS_DIRECTORY);
            RegistryPath = CombineInsideEngine(YokiFrameFileBridgeLayout.ENGINE_REGISTRY_FILE_NAME);
            HeartbeatPath = CombineInsideEngine(
                YokiFrameFileBridgeLayout.STATUS_DIRECTORY,
                YokiFrameFileBridgeLayout.HEARTBEAT_FILE_NAME);
        }

        /// <summary>获取规范化 Godot 项目根。</summary>
        public string ProjectRoot { get; }

        /// <summary>获取 `godot-editor` engine 协议根。</summary>
        public string EngineRoot { get; }

        /// <summary>获取待处理命令目录。</summary>
        public string CommandsRoot { get; }

        /// <summary>获取成功命令归档目录。</summary>
        public string ArchiveRoot { get; }

        /// <summary>获取 deadletter 目录。</summary>
        public string DeadletterRoot { get; }

        /// <summary>获取 terminal response 目录。</summary>
        public string ResultsRoot { get; }

        /// <summary>获取 engine registry 路径。</summary>
        public string RegistryPath { get; }

        /// <summary>获取 heartbeat 路径。</summary>
        public string HeartbeatPath { get; }

        /// <summary>
        /// 创建状态发布和命令消费需要的固定协议目录。
        /// </summary>
        public void EnsureDirectories()
        {
            EnsureProtocolPathsAreSafe();
            Directory.CreateDirectory(CommandsRoot);
            Directory.CreateDirectory(ArchiveRoot);
            Directory.CreateDirectory(DeadletterRoot);
            Directory.CreateDirectory(ResultsRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(HeartbeatPath));
        }

        /// <summary>
        /// 获取指定请求的 terminal response 路径。
        /// </summary>
        /// <param name="requestId">安全请求标识。</param>
        /// <returns>response 完整路径。</returns>
        public string GetResponsePath(string requestId)
        {
            EnsureSafeId(requestId, nameof(requestId));
            return Path.Combine(ResultsRoot, requestId + YokiFrameFileBridgeLayout.RESPONSE_FILE_SUFFIX);
        }

        /// <summary>
        /// 获取成功命令归档路径，只保留原命令文件名。
        /// </summary>
        /// <param name="commandPath">原始命令路径。</param>
        /// <returns>archive 完整路径。</returns>
        public string GetArchivePath(string commandPath)
        {
            return Path.Combine(ArchiveRoot, Path.GetFileName(commandPath));
        }

        /// <summary>
        /// 获取 deadletter 诊断路径。
        /// </summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        /// <returns>诊断文件路径。</returns>
        public string GetDeadletterInfoPath(string deadletterId)
        {
            EnsureSafeId(deadletterId, nameof(deadletterId));
            return Path.Combine(DeadletterRoot, deadletterId + "-deadletter.json");
        }

        /// <summary>
        /// 获取 deadletter 原请求证据路径。
        /// </summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        /// <returns>原请求文件路径。</returns>
        public string GetDeadletterRequestPath(string deadletterId)
        {
            EnsureSafeId(deadletterId, nameof(deadletterId));
            return Path.Combine(DeadletterRoot, deadletterId + "-request.json");
        }

        /// <summary>
        /// 组合 engine 根内路径，并复用项目根逃逸检查。
        /// </summary>
        /// <param name="segments">engine 根内路径片段。</param>
        /// <returns>项目内完整路径。</returns>
        private string CombineInsideEngine(params string[] segments)
        {
            var engineRelativePath = Path.GetRelativePath(ProjectRoot, EngineRoot);
            string[] allSegments = new string[segments.Length + 1];
            allSegments[0] = engineRelativePath;
            Array.Copy(segments, 0, allSegments, 1, segments.Length);
            return CombineInsideProject(allSegments);
        }

        /// <summary>
        /// 组合项目内路径并阻止绝对路径、上级目录和前缀碰撞逃逸。
        /// </summary>
        /// <param name="segments">项目内路径片段。</param>
        /// <returns>项目内完整路径。</returns>
        private string CombineInsideProject(params string[] segments)
        {
            var fullPath = Path.GetFullPath(Path.Combine(ProjectRoot, Path.Combine(segments)));
            var relativePath = Path.GetRelativePath(ProjectRoot, fullPath);
            if (Path.IsPathRooted(relativePath)
                || relativePath == ".."
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new IOException("Godot Editor FileBridge path escaped the project root.");
            }

            EnsureNoReparsePoint(ProjectRoot, fullPath);
            return fullPath;
        }

        /// <summary>重新校验全部固定协议路径，阻断 Host 创建后被替换的目录链接。</summary>
        private void EnsureProtocolPathsAreSafe()
        {
            EnsureNoReparsePoint(ProjectRoot, EngineRoot);
            EnsureNoReparsePoint(ProjectRoot, CommandsRoot);
            EnsureNoReparsePoint(ProjectRoot, ArchiveRoot);
            EnsureNoReparsePoint(ProjectRoot, DeadletterRoot);
            EnsureNoReparsePoint(ProjectRoot, ResultsRoot);
            EnsureNoReparsePoint(ProjectRoot, RegistryPath);
            EnsureNoReparsePoint(ProjectRoot, HeartbeatPath);
        }

        /// <summary>拒绝项目根到候选路径的现存组件包含符号链接、Junction 或其它重解析点。</summary>
        private static void EnsureNoReparsePoint(string root, string path)
        {
            var current = root;
            EnsurePathComponentIsNotReparsePoint(current);
            var relativePath = Path.GetRelativePath(root, path);
            foreach (var segment in relativePath.Split(
                         new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         StringSplitOptions.RemoveEmptyEntries))
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
                throw new IOException("Godot Editor FileBridge path contains a symbolic link or junction: " + path);
            }
        }

        /// <summary>
        /// 验证路径标识符合共享 SafeId contract。
        /// </summary>
        /// <param name="value">待验证值。</param>
        /// <param name="parameterName">异常参数名。</param>
        private static void EnsureSafeId(string value, string parameterName)
        {
            if (!YokiFrameSafeIdContract.IsSafeId(value))
            {
                throw new ArgumentException("FileBridge path segment is not a safe ID.", parameterName);
            }
        }
    }
}
#endif
