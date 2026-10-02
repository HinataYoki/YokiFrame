#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// 读取 Godot 团队共享的编辑器配置。值位于 project.godot 的 `[yokiframe/editor]`，不使用 Unity 目录。
    /// </summary>
    internal static class GodotYokiFrameEditorSettingsFile
    {
        private const string SECTION = "[yokiframe/editor]";

        /// <summary>
        /// 由 Godot Editor 插件进入编辑器时注册叠加实现。
        /// 不使用类型静态构造，避免 Runtime 在插件加载前创建 Store 时错过注册。
        /// </summary>
        internal static void Register()
        {
            GodotYokiFrameEditorSettingsRegistration.Register(TryApplyLogKitEditorSettings);
        }

        /// <summary>
        /// 读取当前 Godot 项目的编辑器配置，并把 LogKit 的编辑器字段叠到已完成 Runtime 解析的 Store。
        /// 文件缺失视为未覆盖；损坏文件只报告诊断，不改动已有 Runtime 值。
        /// </summary>
        /// <param name="store">待叠加的设置 Store。</param>
        /// <param name="errorMessage">文件存在但无法安全解析时的诊断。</param>
        /// <returns>文件缺失或解析成功时返回 true。</returns>
        internal static bool TryApplyLogKitEditorSettings(
            YokiFrameRuntimeSettingsStore store,
            out string errorMessage)
        {
            Dictionary<string, string> values;
            if (!TryRead(out values, out errorMessage)) return false;
            CopyValue(values, store, LogKitSettings.SAVE_LOG_IN_EDITOR_KEY);
            CopyValue(values, store, LogKitSettings.EDITOR_FILE_NAME_KEY);
            return true;
        }

        /// <summary>把一个存在的 LogKit 编辑器字段写入 Store。</summary>
        /// <param name="values">已解析的编辑器配置。</param>
        /// <param name="store">待叠加的设置 Store。</param>
        /// <param name="key">LogKit 编辑器配置键。</param>
        private static void CopyValue(
            Dictionary<string, string> values,
            YokiFrameRuntimeSettingsStore store,
            string key)
        {
            string value;
            if (values.TryGetValue(LogKitSettings.KIT_NAME + "\n" + key, out value))
            {
                store.SetValue(LogKitSettings.KIT_NAME, key, value);
            }
        }

        /// <summary>
        /// 读取当前 Godot 项目的编辑器配置；文件缺失时返回空字典，由调用方继续使用代码默认值。
        /// </summary>
        /// <param name="values">按 kit 与 key 组合索引的字符串值；重复键以后者为准。</param>
        /// <param name="errorMessage">文件存在但无法安全解析时的诊断。</param>
        /// <returns>文件缺失或解析成功时返回 true。</returns>
        private static bool TryRead(out Dictionary<string, string> values, out string errorMessage)
        {
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            string path = ResolvePath();
            if (!File.Exists(path))
            {
                errorMessage = string.Empty;
                return true;
            }

            string project;
            try
            {
                project = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                errorMessage = "YokiFrame Godot Editor settings 读取失败: " + exception.Message;
                return false;
            }

            ParseSection(ExtractSection(project), values);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>截取编辑器 section，避免把运行时 section 的同名键混入编辑器覆盖。</summary>
        private static string ExtractSection(string project)
        {
            string normalized = project.Replace("\r\n", "\n").Replace('\r', '\n');
            int start = normalized.IndexOf(SECTION, StringComparison.Ordinal);
            if (start < 0) return string.Empty;
            int contentStart = start + SECTION.Length;
            int next = normalized.IndexOf("\n[", contentStart, StringComparison.Ordinal);
            return next < 0 ? normalized.Substring(contentStart) : normalized.Substring(contentStart, next - contentStart);
        }

        /// <summary>解析 `kit/key="value"` 行；重复键以后者为准，注释和其它 section 不进入结果。</summary>
        private static void ParseSection(string section, Dictionary<string, string> values)
        {
            string[] lines = section.Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                int separator = line.IndexOf('=');
                int slash = line.IndexOf('/');
                if (separator <= 0 || slash <= 0 || slash >= separator - 1) continue;
                string kit = line.Substring(0, slash).Trim();
                string key = line.Substring(slash + 1, separator - slash - 1).Trim();
                string raw = line.Substring(separator + 1).Trim();
                if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                {
                    raw = raw.Substring(1, raw.Length - 2);
                }

                values[kit + "\n" + key] = raw;
            }
        }

        /// <summary>
        /// 解析固定相对路径，并确认结果仍位于当前 Godot 项目根内。
        /// </summary>
        /// <returns>当前项目编辑器配置绝对路径。</returns>
        private static string ResolvePath()
        {
            string projectRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
            string path = Path.GetFullPath(Path.Combine(projectRoot, "project.godot"));
            string prefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("YokiFrame Godot Editor Settings 路径越出当前项目。");
            }

            return path;
        }
    }
}
#endif
