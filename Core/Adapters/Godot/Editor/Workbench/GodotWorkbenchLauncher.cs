#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// 从项目级 Runtime 缓存解析当前平台 Workbench，并绑定当前 Godot 项目启动。
    /// </summary>
    internal static partial class GodotWorkbenchLauncher
    {
        private const string WINDOWS_MANAGED_RUNTIME_ID = "win-x64";
        private const string WINDOWS_NATIVE_AOT_RUNTIME_ID = "win-x64-aot";

        private const int RUNTIME_CACHE_LAYOUT_VERSION = 1;

        private static readonly string[] sPackageRelativeSegments =
        {
            "addons",
            "yokiframe",
            "package",
            "YokiFrame"
        };

        /// <summary>
        /// 解析并启动当前平台 Workbench，不修改缓存或目标项目；Godot 不在此入口自动执行 bootstrap。
        /// </summary>
        /// <param name="projectRoot">当前 Godot 项目根。</param>
        /// <param name="ownerHandle">Windows Godot 主窗口句柄；其它平台传 0。</param>
        /// <param name="errorMessage">失败时的人类可读诊断。</param>
        /// <returns>成功创建进程时返回进程 ID，否则返回 0。</returns>
        public static int Launch(string projectRoot, long ownerHandle, out string errorMessage)
        {
            try
            {
                var runtimeRoot = ResolveRuntimeRoot(projectRoot);
                var executablePath = ResolveExecutablePath(runtimeRoot);
                var arguments = CreateArguments(projectRoot, ResolvePackageRoot(projectRoot), ownerHandle);
                var processId = OS.CreateProcess(executablePath, arguments, false);
                errorMessage = processId > 0
                    ? string.Empty
                    : "Godot failed to create the Workbench process: " + executablePath;
                return processId;
            }
            catch (Exception exception) when (IsExpectedLaunchFailure(exception))
            {
                errorMessage = exception.Message;
                return 0;
            }
        }

        /// <summary>
        /// 从项目缓存 manifest 读取当前 OS/RID 对应的 GUI entry，并执行路径与存在性校验。
        /// </summary>
        /// <param name="runtimeRoot">受控 WorkbenchRuntime 根。</param>
        /// <returns>可执行文件完整路径。</returns>
        private static string ResolveExecutablePath(string runtimeRoot)
        {
            var manifestPath = Path.Combine(runtimeRoot, "tool-manifest.json");
            var runtimeIds = ResolvePreferredRuntimeIds(ResolveRuntimeId());
            if (!TryValidateRuntimeManifest(
                    manifestPath,
                    runtimeRoot,
                    runtimeIds,
                    out var executablePath,
                    out var error))
            {
                throw new InvalidDataException("Workbench Runtime cache is invalid: " + error);
            }

            return executablePath;
        }

        /// <summary>
        /// 从项目 `.yokiframe/runtime` 的 current.json 解析当前指纹目录；缺失时明确要求用户先手动执行源码 bootstrap。
        /// </summary>
        /// <param name="projectRoot">当前 Godot 项目根。</param>
        /// <returns>当前有效 Runtime 缓存根。</returns>
        private static string ResolveRuntimeRoot(string projectRoot)
        {
            var fullProjectRoot = Path.GetFullPath(projectRoot);
            var pointerPath = YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(fullProjectRoot);
            if (!File.Exists(pointerPath))
            {
                throw new FileNotFoundException(
                    "Workbench Runtime cache is missing. Run YokiFrame bootstrap with --project before opening Workbench.",
                    pointerPath);
            }

            if (HasReparsePointInPath(fullProjectRoot, pointerPath))
            {
                throw new InvalidDataException("Workbench Runtime cache pointer uses a symbolic link or reparse point.");
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(pointerPath));
            var root = document.RootElement;
            if (ReadInt32(root, "layoutVersion") != RUNTIME_CACHE_LAYOUT_VERSION)
            {
                throw new InvalidDataException("Workbench Runtime cache pointer layout is unsupported.");
            }

            var sourceFingerprint = ReadString(root, "sourceFingerprint");
            try
            {
                var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(fullProjectRoot, sourceFingerprint);
                if (!Directory.Exists(runtimeRoot))
                {
                    throw new DirectoryNotFoundException("Workbench Runtime cache directory is missing: " + runtimeRoot);
                }

                if (HasReparsePointInPath(fullProjectRoot, runtimeRoot))
                {
                    throw new InvalidDataException("Workbench Runtime cache uses a symbolic link or reparse point.");
                }

                return runtimeRoot;
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Workbench Runtime cache pointer fingerprint is invalid.", exception);
            }
        }

        /// <summary>
        /// 获取 Godot Installer 投影中的只读 YokiFrame 包根，供 Workbench 在项目缓存外仍可读取文档和包元数据。
        /// </summary>
        /// <param name="projectRoot">当前 Godot 项目根。</param>
        /// <returns>Godot 受管包根完整路径。</returns>
        private static string ResolvePackageRoot(string projectRoot)
        {
            return Path.GetFullPath(Path.Combine(projectRoot, Path.Combine(sPackageRelativeSegments)));
        }

        /// <summary>
        /// 读取对象中的可选字符串属性，缺失或类型不符时返回空文本。
        /// </summary>
        /// <param name="element">目标 JSON 对象。</param>
        /// <param name="propertyName">属性名。</param>
        /// <returns>字符串值或空文本。</returns>
        private static string ReadString(JsonElement element, string propertyName)
        {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? string.Empty
                    : string.Empty;
        }

        /// <summary>
        /// 读取对象中的可选整数属性，缺失或类型不符时返回 0。
        /// </summary>
        /// <param name="element">目标 JSON 对象。</param>
        /// <param name="propertyName">整数属性名称。</param>
        /// <returns>已解析整数；无效时返回 0。</returns>
        private static int ReadInt32(JsonElement element, string propertyName)
        {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(propertyName, out var value)
                && value.TryGetInt32(out var number)
                    ? number
                    : 0;
        }

        /// <summary>
        /// 根据 Godot 当前 OS 与架构解析受支持的 Workbench RID。
        /// </summary>
        /// <returns>当前平台 RID。</returns>
        private static string ResolveRuntimeId()
        {
            var osName = OS.GetName();
            if (osName == "Windows")
            {
                return WINDOWS_MANAGED_RUNTIME_ID;
            }

            if (osName == "Linux")
            {
                return "linux-x64";
            }

            if (osName == "macOS")
            {
                return Engine.GetArchitectureName() == "arm64" ? "osx-arm64" : "osx-x64";
            }

            throw new PlatformNotSupportedException("Workbench is not supported on " + osName + ".");
        }

        /// <summary>
        /// 为当前宿主 RID 生成 Workbench profile 候选列表；Windows 只使用手动 bootstrap 的 Native AOT 缓存。
        /// </summary>
        /// <param name="runtimeId">由当前 Godot 宿主解析出的基础 RID。</param>
        /// <returns>按启动优先级排列的 Runtime profile 标识。</returns>
        private static string[] ResolvePreferredRuntimeIds(string runtimeId)
        {
            return string.Equals(runtimeId, WINDOWS_MANAGED_RUNTIME_ID, StringComparison.Ordinal)
                ? new[] { WINDOWS_NATIVE_AOT_RUNTIME_ID }
                : new[] { runtimeId };
        }

        /// <summary>
        /// 创建绑定当前项目、源码包和可选 Windows owner window 的启动参数。
        /// </summary>
        /// <param name="projectRoot">Godot 项目根。</param>
        /// <param name="packageRoot">当前 Godot 受管包根。</param>
        /// <param name="ownerHandle">Windows owner 句柄。</param>
        /// <returns>Workbench 参数数组。</returns>
        private static string[] CreateArguments(string projectRoot, string packageRoot, long ownerHandle)
        {
            List<string> arguments = new List<string> { "--project", projectRoot, "--source", packageRoot };
            if (OS.GetName() == "Windows" && ownerHandle > 0)
            {
                arguments.Add("--parent-hwnd");
                arguments.Add(ownerHandle.ToString());
            }

            return arguments.ToArray();
        }

        /// <summary>
        /// 识别可转换为用户诊断的 manifest、文件系统和平台失败。
        /// </summary>
        /// <param name="exception">启动异常。</param>
        /// <returns>属于预期可诊断失败时返回 true。</returns>
        private static bool IsExpectedLaunchFailure(Exception exception)
        {
            return exception is IOException
                || exception is UnauthorizedAccessException
                || exception is JsonException
                || exception is ArgumentException
                || exception is PlatformNotSupportedException;
        }
    }
}
#endif
