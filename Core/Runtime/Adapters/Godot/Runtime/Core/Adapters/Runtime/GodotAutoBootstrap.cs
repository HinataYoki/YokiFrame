#if GODOT
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Godot;

namespace YokiFrame.Godot
{
    /// <summary>
    /// 在 Godot 编辑器加载 C# 程序集时自动注册 YokiFrame 的 autoload 和编辑器入口，
    /// 让用户把 YokiFrame 文件夹复制进项目后，无需手动拖节点或理解 Godot 插件结构。
    /// </summary>
    internal static class GodotAutoBootstrap
    {
        private const string AUTOLOAD_NAME = "YokiFrameGodotBootstrap";
        private const string AUTOLOAD_KEY = "autoload/" + AUTOLOAD_NAME;
        private const string PACKAGE_ROOT_KEY = "yokiframe/package_root";
        private const string BOOTSTRAP_FILE_NAME = "GodotBootstrap.cs";
        private const string BOOTSTRAP_RELATIVE_SUFFIX = "/Core/Runtime/Adapters/Godot/Runtime/Core/Adapters/Runtime/GodotBootstrap.cs";
        private const string INSTALLER_PACKAGE_BOOTSTRAP_SUFFIX = "/addons/yokiframe/package/YokiFrame/Core/Runtime/Adapters/Godot/Runtime/Core/Adapters/Runtime/GodotBootstrap.cs";
        private const string GODOT_EDITOR_PLUGIN_NAME = "yokiframe";
        private const string GODOT_EDITOR_PLUGIN_DIR = "addons/yokiframe";
        private const string GODOT_EDITOR_PLUGIN_SCRIPT_NAME = "plugin.gd";
        private const string GODOT_EDITOR_PLUGIN_REAL_SCRIPT_SUFFIX = "/Core/Runtime/Adapters/Godot/Editor/addons/yokiframe/plugin.gd";
        private const string INSTALLER_GODOT_EDITOR_PLUGIN_PATH = "res://addons/yokiframe/package/YokiFrame/Core/Runtime/Adapters/Godot/Editor/addons/yokiframe/plugin.gd";
        private const string GODOT_EDITOR_PLUGIN_CONFIG_PATH = "res://addons/yokiframe/plugin.cfg";
        private const string EDITOR_PLUGINS_ENABLED_KEY = "editor_plugins/enabled";

#pragma warning disable CA2255
        [ModuleInitializer]
        internal static void Initialize()
        {
            EnsureAutoloadRegistered();
        }
#pragma warning restore CA2255

        internal static void EnsureAutoloadRegistered()
        {
            try
            {
                if (!Engine.IsEditorHint())
                    return;

                var bootstrapScriptPath = FindBootstrapScriptPath();
                if (string.IsNullOrEmpty(bootstrapScriptPath))
                    return;

                var changed = false;
                var packageRoot = GetPackageRootFromBootstrapPath(bootstrapScriptPath);
                if (!string.IsNullOrEmpty(packageRoot))
                    changed |= EnsureProjectSetting(PACKAGE_ROOT_KEY, packageRoot);

                changed |= EnsureProjectSetting(AUTOLOAD_KEY, "*" + bootstrapScriptPath);
                if (changed)
                {
                    var saveResult = ProjectSettings.Save();
                    if (saveResult == Error.Ok)
                    {
                        GD.Print("[YokiFrame] 已自动注册 Godot autoload: " + bootstrapScriptPath);
                    }
                    else
                    {
                        GD.PushWarning("[YokiFrame] 自动注册 autoload 失败，ProjectSettings.Save() 返回: " + saveResult);
                    }
                }

                EnsureGodotEditorPluginInstalled(packageRoot);
            }
            catch (Exception exception)
            {
                GD.PushWarning("[YokiFrame] 自动注册 Godot autoload 失败: " + exception.Message);
            }
        }

        private static bool EnsureProjectSetting(string key, string value)
        {
            if (ProjectSettings.HasSetting(key))
            {
                var currentValue = ProjectSettings.GetSetting(key).ToString();
                if (string.Equals(currentValue, value, StringComparison.Ordinal))
                    return false;
            }

            ProjectSettings.SetSetting(key, value);
            return true;
        }

        private static string FindBootstrapScriptPath()
        {
            var projectRoot = ProjectSettings.GlobalizePath("res://");
            if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                return null;

            var bootstrapFiles = Directory.GetFiles(projectRoot, BOOTSTRAP_FILE_NAME, SearchOption.AllDirectories);
            if (bootstrapFiles.Length == 0)
                return null;

            var preferredFile = ChoosePreferredBootstrapFile(bootstrapFiles);
            if (string.IsNullOrEmpty(preferredFile))
                return null;

            return ToResPath(projectRoot, preferredFile);
        }

        private static string ChoosePreferredBootstrapFile(string[] bootstrapFiles)
        {
            for (var i = 0; i < bootstrapFiles.Length; i++)
            {
                var normalizedPath = NormalizePath(bootstrapFiles[i]);
                if (normalizedPath.EndsWith(INSTALLER_PACKAGE_BOOTSTRAP_SUFFIX, StringComparison.OrdinalIgnoreCase))
                    return bootstrapFiles[i];
            }

            for (var i = 0; i < bootstrapFiles.Length; i++)
            {
                var normalizedPath = NormalizePath(bootstrapFiles[i]);
                if (normalizedPath.EndsWith(BOOTSTRAP_RELATIVE_SUFFIX, StringComparison.OrdinalIgnoreCase))
                    return bootstrapFiles[i];
            }

            return bootstrapFiles[0];
        }

        private static string ToResPath(string projectRoot, string absolutePath)
        {
            var normalizedRoot = NormalizePath(projectRoot).TrimEnd('/');
            var normalizedAbsolutePath = NormalizePath(absolutePath);
            if (!normalizedAbsolutePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return null;

            var relativePath = normalizedAbsolutePath.Substring(normalizedRoot.Length).TrimStart('/');
            return "res://" + relativePath;
        }

        private static string GetPackageRootFromBootstrapPath(string bootstrapScriptPath)
        {
            var normalizedPath = NormalizePath(bootstrapScriptPath);
            var suffixIndex = normalizedPath.LastIndexOf(BOOTSTRAP_RELATIVE_SUFFIX, StringComparison.OrdinalIgnoreCase);
            if (suffixIndex <= 0)
                return string.Empty;

            return normalizedPath.Substring(0, suffixIndex);
        }

        private static void EnsureGodotEditorPluginInstalled(string packageRoot)
        {
            if (string.IsNullOrEmpty(packageRoot))
                return;

            var targetDir = ProjectSettings.GlobalizePath("res://" + GODOT_EDITOR_PLUGIN_DIR);
            Directory.CreateDirectory(targetDir);

            WriteFileIfChanged(Path.Combine(targetDir, "plugin.cfg"), BuildGodotEditorPluginConfig());
            WriteFileIfChanged(Path.Combine(targetDir, GODOT_EDITOR_PLUGIN_SCRIPT_NAME), BuildGodotEditorPluginStub(packageRoot));

            RefreshGodotEditorPluginFiles();

            if (EnsureGodotEditorPluginEnabledSetting())
            {
                var saveResult = ProjectSettings.Save();
                if (saveResult == Error.Ok)
                {
                    GD.Print("[YokiFrame] 已登记 Godot 编辑器插件: " + GODOT_EDITOR_PLUGIN_DIR);
                }
                else
                {
                    GD.PushWarning("[YokiFrame] 登记 Godot 编辑器插件失败，ProjectSettings.Save() 返回: " + saveResult);
                }
            }
        }

        private static void RefreshGodotEditorPluginFiles()
        {
            var editorInterface = EditorInterface.Singleton;
            if (editorInterface == null)
                return;

            var fileSystem = editorInterface.GetResourceFilesystem();
            if (fileSystem == null)
                return;

            fileSystem.UpdateFile("res://" + GODOT_EDITOR_PLUGIN_DIR + "/plugin.cfg");
            fileSystem.UpdateFile("res://" + GODOT_EDITOR_PLUGIN_DIR + "/" + GODOT_EDITOR_PLUGIN_SCRIPT_NAME);
        }

        private static string BuildGodotEditorPluginConfig()
        {
            return "[plugin]\n"
                   + "\n"
                   + "name=\"YokiFrame\"\n"
                   + "description=\"YokiFrame editor panel launcher\"\n"
                   + "author=\"YokiFrame\"\n"
                   + "version=\"2.0.0\"\n"
                   + "script=\"" + GODOT_EDITOR_PLUGIN_SCRIPT_NAME + "\"\n";
        }

        private static string BuildGodotEditorPluginStub(string packageRoot)
        {
            var normalizedRoot = NormalizePath(packageRoot).TrimEnd('/');
            var realPluginPath = normalizedRoot.EndsWith("/addons/yokiframe/package/YokiFrame", StringComparison.OrdinalIgnoreCase)
                ? INSTALLER_GODOT_EDITOR_PLUGIN_PATH
                : normalizedRoot + GODOT_EDITOR_PLUGIN_REAL_SCRIPT_SUFFIX;
            return "@tool\n"
                   + "extends \"" + EscapeGodotString(realPluginPath) + "\"\n";
        }

        private static bool EnsureGodotEditorPluginEnabledSetting()
        {
            var enabledPlugins = Array.Empty<string>();
            if (ProjectSettings.HasSetting(EDITOR_PLUGINS_ENABLED_KEY))
            {
                try
                {
                    enabledPlugins = ProjectSettings.GetSetting(EDITOR_PLUGINS_ENABLED_KEY, Array.Empty<string>()).AsStringArray();
                }
                catch
                {
                    enabledPlugins = Array.Empty<string>();
                }
            }

            for (var i = 0; i < enabledPlugins.Length; i++)
            {
                if (string.Equals(enabledPlugins[i], GODOT_EDITOR_PLUGIN_CONFIG_PATH, StringComparison.Ordinal))
                    return false;
            }

            var nextPlugins = new string[enabledPlugins.Length + 1];
            Array.Copy(enabledPlugins, nextPlugins, enabledPlugins.Length);
            nextPlugins[nextPlugins.Length - 1] = GODOT_EDITOR_PLUGIN_CONFIG_PATH;
            ProjectSettings.SetSetting(EDITOR_PLUGINS_ENABLED_KEY, nextPlugins);
            return true;
        }

        private static void WriteFileIfChanged(string targetPath, string content)
        {
            if (File.Exists(targetPath))
            {
                var targetText = File.ReadAllText(targetPath);
                if (string.Equals(content, targetText, StringComparison.Ordinal))
                    return;
            }

            File.WriteAllText(targetPath, content);
        }

        private static string EscapeGodotString(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
#endif
