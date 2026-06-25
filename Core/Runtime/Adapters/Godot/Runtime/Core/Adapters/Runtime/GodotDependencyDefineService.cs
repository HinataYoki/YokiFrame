#if GODOT
using System;
using System.IO;
using Godot;

namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot 侧可选依赖宏定义服务。
    /// Godot C# 没有 Unity PlayerSettings，因此这里通过项目根 Directory.Build.props 维护同名编译宏。
    /// </summary>
    public static class GodotDependencyDefineService
    {
        /// <summary>
        /// Luban 可选依赖编译宏。
        /// </summary>
        public const string LUBAN_SUPPORT_DEFINE = "YOKIFRAME_LUBAN_SUPPORT";

        /// <summary>
        /// Luban 可选依赖编译宏。
        /// </summary>
        public static string LubanSupportDefine => LUBAN_SUPPORT_DEFINE;

        private const string PROPS_FILE_NAME = "Directory.Build.props";
        private const string LUBAN_RUNTIME_ASSEMBLY_NAME = "Luban.Runtime";

        /// <summary>
        /// 刷新 Godot 项目根 Directory.Build.props 中的 YokiFrame 可选依赖编译宏。
        /// </summary>
        /// <returns>宏定义文件发生变化时返回 true，否则返回 false。</returns>
        public static bool RefreshDefines()
        {
            var projectRoot = ResolveProjectRoot();
            var lubanAvailable = DetectLubanEnvironment(projectRoot);
            return SetDefine(projectRoot, LUBAN_SUPPORT_DEFINE, lubanAvailable);
        }

        /// <summary>
        /// 获取当前 Godot 项目是否可访问 Luban Runtime。
        /// </summary>
        /// <returns>检测到 Luban Runtime 时返回 true，否则返回 false。</returns>
        public static bool IsLubanEnvironmentAvailable()
        {
            return DetectLubanEnvironment(ResolveProjectRoot());
        }

        private static string ResolveProjectRoot()
        {
            return Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        }

        private static bool DetectLubanEnvironment(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                return false;

            return ProjectFilesReferenceLuban(projectRoot) || LubanRuntimeDllExists(projectRoot);
        }

        private static bool ProjectFilesReferenceLuban(string projectRoot)
        {
            if (FilesContain(projectRoot, "*.csproj", LUBAN_RUNTIME_ASSEMBLY_NAME))
                return true;

            return FilesContain(projectRoot, "*.props", LUBAN_RUNTIME_ASSEMBLY_NAME);
        }

        private static bool FilesContain(string directory, string pattern, string text)
        {
            var files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
            for (var i = 0; i < files.Length; i++)
            {
                try
                {
                    if (File.ReadAllText(files[i]).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                catch
                {
                    // 个别项目文件可能被外部工具占用，跳过后继续检测其它入口。
                }
            }

            return false;
        }

        private static bool LubanRuntimeDllExists(string projectRoot)
        {
            try
            {
                var files = Directory.GetFiles(projectRoot, LUBAN_RUNTIME_ASSEMBLY_NAME + ".dll", SearchOption.AllDirectories);
                return files.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool SetDefine(string projectRoot, string define, bool enabled)
        {
            var propsPath = Path.Combine(projectRoot, PROPS_FILE_NAME);
            var content = File.Exists(propsPath) ? File.ReadAllText(propsPath) : BuildEmptyProps();
            var newContent = enabled ? AddDefine(content, define) : RemoveDefine(content, define);

            if (string.Equals(content, newContent, StringComparison.Ordinal))
                return false;

            File.WriteAllText(propsPath, newContent);
            return true;
        }

        private static string BuildEmptyProps()
        {
            return "<Project>" + System.Environment.NewLine +
                   "</Project>" + System.Environment.NewLine;
        }

        private static string AddDefine(string content, string define)
        {
            if (content.IndexOf(define, StringComparison.Ordinal) >= 0)
                return content;

            const string CLOSE_DEFINE_CONSTANTS = "</DefineConstants>";
            var defineEnd = content.IndexOf(CLOSE_DEFINE_CONSTANTS, StringComparison.Ordinal);
            if (defineEnd >= 0)
            {
                return content.Substring(0, defineEnd) + ";" + define + content.Substring(defineEnd);
            }

            const string CLOSE_PROJECT = "</Project>";
            var projectEnd = content.LastIndexOf(CLOSE_PROJECT, StringComparison.Ordinal);
            var block =
                "  <PropertyGroup>" + System.Environment.NewLine +
                "    <DefineConstants>$(DefineConstants);" + define + "</DefineConstants>" + System.Environment.NewLine +
                "  </PropertyGroup>" + System.Environment.NewLine;

            if (projectEnd < 0)
            {
                return "<Project>" + System.Environment.NewLine + block + "</Project>" + System.Environment.NewLine;
            }

            return content.Substring(0, projectEnd) + block + content.Substring(projectEnd);
        }

        private static string RemoveDefine(string content, string define)
        {
            return content
                .Replace(";" + define, string.Empty)
                .Replace(define + ";", string.Empty)
                .Replace(define, string.Empty);
        }
    }
}
#endif
