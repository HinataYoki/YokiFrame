#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    internal static partial class UIKitPanelPrefabCreator
    {
        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return default;

            var type = Type.GetType(typeName);
            if (type != default)
                return type;

            var assemblies = LoadedAssemblyProvider.GetLoadedAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName, false);
                if (type != default)
                    return type;
            }

            return default;
        }

        private static Type ResolveType(string typeName, string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return ResolveType(typeName);

            var type = Type.GetType(typeName + ", " + assemblyName, false);
            if (type != default)
                return type;

            try
            {
                var assembly = Assembly.Load(assemblyName);
                if (assembly != default)
                {
                    type = assembly.GetType(typeName, false);
                    if (type != default)
                        return type;
                }
            }
            catch
            {
                // 程序集名可能来自旧配置；继续回退到全部已加载程序集扫描。
            }

            return ResolveType(typeName);
        }

        private static void ValidateRequest(UIKitPanelCreateRequest request)
        {
            if (request == default)
                throw new ArgumentNullException(nameof(request));

            if (!IsValidCSharpIdentifier(request.PanelName))
                throw new InvalidOperationException("Panel 名称必须是合法 C# 类型名: " + request.PanelName);

            if (!IsValidNamespace(request.ScriptNamespace))
                throw new InvalidOperationException("命名空间不合法: " + request.ScriptNamespace);

            request.AssemblyName = string.IsNullOrEmpty(request.AssemblyName) ? DEFAULT_ASSEMBLY_NAME : request.AssemblyName;
            request.CodeTemplate = NormalizeCodeTemplateName(request.CodeTemplate);
        }

        /// <summary>
        /// 校验并规范化可在工作台与 Unity Editor 入口间共享的生成参数。
        /// </summary>
        internal static void NormalizeEditorSettings(UIKitPanelCreateRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            request.ApplyDefaults();
            if (!IsValidNamespace(request.ScriptNamespace))
                throw new InvalidOperationException("命名空间不合法: " + request.ScriptNamespace);

            request.PrefabFolder = NormalizeAssetFolder(request.PrefabFolder, DEFAULT_PREFAB_FOLDER);
            request.ScriptFolder = NormalizeAssetFolder(request.ScriptFolder, DEFAULT_SCRIPT_FOLDER);
            request.AssemblyName = string.IsNullOrWhiteSpace(request.AssemblyName)
                ? DEFAULT_ASSEMBLY_NAME
                : request.AssemblyName.Trim();
            request.CodeTemplate = NormalizeCodeTemplateName(request.CodeTemplate);
        }

        internal static string NormalizeCodeTemplateName(string templateName)
        {
            if (string.Equals(templateName, MINIMAL_CODE_TEMPLATE, StringComparison.OrdinalIgnoreCase))
                return MINIMAL_CODE_TEMPLATE;

            return DEFAULT_CODE_TEMPLATE;
        }

        private static bool IsMinimalCodeTemplate(string templateName) =>
            string.Equals(NormalizeCodeTemplateName(templateName), MINIMAL_CODE_TEMPLATE, StringComparison.Ordinal);

        private static bool IsValidNamespace(string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            var parts = value.Split('.');
            for (var i = 0; i < parts.Length; i++)
            {
                if (!IsValidCSharpIdentifier(parts[i]))
                    return false;
            }

            return true;
        }

        private static bool IsValidCSharpIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || sCSharpKeywords.Contains(value))
                return false;

            if (!char.IsLetter(value[0]) && value[0] != '_')
                return false;

            for (var i = 1; i < value.Length; i++)
            {
                if (!char.IsLetterOrDigit(value[i]) && value[i] != '_')
                    return false;
            }

            return true;
        }

        private static string NormalizeAssetFolder(string path, string fallback)
        {
            path = string.IsNullOrEmpty(path) ? fallback : path.Trim();
            path = path.Replace('\\', '/').TrimEnd('/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (path.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                path = "Assets" + path.Substring(dataPath.Length);

            if (!path.StartsWith("Assets", StringComparison.Ordinal) || path.Contains(".."))
                throw new InvalidOperationException("路径必须位于 Assets 目录下: " + path);

            return path;
        }

        private static string CombineAssetPath(string folder, string fileName) =>
            folder.TrimEnd('/') + "/" + fileName.TrimStart('/');

        private static void EnsureAssetFolder(string assetFolder)
        {
            var segments = assetFolder.Replace('\\', '/').Split('/');
            if (segments.Length == 0 || segments[0] != "Assets")
                throw new InvalidOperationException("路径必须位于 Assets 目录下: " + assetFolder);

            var current = "Assets";
            for (var i = 1; i < segments.Length; i++)
            {
                if (string.IsNullOrEmpty(segments[i]))
                    continue;

                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);

                current = next;
            }
        }

        private static string GetPanelScriptPath(UIKitPanelCreateRequest request, string scriptFolder) =>
            CombineAssetPath(CombineAssetPath(scriptFolder, request.PanelName), request.PanelName + ".cs");

        private static string GetPanelDesignerPath(UIKitPanelCreateRequest request, string scriptFolder) =>
            CombineAssetPath(CombineAssetPath(scriptFolder, request.PanelName), request.PanelName + ".Designer.cs");

        private static bool GenerateCSharpFile(string assetPath, string scriptNamespace, bool autoGenerated, Action<ICodeScope> build)
        {
            var code = CodeGenKit.GenerateToString(root =>
            {
                if (autoGenerated)
                    AppendAutoGeneratedHeader(root);

                root.Using("UnityEngine");
                root.Using("YokiFrame");
                root.EmptyLine();
                if (string.IsNullOrEmpty(scriptNamespace))
                {
                    build(root);
                    return;
                }

                root.Namespace(scriptNamespace, scope => build(scope));
            });

            if (File.Exists(assetPath) && string.Equals(File.ReadAllText(assetPath), code, StringComparison.Ordinal))
                return false;

            var directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(assetPath, code, new System.Text.UTF8Encoding(false));
            return true;
        }

        private static void AppendAutoGeneratedHeader(ICodeScope scope)
        {
            scope.Custom("//------------------------------------------------------------------------------");
            scope.Custom("// <auto-generated>");
            scope.Custom("//     This code was generated by YokiFrame UIKit.");
            scope.Custom("// </auto-generated>");
            scope.Custom("//------------------------------------------------------------------------------");
            scope.EmptyLine();
        }

        private static void AddPendingPrefab(
            string panelName,
            string scriptNamespace,
            string prefabPath,
            string scriptFolder,
            string assemblyName,
            bool requiresOpenPrefabStage)
        {
            var normalizedAssemblyName = string.IsNullOrEmpty(assemblyName) ? DEFAULT_ASSEMBLY_NAME : assemblyName;
            var entry = panelName + PENDING_SEPARATOR + scriptNamespace + PENDING_SEPARATOR + prefabPath +
                        PENDING_SEPARATOR + scriptFolder + PENDING_SEPARATOR + normalizedAssemblyName;
            if (requiresOpenPrefabStage)
                SetOpenPrefabStageBindingRetry(prefabPath, 0);
            else
                ClearOpenPrefabStageBinding(prefabPath);

            var pending = SessionState.GetString(PENDING_SESSION_KEY, string.Empty);
            if (!string.IsNullOrEmpty(pending) && pending.Contains(entry))
                return;

            SessionState.SetString(PENDING_SESSION_KEY, string.IsNullOrEmpty(pending) ? entry : pending + "\n" + entry);
        }

        private static bool TryParsePendingEntry(
            string value,
            out string panelName,
            out string scriptNamespace,
            out string prefabPath,
            out string scriptFolder,
            out string assemblyName)
        {
            panelName = default;
            scriptNamespace = default;
            prefabPath = default;
            scriptFolder = default;
            assemblyName = DEFAULT_ASSEMBLY_NAME;

            var parts = value.Split(PENDING_SEPARATOR);
            if (parts.Length != 4 && parts.Length != 5)
                return false;

            panelName = parts[0];
            scriptNamespace = parts[1];
            prefabPath = parts[2];
            scriptFolder = parts[3];
            if (parts.Length == 5 && !string.IsNullOrEmpty(parts[4]))
                assemblyName = parts[4];
            return true;
        }

        private static string NormalizeAssetPathForComparison(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return path.Replace('\\', '/').Trim();
        }

        private static bool IsOpenPrefabStageBindingPending(string prefabPath)
        {
            var normalizedPrefabPath = NormalizeAssetPathForComparison(prefabPath);
            if (string.IsNullOrEmpty(normalizedPrefabPath))
                return false;

            return TryGetOpenPrefabStageBindingRetry(normalizedPrefabPath, out var unusedRetryCount);
        }

        private static bool ShouldRetryOpenPrefabStageBinding(string prefabPath)
        {
            var normalizedPrefabPath = NormalizeAssetPathForComparison(prefabPath);
            if (string.IsNullOrEmpty(normalizedPrefabPath))
                return false;

            var retryCount = 0;
            TryGetOpenPrefabStageBindingRetry(normalizedPrefabPath, out retryCount);
            retryCount++;
            SetOpenPrefabStageBindingRetry(normalizedPrefabPath, retryCount);
            if (retryCount <= MAX_OPEN_STAGE_BIND_RETRY_COUNT)
                return true;

            Debug.LogWarning(
                "UIKit 绑定等待打开的 Prefab Stage 超时，将保留待绑定队列且不会离线保存 Prefab: " +
                normalizedPrefabPath);
            return false;
        }

        private static bool TryGetOpenPrefabStageBindingRetry(string prefabPath, out int retryCount)
        {
            retryCount = 0;
            var normalizedPrefabPath = NormalizeAssetPathForComparison(prefabPath);
            if (string.IsNullOrEmpty(normalizedPrefabPath))
                return false;

            var entries = GetOpenPrefabStageBindingEntries();
            for (var i = 0; i < entries.Count; i++)
            {
                if (TryParseOpenPrefabStageBindingEntry(entries[i], out var entryPrefabPath, out var entryRetryCount) &&
                    string.Equals(entryPrefabPath, normalizedPrefabPath, StringComparison.Ordinal))
                {
                    retryCount = entryRetryCount;
                    return true;
                }
            }

            return false;
        }

        private static void SetOpenPrefabStageBindingRetry(string prefabPath, int retryCount)
        {
            var normalizedPrefabPath = NormalizeAssetPathForComparison(prefabPath);
            if (string.IsNullOrEmpty(normalizedPrefabPath))
                return;

            var entries = GetOpenPrefabStageBindingEntries();
            var updated = false;
            for (var i = 0; i < entries.Count; i++)
            {
                if (!TryParseOpenPrefabStageBindingEntry(entries[i], out var entryPrefabPath, out var entryRetryCount))
                    continue;

                if (!string.Equals(entryPrefabPath, normalizedPrefabPath, StringComparison.Ordinal))
                    continue;

                entries[i] = BuildOpenPrefabStageBindingEntry(normalizedPrefabPath, retryCount);
                updated = true;
                break;
            }

            if (!updated)
                entries.Add(BuildOpenPrefabStageBindingEntry(normalizedPrefabPath, retryCount));

            SaveOpenPrefabStageBindingEntries(entries);
        }

        private static void ClearOpenPrefabStageBinding(string prefabPath)
        {
            var normalizedPrefabPath = NormalizeAssetPathForComparison(prefabPath);
            if (string.IsNullOrEmpty(normalizedPrefabPath))
                return;

            var entries = GetOpenPrefabStageBindingEntries();
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (TryParseOpenPrefabStageBindingEntry(entries[i], out var entryPrefabPath, out var retryCount) &&
                    string.Equals(entryPrefabPath, normalizedPrefabPath, StringComparison.Ordinal))
                {
                    entries.RemoveAt(i);
                }
            }

            SaveOpenPrefabStageBindingEntries(entries);
        }

        private static List<string> GetOpenPrefabStageBindingEntries()
        {
            var pending = SessionState.GetString(PENDING_OPEN_STAGE_SESSION_KEY, string.Empty);
            var entries = new List<string>();
            if (string.IsNullOrEmpty(pending))
                return entries;

            var lines = pending.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                    entries.Add(lines[i]);
            }

            return entries;
        }

        private static void SaveOpenPrefabStageBindingEntries(List<string> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                SessionState.SetString(PENDING_OPEN_STAGE_SESSION_KEY, string.Empty);
                return;
            }

            SessionState.SetString(PENDING_OPEN_STAGE_SESSION_KEY, string.Join("\n", entries.ToArray()));
        }

        private static bool TryParseOpenPrefabStageBindingEntry(string entry, out string prefabPath, out int retryCount)
        {
            prefabPath = null;
            retryCount = 0;
            if (string.IsNullOrEmpty(entry))
                return false;

            var parts = entry.Split(PENDING_SEPARATOR);
            if (parts.Length != 2)
                return false;

            prefabPath = NormalizeAssetPathForComparison(parts[0]);
            return int.TryParse(parts[1], out retryCount);
        }

        private static string BuildOpenPrefabStageBindingEntry(string prefabPath, int retryCount)
        {
            return NormalizeAssetPathForComparison(prefabPath) + PENDING_SEPARATOR + retryCount;
        }
    }
}
#endif
