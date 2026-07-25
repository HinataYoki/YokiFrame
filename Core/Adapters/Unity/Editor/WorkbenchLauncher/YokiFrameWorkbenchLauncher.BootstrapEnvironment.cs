#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Workbench Runtime bootstrap 缺失编译环境的诊断和官方安装入口。
    /// </summary>
    internal static partial class YokiFrameWorkbenchLauncher
    {
        private const string OPEN_MISSING_BOOTSTRAP_ENVIRONMENT_MENU_PATH = "YokiFrame/Workbench/打开缺失的编译环境";
        private const string DOTNET_10_SDK_DOWNLOAD_URL = "https://dotnet.microsoft.com/download/dotnet/10.0";
        private const string VISUAL_STUDIO_BUILD_TOOLS_DOWNLOAD_URL = "https://visualstudio.microsoft.com/visual-cpp-build-tools/";

        private static RuntimeBootstrapEnvironment sMissingBootstrapEnvironment;

        /// <summary>
        /// 从最近一次 bootstrap 失败中识别出的本机编译环境类别。
        /// </summary>
        private enum RuntimeBootstrapEnvironment
        {
            None,
            Dotnet10Sdk,
            VisualStudioCppBuildTools
        }

        /// <summary>
        /// 打开最近一次 bootstrap 失败所缺少编译环境的官方下载页。
        /// </summary>
        [MenuItem(OPEN_MISSING_BOOTSTRAP_ENVIRONMENT_MENU_PATH)]
        private static void OpenMissingRuntimeBootstrapEnvironment()
        {
            var environment = sMissingBootstrapEnvironment;
            if (environment == RuntimeBootstrapEnvironment.None)
            {
                Debug.LogWarning(LOG_PREFIX + "尚未识别出缺失的 Workbench bootstrap 编译环境。");
                return;
            }

            Application.OpenURL(GetRuntimeBootstrapEnvironmentUrl(environment));
            Debug.Log(LOG_PREFIX + "正在打开缺失的编译环境：" + GetRuntimeBootstrapEnvironmentName(environment));
        }

        /// <summary>
        /// 仅在最近一次失败已识别出缺失环境时启用菜单，避免打开与实际错误无关的下载页。
        /// </summary>
        /// <returns>已识别缺失环境时返回 true。</returns>
        [MenuItem(OPEN_MISSING_BOOTSTRAP_ENVIRONMENT_MENU_PATH, true)]
        private static bool ValidateOpenMissingRuntimeBootstrapEnvironment()
        {
            return sMissingBootstrapEnvironment != RuntimeBootstrapEnvironment.None;
        }

        /// <summary>
        /// 将 bootstrap 原始输出转化为包含缺失环境、菜单入口和原始日志的 Console 错误文本。
        /// </summary>
        /// <param name="bootstrapOutput">dotnet bootstrap 合并后的标准输出和标准错误。</param>
        /// <returns>适合 Unity Console 展示的失败信息。</returns>
        private static string CreateRuntimeBootstrapFailureMessage(string bootstrapOutput)
        {
            var environment = DetectMissingRuntimeBootstrapEnvironment(bootstrapOutput);
            sMissingBootstrapEnvironment = environment;
            var output = string.IsNullOrWhiteSpace(bootstrapOutput)
                ? "No process output was captured."
                : bootstrapOutput;
            if (environment == RuntimeBootstrapEnvironment.None)
            {
                return "项目 Runtime bootstrap 失败。未识别出缺失的编译环境，构建日志如下：" + Environment.NewLine + output;
            }

            return "项目 Runtime bootstrap 失败。缺少 "
                + GetRuntimeBootstrapEnvironmentName(environment)
                + "。安装后再次按 Ctrl+E。可从 `"
                + OPEN_MISSING_BOOTSTRAP_ENVIRONMENT_MENU_PATH
                + "` 打开官方下载页。构建日志如下："
                + Environment.NewLine
                + output;
        }

        /// <summary>
        /// 根据 dotnet 与 Native AOT 的稳定错误片段识别缺失的本机构建环境。
        /// </summary>
        /// <param name="bootstrapOutput">dotnet bootstrap 合并后的标准输出和标准错误。</param>
        /// <returns>已识别的缺失环境；无法确认时返回 None。</returns>
        private static RuntimeBootstrapEnvironment DetectMissingRuntimeBootstrapEnvironment(string bootstrapOutput)
        {
            if (ContainsBootstrapOutput(bootstrapOutput, "Platform linker not found")
                || ContainsBootstrapOutput(bootstrapOutput, "Visual Studio 2022")
                || ContainsBootstrapOutput(bootstrapOutput, "Microsoft.VisualStudio.Component.VC.Tools.x86.x64")
                || ContainsBootstrapOutput(bootstrapOutput, "cl.exe\" is not recognized")
                || ContainsBootstrapOutput(bootstrapOutput, "cl.exe\" 不是内部或外部命令"))
            {
                return RuntimeBootstrapEnvironment.VisualStudioCppBuildTools;
            }

            if (ContainsBootstrapOutput(bootstrapOutput, "Unable to start dotnet")
                || ContainsBootstrapOutput(bootstrapOutput, "dotnet is not recognized")
                || ContainsBootstrapOutput(bootstrapOutput, "dotnet\" is not recognized")
                || ContainsBootstrapOutput(bootstrapOutput, "dotnet\" 不是内部或外部命令")
                || ContainsBootstrapOutput(bootstrapOutput, "No .NET SDKs were found")
                || ContainsBootstrapOutput(bootstrapOutput, "A compatible .NET SDK was not found")
                || ContainsBootstrapOutput(bootstrapOutput, "NETSDK1045")
                || ContainsBootstrapOutput(bootstrapOutput, "does not support targeting .NET 10.0"))
            {
                return RuntimeBootstrapEnvironment.Dotnet10Sdk;
            }

            return RuntimeBootstrapEnvironment.None;
        }

        /// <summary>
        /// 对 bootstrap 输出执行不区分大小写的稳定片段匹配。
        /// </summary>
        /// <param name="bootstrapOutput">待检测的原始输出。</param>
        /// <param name="fragment">环境缺失对应的稳定错误片段。</param>
        /// <returns>输出包含目标片段时返回 true。</returns>
        private static bool ContainsBootstrapOutput(string bootstrapOutput, string fragment)
        {
            return !string.IsNullOrWhiteSpace(bootstrapOutput)
                && bootstrapOutput.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 获取指定缺失环境的用户可读名称。
        /// </summary>
        /// <param name="environment">已识别的缺失环境。</param>
        /// <returns>用于 Console 与菜单日志的环境名称。</returns>
        private static string GetRuntimeBootstrapEnvironmentName(RuntimeBootstrapEnvironment environment)
        {
            return environment == RuntimeBootstrapEnvironment.VisualStudioCppBuildTools
                ? "Visual Studio 2022 C++ Build Tools"
                : ".NET 10 SDK";
        }

        /// <summary>
        /// 获取指定缺失环境的官方下载页。
        /// </summary>
        /// <param name="environment">已识别的缺失环境。</param>
        /// <returns>可由 Unity 浏览器入口打开的 HTTPS 地址。</returns>
        private static string GetRuntimeBootstrapEnvironmentUrl(RuntimeBootstrapEnvironment environment)
        {
            return environment == RuntimeBootstrapEnvironment.VisualStudioCppBuildTools
                ? VISUAL_STUDIO_BUILD_TOOLS_DOWNLOAD_URL
                : DOTNET_10_SDK_DOWNLOAD_URL;
        }
    }
}

#endif
