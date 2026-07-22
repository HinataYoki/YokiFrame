#if UNITY_EDITOR

namespace YokiFrame
{
    /// <summary>
    /// 描述 Unity Editor 启动 Avalonia Workbench 所需的进程参数和诊断信息。
    /// </summary>
    internal sealed class YokiFrameWorkbenchLaunchPlan
    {
        /// <summary>
        /// 创建 Workbench 启动计划；只保存数据，不执行进程启动副作用。
        /// </summary>
        /// <param name="canLaunch">入口文件和参数是否可用于启动。</param>
        /// <param name="executablePath">Workbench 可执行文件绝对路径。</param>
        /// <param name="arguments">传给 Workbench 的命令行参数。</param>
        /// <param name="workingDirectory">进程工作目录。</param>
        /// <param name="errorMessage">无法启动时展示给用户的错误信息。</param>
        /// <param name="evidencePath">用于排查启动问题的证据路径。</param>
        public YokiFrameWorkbenchLaunchPlan(
            bool canLaunch,
            string executablePath,
            string arguments,
            string workingDirectory,
            string errorMessage,
            string evidencePath)
            : this(canLaunch, executablePath, arguments, workingDirectory, errorMessage, evidencePath, false, string.Empty, string.Empty)
        {
        }

        /// <summary>
        /// 创建 Workbench 启动计划，并携带项目级 Runtime bootstrap 信息。
        /// </summary>
        /// <param name="canLaunch">入口文件和参数是否可用于启动。</param>
        /// <param name="executablePath">Workbench 可执行文件绝对路径。</param>
        /// <param name="arguments">传给 Workbench 的命令行参数。</param>
        /// <param name="workingDirectory">进程工作目录。</param>
        /// <param name="errorMessage">无法启动时展示给用户的错误信息。</param>
        /// <param name="evidencePath">用于排查启动问题的证据路径。</param>
        /// <param name="requiresBootstrap">启动前是否需要先生成项目级 Runtime 缓存。</param>
        /// <param name="packageRoot">只读 YokiFrame 源码包根。</param>
        /// <param name="runtimeRoot">当前源码指纹对应的项目级 Runtime 根。</param>
        public YokiFrameWorkbenchLaunchPlan(
            bool canLaunch,
            string executablePath,
            string arguments,
            string workingDirectory,
            string errorMessage,
            string evidencePath,
            bool requiresBootstrap,
            string packageRoot,
            string runtimeRoot)
        {
            CanLaunch = canLaunch;
            ExecutablePath = executablePath;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            ErrorMessage = errorMessage;
            EvidencePath = evidencePath;
            RequiresBootstrap = requiresBootstrap;
            PackageRoot = packageRoot;
            RuntimeRoot = runtimeRoot;
        }

        /// <summary>
        /// 获取入口文件和参数是否可用于启动。
        /// </summary>
        public bool CanLaunch { get; }

        /// <summary>
        /// 获取 Workbench 可执行文件绝对路径。
        /// </summary>
        public string ExecutablePath { get; }

        /// <summary>
        /// 获取传给 Workbench 的命令行参数。
        /// </summary>
        public string Arguments { get; }

        /// <summary>
        /// 获取进程工作目录。
        /// </summary>
        public string WorkingDirectory { get; }

        /// <summary>
        /// 获取无法启动时展示给用户的错误信息。
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 获取用于排查启动问题的证据路径。
        /// </summary>
        public string EvidencePath { get; }

        /// <summary>
        /// 获取启动前是否需要先生成项目级 Runtime 缓存。
        /// </summary>
        public bool RequiresBootstrap { get; }

        /// <summary>
        /// 获取只读 YokiFrame 源码包根；缓存已可用的启动计划仍保留该路径供诊断。
        /// </summary>
        public string PackageRoot { get; }

        /// <summary>
        /// 获取当前源码指纹对应的项目级 Runtime 根；无需 bootstrap 时该目录已经可直接启动。
        /// </summary>
        public string RuntimeRoot { get; }
    }
}

#endif
