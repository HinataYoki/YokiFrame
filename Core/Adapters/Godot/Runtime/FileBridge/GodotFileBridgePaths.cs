#if GODOT && TOOLS
using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 解析并约束 Godot Runtime FileBridge 的项目内路径。
    /// </summary>
    internal sealed class GodotFileBridgePaths : IYokiFrameFileBridgeEnginePaths
    {
        /// <summary>
        /// 创建路径集合，并把所有协议路径限制在目标 Godot 项目根内。
        /// </summary>
        /// <param name="projectRoot">Godot 项目根目录。</param>
        public GodotFileBridgePaths(string projectRoot)
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
                GodotFileBridgeHost.ENGINE_ID);
            CommandsRoot = CombineInsideEngine(YokiFrameFileBridgeLayout.COMMANDS_DIRECTORY);
            ProcessingRoot = Path.Combine(CommandsRoot, YokiFrameFileBridgeLayout.PROCESSING_DIRECTORY);
            ArchiveRoot = Path.Combine(CommandsRoot, YokiFrameFileBridgeLayout.ARCHIVE_DIRECTORY);
            DeadletterRoot = Path.Combine(CommandsRoot, YokiFrameFileBridgeLayout.DEADLETTER_DIRECTORY);
            ResultsRoot = CombineInsideEngine(YokiFrameFileBridgeLayout.RESULTS_DIRECTORY);
            RegistryPath = CombineInsideEngine(YokiFrameFileBridgeLayout.ENGINE_REGISTRY_FILE_NAME);
            HeartbeatPath = CombineInsideEngine(
                YokiFrameFileBridgeLayout.STATUS_DIRECTORY,
                YokiFrameFileBridgeLayout.HEARTBEAT_FILE_NAME);
        }

        /// <summary>
        /// 获取 Godot 项目根绝对路径。
        /// </summary>
        public string ProjectRoot { get; }

        /// <summary>
        /// 获取 godot-runtime engine 协议根。
        /// </summary>
        public string EngineRoot { get; }

        /// <summary>
        /// 获取待处理命令目录。
        /// </summary>
        public string CommandsRoot { get; }

        /// <summary>获取跨进程 command claim 目录。</summary>
        public string ProcessingRoot { get; }

        /// <summary>
        /// 获取命令归档目录。
        /// </summary>
        public string ArchiveRoot { get; }

        /// <summary>
        /// 获取 deadletter 目录。
        /// </summary>
        public string DeadletterRoot { get; }

        /// <summary>
        /// 获取 terminal response 目录。
        /// </summary>
        public string ResultsRoot { get; }

        /// <summary>
        /// 获取 engine.json 路径。
        /// </summary>
        public string RegistryPath { get; }

        /// <summary>
        /// 获取 heartbeat.json 路径。
        /// </summary>
        public string HeartbeatPath { get; }

        /// <summary>获取同一项目和 godot-runtime Host 的 admission 锁路径。</summary>
        public string AdmissionLockPath => Path.Combine(EngineRoot, "host.lock");

        /// <summary>
        /// 创建全部固定协议目录，保证状态发布和命令消费可直接落盘。
        /// </summary>
        public void EnsureDirectories()
        {
            EnsureProtocolPathsAreSafe();
            Directory.CreateDirectory(CommandsRoot);
            Directory.CreateDirectory(ProcessingRoot);
            Directory.CreateDirectory(ArchiveRoot);
            Directory.CreateDirectory(DeadletterRoot);
            Directory.CreateDirectory(ResultsRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(HeartbeatPath));
        }

        /// <summary>
        /// 在每轮命令处理前复核固定协议路径，防止 Host 启动后目录被替换为重解析点。
        /// </summary>
        public void EnsureReady()
        {
            EnsureProtocolPathsAreSafe();
        }

        /// <summary>
        /// 获取指定 Kit 的 state snapshot 路径。
        /// </summary>
        /// <param name="kit">安全 Kit 标识。</param>
        /// <returns>snapshot 完整路径。</returns>
        public string GetSnapshotPath(string kit)
        {
            return GetSnapshotPath(kit, "state");
        }

        /// <summary>
        /// 获取指定 Kit 与 Snapshot 名称的协议路径。
        /// </summary>
        /// <param name="kit">安全 Kit 标识。</param>
        /// <param name="snapshotName">安全 Snapshot 标识。</param>
        /// <returns>snapshot 完整路径。</returns>
        public string GetSnapshotPath(string kit, string snapshotName)
        {
            EnsureSafeId(kit, nameof(kit));
            EnsureSafeId(snapshotName, nameof(snapshotName));
            return CombineInsideEngine(
                YokiFrameFileBridgeLayout.SNAPSHOTS_DIRECTORY,
                kit,
                snapshotName + YokiFrameFileBridgeLayout.JSON_EXTENSION);
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
        /// 获取命令归档路径，仅使用原命令文件名避免目录穿越。
        /// </summary>
        /// <param name="commandPath">原始命令文件路径。</param>
        /// <returns>archive 目标路径。</returns>
        public string GetArchivePath(string commandPath)
        {
            return Path.Combine(ArchiveRoot, Path.GetFileName(commandPath));
        }

        /// <summary>
        /// 获取 deadletter 诊断文件路径。
        /// </summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        /// <returns>诊断文件完整路径。</returns>
        public string GetDeadletterInfoPath(string deadletterId)
        {
            EnsureSafeId(deadletterId, nameof(deadletterId));
            return Path.Combine(DeadletterRoot, deadletterId + "-deadletter.json");
        }

        /// <summary>
        /// 获取 deadletter 原始请求路径。
        /// </summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        /// <returns>原始请求证据路径。</returns>
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
        /// 组合项目内路径，并使用相对路径语义阻止前缀碰撞和上级逃逸。
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
                throw new IOException("Godot FileBridge path escaped the project root.");
            }

            YokiFrameFilePathPolicy.EnsureNoReparsePoint(ProjectRoot, fullPath);
            return fullPath;
        }

        /// <summary>重新校验全部固定协议路径，阻断 Host 创建后被替换的目录链接。</summary>
        private void EnsureProtocolPathsAreSafe()
        {
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(ProjectRoot, EngineRoot);
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(ProjectRoot, CommandsRoot);
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(ProjectRoot, ProcessingRoot);
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(ProjectRoot, ArchiveRoot);
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(ProjectRoot, DeadletterRoot);
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(ProjectRoot, ResultsRoot);
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(ProjectRoot, RegistryPath);
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(ProjectRoot, HeartbeatPath);
            YokiFrameFilePathPolicy.EnsureNoReparsePoint(ProjectRoot, AdmissionLockPath);
        }

        /// <summary>
        /// 验证协议路径片段符合共享 SafeId contract。
        /// </summary>
        /// <param name="value">待验证标识。</param>
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
