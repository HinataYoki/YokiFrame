#if !GODOT
using System;
using System.IO;
using Process = System.Diagnostics.Process;
#if UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif
using Unity.CodeEditor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace YokiFrame.Unity
{
    internal static partial class UnityCommandBridgeHost
    {
        internal static Func<string, int, int, bool> CodeEditorProjectOpener = DefaultCodeEditorProjectOpener;
        internal static Func<string, int, bool> ExternalCodeLocationOpener = InternalEditorUtility.OpenFileAtLineExternal;
        internal static Action ExternalEditorFocusRequester = FocusExternalScriptEditorWindow;
        internal static Action<Action> ExternalEditorFocusScheduler = static action => EditorApplication.delayCall += () => action();

        internal static string OpenCodeLocationWithDefaultEditor(string filePath, int line)
        {
            var normalizedPath = filePath ?? string.Empty;
            if (string.IsNullOrEmpty(normalizedPath))
                throw new ArgumentException("filePath is empty", nameof(filePath));

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Cannot resolve Unity project root");

            var fullPath = ResolveCodeLocationPath(projectRoot, normalizedPath);
            var rootPath = Path.GetFullPath(projectRoot);
            if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("code location is outside Unity project root");

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("code location file does not exist", fullPath);

            var clampedLine = line > 0 ? line : 1;
            var openedByCurrentEditor = TryOpenCodeLocationWithCurrentEditor(fullPath, clampedLine);
            if (!openedByCurrentEditor && !TryOpenCodeLocationWithUnityFallback(fullPath, clampedLine))
                throw new InvalidOperationException("Unity failed to open code location with the configured external editor");

            RequestExternalEditorFocus();
            return BuildOpenCodeLocationResponse(normalizedPath, clampedLine, openedByCurrentEditor);
        }

        internal static bool TryOpenCodeLocationWithCurrentEditor(string fullPath, int line)
        {
            var opener = CodeEditorProjectOpener;
            if (opener == default)
                return false;

            try
            {
                return opener(fullPath, line, 0);
            }
            catch (Exception e)
            {
                LogKit.Warning($"[YokiCommandBridge] 当前代码编辑器打开失败，将尝试 Unity 外部打开回退: {e.Message}");
                return false;
            }
        }

        internal static bool TryOpenCodeLocationWithUnityFallback(string fullPath, int line)
        {
            var opener = ExternalCodeLocationOpener;
            if (opener == default)
                return false;

            try
            {
                return opener(fullPath, line);
            }
            catch (Exception e)
            {
                LogKit.Warning($"[YokiCommandBridge] Unity 外部打开回退失败: {e.Message}");
                return false;
            }
        }

        internal static void RequestExternalEditorFocus()
        {
            var scheduler = ExternalEditorFocusScheduler;
            if (scheduler == default)
                return;

            scheduler(static () =>
            {
                try
                {
                    ExternalEditorFocusRequester?.Invoke();
                }
                catch (Exception e)
                {
                    LogKit.Warning($"[YokiCommandBridge] 激活外部代码编辑器失败: {e.Message}");
                }
            });
        }

        private static string ResolveCodeLocationPath(string projectRoot, string normalizedPath)
        {
            if (Path.IsPathRooted(normalizedPath))
                return Path.GetFullPath(normalizedPath);

            return Path.GetFullPath(Path.Combine(
                projectRoot,
                normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string BuildOpenCodeLocationResponse(string normalizedPath, int clampedLine, bool openedByCurrentEditor)
        {
            return "{\"opened\":true,\"editor\":\"" +
                   (openedByCurrentEditor ? "unity-current-editor" : "unity-external-fallback") +
                   "\",\"filePath\":\"" +
                   JsonHelper.EscapeString(normalizedPath.Replace('\\', '/')) +
                   "\",\"line\":" + clampedLine + "}";
        }

        private static bool DefaultCodeEditorProjectOpener(string fullPath, int line, int column)
        {
            var currentEditor = CodeEditor.CurrentEditor;
            if (currentEditor == default)
                return false;

            return currentEditor.OpenProject(fullPath, line, column);
        }

        private static void FocusExternalScriptEditorWindow()
        {
            var configuredEditor = CodeEditor.CurrentEditorInstallation;
            var configuredEditorName = Path.GetFileNameWithoutExtension(configuredEditor);
            if (string.IsNullOrEmpty(configuredEditorName))
                return;

            var currentProcessId = Process.GetCurrentProcess().Id;
            foreach (var process in Process.GetProcessesByName(configuredEditorName))
            {
                try
                {
                    if (process.Id == currentProcessId)
                        continue;

                    if (TryFocusProcessMainWindow(process))
                        return;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static bool TryFocusProcessMainWindow(Process process)
        {
#if UNITY_EDITOR_WIN
            var handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero)
                return false;

            if (IsIconic(handle))
                ShowWindow(handle, SW_RESTORE);
            else
                ShowWindow(handle, SW_SHOW);

            return SetForegroundWindow(handle);
#else
            return false;
#endif
        }

#if UNITY_EDITOR_WIN
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
#endif
    }
}
#endif
