#if UNITY_EDITOR
using System;
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

        private static void GenerateCSharpFile(string assetPath, string scriptNamespace, bool autoGenerated, Action<ICodeScope> build)
        {
            CodeGenKit.GenerateToFile(assetPath, root =>
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

        private static void AddPendingPrefab(string panelName, string scriptNamespace, string prefabPath, string scriptFolder, string assemblyName)
        {
            var normalizedAssemblyName = string.IsNullOrEmpty(assemblyName) ? DEFAULT_ASSEMBLY_NAME : assemblyName;
            var entry = panelName + PENDING_SEPARATOR + scriptNamespace + PENDING_SEPARATOR + prefabPath +
                        PENDING_SEPARATOR + scriptFolder + PENDING_SEPARATOR + normalizedAssemblyName;
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
    }
}
#endif
