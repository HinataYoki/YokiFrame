using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 根据实际参与 Workbench 发布的源码和 MSBuild 输入生成稳定 SHA-256 指纹。
    /// </summary>
    public static class YokiFrameWorkbenchSourceFingerprint
    {
        private const string WORKBENCH_DIRECTORY_NAME = "YokiFrameWorkbench~";
        private const string SOURCE_DIRECTORY_NAME = "src";
        /// <summary>
        /// 计算给定 YokiFrame 包根的 Workbench 构建输入指纹；测试、缓存和二进制不会参与计算。
        /// </summary>
        /// <param name="packageRoot">YokiFrame 源码包根。</param>
        /// <returns>64 位小写 SHA-256 十六进制指纹。</returns>
        public static string Compute(string packageRoot)
        {
            return Compute(packageRoot, CancellationToken.None);
        }

        /// <summary>
        /// 计算给定 YokiFrame 包根的 Workbench 构建输入指纹，并允许调用方按窗口生命周期取消后台扫描。
        /// </summary>
        /// <param name="packageRoot">YokiFrame 源码包根。</param>
        /// <param name="cancellationToken">后台检测生命周期取消令牌。</param>
        /// <returns>64 位小写 SHA-256 十六进制指纹。</returns>
        public static string Compute(string packageRoot, CancellationToken cancellationToken)
        {
            var fullPackageRoot = RequireDirectory(packageRoot, nameof(packageRoot));
            var workbenchRoot = RequireDirectory(
                Path.Combine(fullPackageRoot, WORKBENCH_DIRECTORY_NAME),
                nameof(packageRoot));
            var sourceRoot = RequireDirectory(
                Path.Combine(workbenchRoot, SOURCE_DIRECTORY_NAME),
                nameof(packageRoot));
            List<string> inputPaths = CollectInputPaths(
                fullPackageRoot,
                workbenchRoot,
                sourceRoot,
                cancellationToken);
            using var hash = SHA256.Create();
            foreach (var inputPath in inputPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendText(hash, NormalizeRelativePath(fullPackageRoot, inputPath));
                AppendByte(hash, 0);
                AppendFile(hash, inputPath, cancellationToken);
                AppendByte(hash, 0);
            }

            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return ToLowerHex(hash.Hash ?? Array.Empty<byte>());
        }

        /// <summary>
        /// 收集 Workbench 构建会读取的源码、项目和 MSBuild 文件，并按包相对路径排序保证跨平台稳定。
        /// </summary>
        /// <param name="packageRoot">YokiFrame 包根。</param>
        /// <param name="workbenchRoot">工具链根目录。</param>
        /// <param name="sourceRoot">工具链源码根目录。</param>
        /// <returns>有序、去重后的构建输入完整路径。</returns>
        private static List<string> CollectInputPaths(
            string packageRoot,
            string workbenchRoot,
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            List<string> paths = new();
            AddIfExists(paths, Path.Combine(packageRoot, "Directory.Build.props"));
            AddIfExists(paths, Path.Combine(workbenchRoot, "Directory.Build.props"));
            foreach (var path in EnumerateSourceFiles(sourceRoot, cancellationToken))
            {
                if (IsBuildInput(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort((left, right) => StringComparer.Ordinal.Compare(
                NormalizeRelativePath(packageRoot, left),
                NormalizeRelativePath(packageRoot, right)));
            return paths;
        }

        /// <summary>
        /// 递归枚举源码文件并在进入目录前剪枝 `bin`、`obj` 和重解析点，避免扫描大量生成产物。
        /// </summary>
        /// <param name="sourceRoot">工具链源码根。</param>
        /// <param name="cancellationToken">后台检测生命周期取消令牌。</param>
        /// <returns>可能参与构建的源码文件序列。</returns>
        private static IEnumerable<string> EnumerateSourceFiles(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            Stack<string> pendingDirectories = new();
            pendingDirectories.Push(sourceRoot);
            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = pendingDirectories.Pop();
                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(directory);
                    if (IsGeneratedBuildDirectory(name)
                        || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    pendingDirectories.Push(directory);
                }

                foreach (var path in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return path;
                }
            }
        }

        /// <summary>
        /// 将存在的顶层 MSBuild 输入加入集合；不存在时由具体项目自身报出更准确的构建错误。
        /// </summary>
        /// <param name="paths">待追加集合。</param>
        /// <param name="path">候选文件路径。</param>
        private static void AddIfExists(ICollection<string> paths, string path)
        {
            if (File.Exists(path))
            {
                EnsureNotReparsePoint(path);
                paths.Add(path);
            }
        }

        /// <summary>
        /// 判断文件是否会影响 Workbench、Installer 或 CLI 发布，避免测试、文档和缓存导致无意义重建。
        /// </summary>
        /// <param name="path">候选文件完整路径。</param>
        /// <returns>应参加指纹计算时返回 true。</returns>
        private static bool IsBuildInput(string path)
        {
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".axaml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".props", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".targets", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".resx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".manifest", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".css", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断候选目录是否为 MSBuild 自动生成目录，避免枚举上次构建的中间文件。
        /// </summary>
        /// <param name="directoryName">候选目录名称。</param>
        /// <returns>为 bin 或 obj 目录时返回 true。</returns>
        private static bool IsGeneratedBuildDirectory(string directoryName)
        {
            return string.Equals(directoryName, "bin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(directoryName, "obj", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 把单个 UTF-8 文本片段输入增量 SHA-256，路径分隔符已在调用方统一。
        /// </summary>
        /// <param name="hash">当前哈希器。</param>
        /// <param name="value">待输入文本。</param>
        private static void AppendText(HashAlgorithm hash, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
        }

        /// <summary>
        /// 向增量 SHA-256 写入单个分隔字节，防止相邻路径或文件内容发生拼接歧义。
        /// </summary>
        /// <param name="hash">当前哈希器。</param>
        /// <param name="value">待写入字节。</param>
        private static void AppendByte(HashAlgorithm hash, byte value)
        {
            var bytes = new[] { value };
            hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
        }

        /// <summary>
        /// 以固定缓冲区逐段读取文件，避免把大型资源一次性加载到内存。
        /// </summary>
        /// <param name="hash">当前哈希器。</param>
        /// <param name="path">待输入文件。</param>
        private static void AppendFile(
            HashAlgorithm hash,
            string path,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[81920];
            using var stream = File.OpenRead(path);
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hash.TransformBlock(buffer, 0, read, buffer, 0);
            }
        }

        /// <summary>
        /// 验证目录存在并返回规范化完整路径。
        /// </summary>
        /// <param name="path">待验证目录路径。</param>
        /// <param name="parameterName">异常对应的参数名称。</param>
        /// <returns>规范化完整路径。</returns>
        private static string RequireDirectory(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("YokiFrame package root is required.", parameterName);
            }

            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException("Workbench build input directory was not found: " + fullPath);
            }

            EnsureNotReparsePoint(fullPath);
            return fullPath;
        }

        /// <summary>
        /// 拒绝直接读取的构建输入根或顶层文件是符号链接、Junction 或其它重解析点。
        /// </summary>
        /// <param name="path">待参与指纹计算的现存路径。</param>
        private static void EnsureNotReparsePoint(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Workbench build input must not be a symbolic link or junction: " + path);
            }
        }

        /// <summary>
        /// 将路径转换为包相对正斜杠文本，确保不同宿主系统得到相同指纹。
        /// </summary>
        /// <param name="packageRoot">YokiFrame 包根。</param>
        /// <param name="path">包内文件完整路径。</param>
        /// <returns>正斜杠包相对路径。</returns>
        private static string NormalizeRelativePath(string packageRoot, string path)
        {
            return Path.GetRelativePath(packageRoot, path).Replace('\\', '/');
        }

        /// <summary>
        /// 将哈希字节转换为稳定的小写十六进制文本。
        /// </summary>
        /// <param name="bytes">SHA-256 原始字节。</param>
        /// <returns>小写十六进制文本。</returns>
        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
