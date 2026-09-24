#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 统一验证 UIKit 生成路径、命名空间和程序集边界。
    /// </summary>
    internal sealed partial class UIKitPanelCodeLayout
    {
        internal const string PANEL_FOLDER = "Panel";
        internal const string COMPONENT_FOLDER = "Component";
        internal const string ELEMENT_FOLDER = "Element";

        /// <summary>验证请求并构造不可变输出布局。</summary>
        internal UIKitPanelCodeLayout(UIKitPanelGenerationRequest request, string elementComponentName = null)
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
            ElementComponentName = string.IsNullOrEmpty(elementComponentName) ? string.Empty
                : CodeGenKit.RequireIdentifier(elementComponentName, nameof(elementComponentName));
        }

        internal string PanelName { get; }
        internal string PrefabFolder { get; }
        internal string ScriptFolder { get; }
        internal string ScriptNamespace { get; }
        internal string AssemblyName { get; }
        internal string CodeTemplate { get; }
        internal string PrefabPath { get; }
        internal string ElementComponentName { get; }
        internal string PanelFolder => CombineAssetPath(ScriptFolder, PANEL_FOLDER + "/" + PanelName);
        internal string PanelScriptPath => CombineAssetPath(PanelFolder, PanelName + ".cs");
        internal string PanelDesignerPath => CombineAssetPath(PanelFolder, PanelName + ".Designer.cs");

        /// <summary>获取 Element 用户或 Designer 文件路径。</summary>
        internal string GetElementPath(string typeName, bool designer)
        {
            string fileName = typeName + (designer ? ".Designer.cs" : ".cs");
            string ownerFolder = ElementComponentName.Length == 0 ? PanelFolder
                : CombineAssetPath(ScriptFolder, COMPONENT_FOLDER + "/" + ElementComponentName);
            return CombineAssetPath(ownerFolder, ELEMENT_FOLDER + "/" + fileName);
        }

        /// <summary>获取 Component 用户或 Designer 文件路径。</summary>
        internal string GetComponentPath(string typeName, bool designer)
        {
            string fileName = typeName + (designer ? ".Designer.cs" : ".cs");
            return CombineAssetPath(ScriptFolder, COMPONENT_FOLDER + "/" + typeName + "/" + fileName);
        }

        /// <summary>获取 Element 类型命名空间。</summary>
        internal string GetElementNamespace()
        {
            return ScriptNamespace + "." + (ElementComponentName.Length == 0
                ? PanelName : ElementComponentName) + "UIElement";
        }

        /// <summary>进入公共 Component 的子绑定作用域；返回新布局，避免兄弟节点受到递归状态污染。</summary>
        internal UIKitPanelCodeLayout ForComponent(string typeName)
        {
            return new UIKitPanelCodeLayout(new UIKitPanelGenerationRequest
            {
                panelName = PanelName, prefabFolder = PrefabFolder, scriptFolder = ScriptFolder,
                scriptNamespace = ScriptNamespace, assemblyName = AssemblyName,
                codeTemplate = CodeTemplate, prefabPath = PrefabPath,
            }, typeName);
        }

        /// <summary>只在 Component 节点建立新类型作用域，Element 继续复用所属 Panel 或 Component 的作用域。</summary>
        internal UIKitPanelCodeLayout ForChildren(UIKitBindNode node)
        {
            return node.Strategy.OutputKind == UIKitBindOutputKind.Component ? ForComponent(node.TypeName) : this;
        }

        /// <summary>取得节点生成类型的完整名称，供代码字段、迁移和 Prefab 挂载共同使用。</summary>
        internal string GetFullTypeName(UIKitBindOutputKind kind, string typeName)
        {
            return (kind == UIKitBindOutputKind.Element ? GetElementNamespace() : ScriptNamespace) + "." + typeName;
        }

        /// <summary>把项目相对路径转换为当前项目绝对路径，不假定包位于 Assets 还是 Packages。</summary>
        internal static string ToAbsolutePath(string assetPath)
        {
            string normalized = NormalizeProjectPath(assetPath, nameof(assetPath));
            return Path.GetFullPath(Path.Combine(GetProjectRoot(),
                normalized.Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <summary>将磁盘路径或项目相对路径转换为 Unity AssetDatabase 使用的项目相对路径。</summary>
        internal static string ToAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Asset 路径不能为空。", nameof(path));
            string projectRoot = GetProjectRoot();
            string fullPath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = projectRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("路径不在当前 Unity 项目内: " + path, nameof(path));
            return Path.GetRelativePath(projectRoot, fullPath).Replace('\\', '/');
        }

        /// <summary>解析当前 Unity 项目根目录，供 Assets 与 Packages 等项目相对路径共用。</summary>
        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        /// <summary>规范化并验证项目相对 Asset 路径，禁止绝对路径和越过项目根目录。</summary>
        private static string NormalizeProjectPath(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("项目相对路径不能为空。", parameterName);
            string normalized = path.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(normalized))
                throw new ArgumentException("路径必须使用当前项目相对路径: " + path, parameterName);
            string fullPath = Path.GetFullPath(Path.Combine(GetProjectRoot(),
                normalized.Replace('/', Path.DirectorySeparatorChar)));
            string projectRoot = GetProjectRoot();
            string prefix = projectRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("路径不能越过当前 Unity 项目根: " + path, parameterName);
            string result = Path.GetRelativePath(projectRoot, fullPath).Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(result) || result == ".")
                throw new ArgumentException("路径不能指向 Unity 项目根目录: " + path, parameterName);
            return result;
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

        /// <summary>验证路径为 Unity 支持的项目相对文件夹，兼容 Assets 与 Packages 两种包布局。</summary>
        private static string RequireAssetFolder(string value, string parameterName)
        {
            string normalized = NormalizeProjectPath(value, parameterName);
            if (!IsUnityAssetPath(normalized))
                throw new ArgumentException("路径必须位于项目 Assets 或 Packages 根下: " + value, parameterName);
            return normalized;
        }

        /// <summary>验证路径为 Unity 支持的项目相对文件。</summary>
        private static string RequireAssetFile(string value, string extension, string parameterName)
        {
            string normalized = NormalizeProjectPath(value, parameterName);
            if (!IsUnityAssetPath(normalized)
                || !normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("必须是项目 Assets 或 Packages 内的 " + extension + " 文件: " + value, parameterName);
            return normalized;
        }

        /// <summary>判断路径是否属于 Unity AssetDatabase 可访问的项目资源根。</summary>
        internal static bool IsUnityAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)
                || path == "Packages" || path.StartsWith("Packages/", StringComparison.Ordinal);
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
