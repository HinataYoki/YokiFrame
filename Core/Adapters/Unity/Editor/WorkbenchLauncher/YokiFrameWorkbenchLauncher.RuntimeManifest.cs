#if UNITY_EDITOR

using System;
using System.Globalization;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Workbench 发布路径、启动参数和 manifest 私有模型。
    /// </summary>
    internal static partial class YokiFrameWorkbenchLauncher
    {
        /// <summary>
        /// 把 manifest 中的正斜杠路径转换为当前系统路径。
        /// </summary>
        /// <param name="relativePath">manifest 中的相对路径。</param>
        /// <returns>当前系统可用的相对路径。</returns>
        private static string NormalizeRelativePath(string relativePath)
        {
            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// 创建 Workbench 命令行参数；父窗口句柄存在时附加给 Avalonia 侧设置 owned 顶层窗口 owner。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根目录。</param>
        /// <param name="packageRoot">当前 Workbench 对应的只读源码包根。</param>
        /// <param name="parentWindowHandle">Unity Editor 主窗口 HWND。</param>
        /// <returns>Workbench 命令行参数。</returns>
        private static string CreateWorkbenchArguments(string projectRoot, string packageRoot, long parentWindowHandle)
        {
            var arguments = WORKBENCH_PROJECT_ARGUMENT + " " + QuoteArgument(projectRoot)
                + " " + WORKBENCH_SOURCE_ARGUMENT + " " + QuoteArgument(packageRoot);
            if (parentWindowHandle <= 0L)
            {
                return arguments;
            }

            return arguments + " " + WORKBENCH_PARENT_WINDOW_ARGUMENT + " " + parentWindowHandle.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 为命令行参数添加引号，避免项目路径中的空格截断参数。
        /// </summary>
        /// <param name="argument">原始参数。</param>
        /// <returns>可放入 Arguments 字符串的参数。</returns>
        private static string QuoteArgument(string argument)
        {
            return "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        /// <summary>
        /// 表示 Workbench runtime manifest 的最小字段集合。
        /// </summary>
        [Serializable]
        private sealed class YokiFrameWorkbenchRuntimeManifest
        {
            public int manifestVersion;
            public int layoutVersion;
            public string runtimeRoot = string.Empty;
            public YokiFrameWorkbenchRuntimePlatform[] platforms = Array.Empty<YokiFrameWorkbenchRuntimePlatform>();
        }

        /// <summary>
        /// 表示项目 `.yokiframe/runtime` 当前 Runtime 指针的最小字段集合。
        /// </summary>
        [Serializable]
        private sealed class YokiFrameWorkbenchRuntimePointer
        {
            public int layoutVersion;
            public string sourceFingerprint = string.Empty;
        }

        /// <summary>
        /// 表示 Workbench runtime manifest 中的单个平台发布入口。
        /// </summary>
        [Serializable]
        private sealed class YokiFrameWorkbenchRuntimePlatform
        {
            public string platform = string.Empty;
            public string runtimeIdentifier = string.Empty;
            public bool sharedRuntime;
            public string entrypoint = string.Empty;
            public string guiEntry = string.Empty;
            public string cliEntry = string.Empty;
            public int fileCount;
            public long totalBytes;
            public YokiFrameWorkbenchRuntimeFile[] files = Array.Empty<YokiFrameWorkbenchRuntimeFile>();
        }

        /// <summary>
        /// 表示 Runtime manifest 中的单个发布文件摘要。
        /// </summary>
        [Serializable]
        private sealed class YokiFrameWorkbenchRuntimeFile
        {
            public string relativePath = string.Empty;
            public long sizeBytes;
            public string sha256 = string.Empty;
        }
    }
}

#endif
