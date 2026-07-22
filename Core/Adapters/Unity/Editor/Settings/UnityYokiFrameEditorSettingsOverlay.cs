#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YokiFrame.Unity;

namespace YokiFrame
{
    /// <summary>
    /// 读取当前 Unity 项目的 Editor 专属配置，并通过 Editor-only 端口叠加到当前工具会话。
    /// </summary>
    internal static class UnityYokiFrameEditorSettingsOverlay
    {
        private const string EDITOR_SETTINGS_RELATIVE_PATH =
            "ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json";

        /// <summary>
        /// 在 Unity Editor 程序集加载后安装项目配置实现，Player 编译不会看到该注册。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Register()
        {
            UnityYokiFrameRuntimeSettingsEditorOverlay.Register(TryApply);
        }

        /// <summary>
        /// 读取并校验 Editor 配置，将当前已支持的 LogKit Editor 字段复制到候选 Store。
        /// </summary>
        /// <param name="runtimeStore">已完成 Resources 解析的候选 Store。</param>
        /// <param name="errorMessage">读取或解析失败时的诊断。</param>
        /// <returns>文件缺失或配置成功合并时返回 true。</returns>
        private static bool TryApply(
            YokiFrameRuntimeSettingsStore runtimeStore,
            out string errorMessage)
        {
            string editorSettingsPath = ResolveEditorSettingsPath();
            if (!File.Exists(editorSettingsPath))
            {
                errorMessage = string.Empty;
                return true;
            }

            if (!TryRead(editorSettingsPath, out string json, out errorMessage)) return false;
            if (!UnityYokiFrameRuntimeSettingsLoader.TryParse(
                    json,
                    out var editorStore,
                    out errorMessage)) return false;
            CopySetting(editorStore, runtimeStore, LogKitSettings.SAVE_LOG_IN_EDITOR_KEY);
            CopySetting(editorStore, runtimeStore, LogKitSettings.EDITOR_FILE_NAME_KEY);
            return true;
        }

        /// <summary>
        /// 解析当前项目的固定 Editor 配置路径，并验证结果仍位于 ProjectSettings 内。
        /// </summary>
        /// <returns>当前项目 Editor Settings 绝对路径。</returns>
        private static string ResolveEditorSettingsPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string settingsRoot = Path.GetFullPath(Path.Combine(projectRoot, "ProjectSettings"));
            string editorSettingsPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                EDITOR_SETTINGS_RELATIVE_PATH.Replace('/', Path.DirectorySeparatorChar)));
            string containmentRoot = settingsRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!editorSettingsPath.StartsWith(containmentRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("YokiFrame Editor Settings 路径越出当前项目 ProjectSettings。");
            }

            return editorSettingsPath;
        }

        /// <summary>
        /// 读取 Editor 配置文本，并把 IO 或权限失败转换为可报告结果。
        /// </summary>
        /// <param name="path">Editor 配置绝对路径。</param>
        /// <param name="json">读取成功时返回配置文本。</param>
        /// <param name="errorMessage">读取失败时的诊断。</param>
        /// <returns>读取成功时返回 true。</returns>
        private static bool TryRead(string path, out string json, out string errorMessage)
        {
            try
            {
                json = File.ReadAllText(path);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                json = string.Empty;
                errorMessage = "YokiFrame Editor settings 读取失败: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 把存在的单个 Editor 字段复制到当前工具会话 Store。
        /// </summary>
        /// <param name="source">Editor 配置 Store。</param>
        /// <param name="destination">Runtime 与 Editor 组合后的目标 Store。</param>
        /// <param name="key">待复制的 LogKit Editor 配置键。</param>
        private static void CopySetting(
            YokiFrameRuntimeSettingsStore source,
            YokiFrameRuntimeSettingsStore destination,
            string key)
        {
            if (source.TryGetValue(LogKitSettings.KIT_NAME, key, out string value))
            {
                destination.SetValue(LogKitSettings.KIT_NAME, key, value);
            }
        }
    }
}
#endif
