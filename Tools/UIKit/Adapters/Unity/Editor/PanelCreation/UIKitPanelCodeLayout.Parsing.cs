#if UNITY_EDITOR
using System;
using System.IO;

namespace YokiFrame
{
    internal sealed partial class UIKitPanelCodeLayout
    {
        /// <summary>以实际脚本的类型、目录和程序集恢复共同输出根；旧布局会在生成或转换事务中自动迁移。</summary>
        internal static UIKitPanelCodeLayout FromScript(Type type, string scriptPath, string prefabPath)
        {
            string path = RequireAssetFile(scriptPath, ".cs", nameof(scriptPath));
            bool component = typeof(UIComponent).IsAssignableFrom(type);
            bool element = !component && typeof(UIElement).IsAssignableFrom(type);
            bool panel = !component && !element && typeof(UIPanel).IsAssignableFrom(type);
            if (!component && !element && !panel)
                throw new InvalidOperationException("脚本不是 UIKit 生成类型: " + type.FullName);

            string scriptNamespace = type.Namespace ?? string.Empty;
            string fileName = Path.GetFileName(path);
            string directory = AssetDirectory(path);
            string ownerName = type.Name;
            string scriptFolder;
            string elementComponentName = null;
            if (panel)
            {
                if (fileName != ownerName + ".cs") throw LayoutError(path);
                string parent = AssetDirectory(directory);
                scriptFolder = Path.GetFileName(parent) == PANEL_FOLDER
                    ? AssetDirectory(parent) : parent;
                if (Path.GetFileName(directory) != ownerName) throw LayoutError(path);
            }
            else if (component)
            {
                if (Path.GetFileName(directory) == ownerName
                    && Path.GetFileName(AssetDirectory(directory)) == COMPONENT_FOLDER)
                {
                    scriptFolder = AssetDirectory(AssetDirectory(directory));
                }
                else if ((Path.GetFileName(directory) == COMPONENT_FOLDER
                    || Path.GetFileName(directory) == "UIComponent")
                    && fileName == ownerName + ".cs")
                {
                    scriptFolder = AssetDirectory(directory);
                }
                else
                {
                    throw LayoutError(path);
                }
                // Component 自身也是其子 Element 的 owner，恢复脚本时必须保留这个局部作用域。
                elementComponentName = ownerName;
            }
            else
            {
                if (Path.GetFileName(directory) != ELEMENT_FOLDER) throw LayoutError(path);
                string ownerDirectory = AssetDirectory(directory);
                string category = Path.GetFileName(AssetDirectory(ownerDirectory));
                if (category == PANEL_FOLDER)
                {
                    ownerName = Path.GetFileName(ownerDirectory);
                    scriptFolder = AssetDirectory(AssetDirectory(ownerDirectory));
                }
                else if (category == COMPONENT_FOLDER)
                {
                    ownerName = Path.GetFileName(ownerDirectory);
                    elementComponentName = ownerName;
                    scriptFolder = AssetDirectory(AssetDirectory(ownerDirectory));
                }
                else if (category == "UIComponent")
                {
                    ownerName = Path.GetFileName(ownerDirectory);
                    elementComponentName = ownerName;
                    scriptFolder = AssetDirectory(AssetDirectory(ownerDirectory));
                }
                else
                {
                    ownerName = Path.GetFileName(ownerDirectory);
                    scriptFolder = AssetDirectory(ownerDirectory);
                }
                int separator = scriptNamespace.LastIndexOf('.');
                string segment = separator < 0 ? string.Empty : scriptNamespace.Substring(separator + 1);
                const string suffix = "UIElement";
                if (separator <= 0 || !segment.EndsWith(suffix, StringComparison.Ordinal)
                    || segment.Length == suffix.Length) throw LayoutError(path);
                string namespaceOwner = segment.Substring(0, segment.Length - suffix.Length);
                if (!string.Equals(namespaceOwner, ownerName, StringComparison.Ordinal)) throw LayoutError(path);
                scriptNamespace = scriptNamespace.Substring(0, separator);
            }

            UIKitPanelGenerationRequest request = UIKitPanelGenerationRequest.CreateDefault(ownerName);
            request.scriptFolder = scriptFolder;
            request.scriptNamespace = CodeGenKit.RequireQualifiedName(scriptNamespace, nameof(scriptNamespace));
            request.assemblyName = type.Assembly.GetName().Name;
            request.prefabFolder = AssetDirectory(prefabPath);
            request.prefabPath = prefabPath;
            UIKitPanelCodeLayout layout = new(request, elementComponentName);
            string expected = panel ? layout.PanelScriptPath
                : component ? layout.GetComponentPath(type.Name, false)
                : layout.GetElementPath(type.Name, false);
            if (!string.Equals(path, expected, StringComparison.Ordinal))
            {
                string legacy = panel
                    ? layout.ScriptFolder + "/" + layout.PanelName + "/" + fileName
                    : component
                        ? layout.ScriptFolder + "/UIComponent/" + fileName
                        : layout.ScriptFolder + "/" + (elementComponentName == null
                            ? layout.PanelName : "UIComponent/" + elementComponentName) + "/UIElement/" + fileName;
                if (!string.Equals(path, legacy, StringComparison.Ordinal)) throw LayoutError(path);
            }
            return layout;
        }

        /// <summary>集中处理 Asset 父目录解析，调用方不再按固定层数猜测共同输出根。</summary>
        internal static string AssetDirectory(string path)
        {
            string normalized = path.Replace('\\', '/').TrimEnd('/');
            int separator = normalized.LastIndexOf('/');
            if (separator <= 0) throw new InvalidOperationException("无法解析 Asset 父目录: " + path);
            return normalized.Substring(0, separator);
        }

        /// <summary>为旧布局或身份矛盾提供明确修复入口，不允许生成第二份同名脚本。</summary>
        private static InvalidOperationException LayoutError(string path)
        {
            return new InvalidOperationException("脚本不符合 UIKit 生成布局，且无法确认旧文件归属: "
                + path + "。生成或转换时会自动迁移可确认归属的旧文件，不会覆盖冲突代码。");
        }
    }
}
#endif
