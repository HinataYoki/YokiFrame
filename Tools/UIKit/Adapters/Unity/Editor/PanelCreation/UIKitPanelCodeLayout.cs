#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 统一验证 UIKit 生成路径、命名空间和程序集边界。
    /// </summary>
    internal sealed class UIKitPanelCodeLayout
    {
        /// <summary>验证请求并构造不可变输出布局。</summary>
        internal UIKitPanelCodeLayout(UIKitPanelGenerationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.ApplyDefaults();
            PanelName = CodeGenKit.RequireIdentifier(request.panelName, nameof(request.panelName));
            ScriptNamespace = CodeGenKit.RequireQualifiedName(
                request.scriptNamespace,
                nameof(request.scriptNamespace));
            AssemblyName = RequireAssemblyName(request.assemblyName);
            CodeTemplate = RequireCodeTemplate(request.codeTemplate);
            PrefabFolder = RequireAssetFolder(request.prefabFolder, nameof(request.prefabFolder));
            ScriptFolder = RequireAssetFolder(request.scriptFolder, nameof(request.scriptFolder));
            PrefabPath = string.IsNullOrWhiteSpace(request.prefabPath)
                ? CombineAssetPath(PrefabFolder, PanelName + ".prefab")
                : RequireAssetFile(request.prefabPath, ".prefab", nameof(request.prefabPath));
        }

        internal string PanelName { get; }
        internal string PrefabFolder { get; }
        internal string ScriptFolder { get; }
        internal string ScriptNamespace { get; }
        internal string AssemblyName { get; }
        internal string CodeTemplate { get; }
        internal string PrefabPath { get; }
        internal string PanelFolder => CombineAssetPath(ScriptFolder, PanelName);
        internal string PanelScriptPath => CombineAssetPath(PanelFolder, PanelName + ".cs");
        internal string PanelDesignerPath => CombineAssetPath(PanelFolder, PanelName + ".Designer.cs");

        /// <summary>获取 Element 用户或 Designer 文件路径。</summary>
        internal string GetElementPath(string typeName, bool designer)
        {
            string fileName = typeName + (designer ? ".Designer.cs" : ".cs");
            return CombineAssetPath(PanelFolder, "UIElement/" + fileName);
        }

        /// <summary>获取 Component 用户或 Designer 文件路径。</summary>
        internal string GetComponentPath(string typeName, bool designer)
        {
            string fileName = typeName + (designer ? ".Designer.cs" : ".cs");
            return CombineAssetPath(ScriptFolder, "UIComponent/" + fileName);
        }

        /// <summary>获取 Element 类型命名空间。</summary>
        internal string GetElementNamespace()
        {
            return ScriptNamespace + "." + PanelName + "UIElement";
        }

        /// <summary>把 Assets 相对路径转换为当前项目绝对路径。</summary>
        internal static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <summary>创建目标 Asset 文件夹并交给 Unity 刷新导入。</summary>
        internal static void EnsureAssetFolder(string assetFolder)
        {
            Directory.CreateDirectory(ToAbsolutePath(assetFolder));
        }

        /// <summary>组合并规范化两个 Assets 相对路径片段。</summary>
        internal static string CombineAssetPath(string left, string right)
        {
            return (left.TrimEnd('/') + "/" + right.TrimStart('/')).Replace('\\', '/');
        }

        /// <summary>验证路径为当前项目 Assets 内部目录。</summary>
        private static string RequireAssetFolder(string value, string parameterName)
        {
            string normalized = NormalizeAssetPath(value, parameterName).TrimEnd('/');
            if (string.Equals(normalized, "Assets", StringComparison.Ordinal)) return normalized;
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("路径必须位于当前项目 Assets: " + value, parameterName);
            return normalized;
        }

        /// <summary>验证路径为指定扩展名的 Assets 内文件。</summary>
        private static string RequireAssetFile(string value, string extension, string parameterName)
        {
            string normalized = NormalizeAssetPath(value, parameterName);
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || !normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("必须是 Assets 内的 " + extension + " 文件: " + value, parameterName);
            return normalized;
        }

        /// <summary>拒绝绝对路径、父目录逃逸和空路径。</summary>
        private static string NormalizeAssetPath(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Asset 路径不能为空。", parameterName);
            string normalized = value.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized.IndexOf("../", StringComparison.Ordinal) >= 0
                || normalized.EndsWith("/..", StringComparison.Ordinal))
                throw new ArgumentException("Asset 路径不能越过项目根: " + value, parameterName);
            return normalized;
        }

        /// <summary>验证代码模板只使用受支持的稳定名称。</summary>
        private static string RequireCodeTemplate(string value)
        {
            IUIKitCodeTemplate template = UIKitCodeTemplateRegistry.Require(value);
            return template.Name;
        }

        /// <summary>验证程序集名为单行非空文本且不含路径字符。</summary>
        private static string RequireAssemblyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new[] { '/', '\\', '\r', '\n' }) >= 0)
                throw new ArgumentException("程序集名称不合法: " + value, nameof(value));
            return value.Trim();
        }
    }
}
#endif
