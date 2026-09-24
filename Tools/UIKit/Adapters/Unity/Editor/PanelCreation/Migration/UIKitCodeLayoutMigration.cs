#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace YokiFrame
{
    /// <summary>提供 UIKit 旧布局的手动迁移计划，并复用生成与转换事务所需的空闲检查。</summary>
    internal static class UIKitCodeLayoutMigration
    {
        /// <summary>迁移仅在编译健康且没有其它文件事务时运行，避免混淆两个事务的资产所有权。</summary>
        internal static void RequireIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorUtility.scriptCompilationFailed
                || UIKitConversionTransaction.IsPending || UIKitCodeLayoutMigrationTransaction.IsPending)
                throw new InvalidOperationException("请先完成导入、修复编译错误并等待当前转换或迁移结束。");
        }

        /// <summary>根据真实 MonoScript 类型识别旧 Panel/Component，不通过 Prefab 名或文件名前缀推断类型。</summary>
        internal static UIKitCodeLayoutMigrationPlan Build(string root)
        {
            var plan = new UIKitCodeLayoutMigrationPlan(root);
            var components = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                string path = AssetDatabase.GetAssetPath(script);
                if (!path.StartsWith(plan.Root + "/", StringComparison.Ordinal)) continue;
                Type type = script.GetClass();
                if (type == null || type.IsAbstract) continue;
                if (typeof(UIPanel).IsAssignableFrom(type)
                    && path == plan.Root + "/" + type.Name + "/" + type.Name + ".cs")
                {
                    string source = plan.Root + "/" + type.Name;
                    string target = plan.Root + "/Panel/" + type.Name;
                    plan.Add(source, target);
                    if (Directory.Exists(UIKitPanelCodeLayout.ToAbsolutePath(source + "/UIElement")))
                        plan.Add(target + "/UIElement", target + "/Element");
                }
                else if (typeof(UIComponent).IsAssignableFrom(type)
                    && path == plan.Root + "/UIComponent/" + type.Name + ".cs") components.Add(type.FullName, type);
            }
            AddComponents(plan, components);
            plan.Validate();
            return plan;
        }

        /// <summary>迁移公共组件及其所有明确归属的 partial；无法归属的旧文件拒绝自动处理。</summary>
        private static void AddComponents(UIKitCodeLayoutMigrationPlan plan, Dictionary<string, Type> components)
        {
            string directory = plan.Root + "/UIComponent";
            if (!Directory.Exists(UIKitPanelCodeLayout.ToAbsolutePath(directory))) return;
            foreach (Type type in components.Values)
            {
                string source = directory + "/" + type.Name;
                string target = plan.Root + "/Component/" + type.Name;
                if (!Directory.Exists(UIKitPanelCodeLayout.ToAbsolutePath(source))) continue;
                plan.Add(source, target);
                if (Directory.Exists(UIKitPanelCodeLayout.ToAbsolutePath(source + "/UIElement")))
                    plan.Add(target + "/UIElement", target + "/Element");
            }
            foreach (string folderAbsolute in Directory.GetDirectories(UIKitPanelCodeLayout.ToAbsolutePath(directory)))
            {
                string folder = UIKitPanelCodeLayout.ToAssetPath(folderAbsolute);
                bool known = false;
                foreach (Type type in components.Values) known |= Path.GetFileName(folder) == type.Name;
                if (!known) throw new InvalidOperationException("无法确认旧 Component 子目录归属: " + folder);
            }
            foreach (string fileAbsolute in Directory.GetFiles(UIKitPanelCodeLayout.ToAbsolutePath(directory)))
            {
                string path = UIKitPanelCodeLayout.ToAssetPath(fileAbsolute);
                if (path.EndsWith(".meta", StringComparison.Ordinal)) continue;
                Type owner = Path.GetExtension(path) == ".cs" ? ResolvePartial(path, components) : null;
                if (owner == null) throw new InvalidOperationException("无法确认旧 Component 文件归属，请先人工整理: " + path);
                plan.Add(path, plan.Root + "/Component/" + owner.Name + "/" + Path.GetFileName(path));
            }
        }

        /// <summary>按命名空间和声明精确识别 partial；混合多个类型时不擅自移动到其中一个 owner 下。</summary>
        private static Type ResolvePartial(string path, Dictionary<string, Type> components)
        {
            var tokens = UIKitConversionSource.Tokenize(
                File.ReadAllText(UIKitPanelCodeLayout.ToAbsolutePath(path)));
            string namespaceName = UIKitConversionSource.GetNamespace(tokens);
            Type owner = null;
            for (var index = 0; index + 1 < tokens.Count; index++)
            {
                string token = tokens[index].Text;
                if (token != "class" && token != "struct" && token != "interface" && token != "enum") continue;
                string fullName = namespaceName + "." + tokens[index + 1].Text;
                if (!components.TryGetValue(fullName, out Type match) || (owner != null && owner != match)) return null;
                owner = match;
            }
            return owner;
        }
    }
}
#endif
