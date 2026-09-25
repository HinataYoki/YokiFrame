#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>描述一份不能从挂载身份证明已迁移、因此只能作为确认删除候选的生成文件。</summary>
    internal sealed class UIKitGeneratedCleanupCandidate
    {
        /// <summary>创建一对用户脚本和 Designer 的删除候选。</summary>
        internal UIKitGeneratedCleanupCandidate(string userPath, string designerPath, string reason)
        {
            UserPath = userPath;
            DesignerPath = designerPath;
            Reason = reason;
        }

        /// <summary>用户 partial 的项目相对路径。</summary>
        internal string UserPath { get; }

        /// <summary>Designer partial 的项目相对路径；不存在时为空。</summary>
        internal string DesignerPath { get; }

        /// <summary>进入候选列表的原因，供确认对话框展示。</summary>
        internal string Reason { get; }
    }

    /// <summary>
    /// 扫描当前共同脚本根中的孤立生成模板。
    /// 只收集候选，不删除；有引用、有业务代码或身份不完整的文件只报告。
    /// </summary>
    internal static class UIKitGeneratedCodeCleanup
    {
        private const string DESIGNER_SUFFIX = ".Designer.cs";
        private const int MAX_DIALOG_ITEMS = 12;

        /// <summary>
        /// 在用户明确确认后删除候选文件。取消、测试模式或没有候选时不改磁盘。
        /// </summary>
        /// <param name="layout">本次生成的共同脚本根。</param>
        /// <param name="expected">本次生成仍需要的脚本路径。</param>
        /// <param name="interactive">为 false 时只返回候选，供测试和批处理预览。</param>
        /// <returns>用户确认并已删除的项目相对路径。</returns>
        internal static List<string> ConfirmAndDelete(string root, HashSet<string> expected, bool interactive)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("清理目录不能为空。", nameof(root));
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            List<UIKitGeneratedCleanupCandidate> candidates = Collect(root, expected, out List<string> blocked);
            ReportBlocked(blocked);
            if (candidates.Count == 0 || !interactive) return new List<string>();
            if (!EditorUtility.DisplayDialog(
                    "删除 UIKit 遗留代码",
                    Describe(candidates),
                    "删除",
                    "保留"))
                return new List<string>();

            List<string> deleted = new();
            AssetDatabase.StartAssetEditing();
            try
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    Delete(candidates[index].UserPath, deleted);
                    Delete(candidates[index].DesignerPath, deleted);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return deleted;
        }

        /// <summary>收集当前共同根内、且不在本次期望路径中的孤立模板。</summary>
        internal static List<UIKitGeneratedCleanupCandidate> Collect(
            string root,
            HashSet<string> expected,
            out List<string> blocked)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("清理目录不能为空。", nameof(root));
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            blocked = new List<string>();
            List<UIKitGeneratedCleanupCandidate> candidates = new();
            HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
            string absoluteRoot = UIKitPanelCodeLayout.ToAbsolutePath(root);
            if (!Directory.Exists(absoluteRoot)) return candidates;
            foreach (string file in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                string path = UIKitPanelCodeLayout.ToAssetPath(file);
                if (!visited.Add(path) || expected.Contains(path)) continue;
                if (!TryGetPair(path, out string userPath, out string designerPath, out string incomplete))
                {
                    if (!string.IsNullOrEmpty(incomplete)) blocked.Add(incomplete);
                    continue;
                }

                if (expected.Contains(userPath) || expected.Contains(designerPath)) continue;
                if (!CanDelete(userPath, designerPath, out string reason))
                {
                    blocked.Add(userPath + "：" + reason);
                    continue;
                }

                candidates.Add(new UIKitGeneratedCleanupCandidate(userPath, designerPath, "无 Prefab 使用、无源码引用、无业务代码"));
            }

            return candidates;
        }

        /// <summary>把用户脚本和 Designer 配成一组；身份不完整时只报告，不进入删除候选。</summary>
        private static bool TryGetPair(string path, out string userPath, out string designerPath, out string incomplete)
        {
            string assetPath = UIKitPanelCodeLayout.ToAssetPath(path);
            incomplete = string.Empty;
            userPath = assetPath.EndsWith(DESIGNER_SUFFIX, StringComparison.OrdinalIgnoreCase)
                ? assetPath.Substring(0, assetPath.Length - DESIGNER_SUFFIX.Length) + ".cs"
                : assetPath;
            designerPath = userPath.Substring(0, userPath.Length - ".cs".Length) + DESIGNER_SUFFIX;
            if (!File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(userPath)))
            {
                incomplete = assetPath + "：缺少用户脚本，不能判断是否可删除";
                designerPath = string.Empty;
                return false;
            }

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(userPath);
            Type type = script == default ? null : script.GetClass();
            if (type == null || (!typeof(UIElement).IsAssignableFrom(type) && !typeof(UIPanel).IsAssignableFrom(type)))
            {
                incomplete = userPath + "：不是可解析的 UIKit 生成类型";
                return false;
            }

            if (!File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(designerPath))) designerPath = string.Empty;
            return true;
        }

        /// <summary>确认用户脚本和 Designer 都是空模板，且类型没有 Prefab 或源码引用。</summary>
        private static bool CanDelete(string userPath, string designerPath, out string reason)
        {
            reason = string.Empty;
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(userPath);
            Type type = script == default ? null : script.GetClass();
            if (type == null)
            {
                reason = "脚本类型无法解析";
                return false;
            }

            if (HasUserCode(File.ReadAllText(UIKitPanelCodeLayout.ToAbsolutePath(userPath)), type.Name))
            {
                reason = "用户脚本包含业务代码";
                return false;
            }

            if (!string.IsNullOrEmpty(designerPath)
                && HasUserCode(File.ReadAllText(UIKitPanelCodeLayout.ToAbsolutePath(designerPath)), type.Name))
            {
                reason = "Designer 包含模板之外的代码";
                return false;
            }

            if (UsedByPrefab(type) || ReferencedBySource(type, userPath, designerPath))
            {
                reason = "仍被 Prefab 或源码引用";
                return false;
            }

            return true;
        }

        /// <summary>检查全部 Prefab 上的每一份 UIElement，避免只看 GetComponent 返回的第一份。</summary>
        private static bool UsedByPrefab(Type type)
        {
            string fullName = type.FullName;
            string assemblyName = type.Assembly.GetName().Name;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == default) continue;
                UIElement[] owners = prefab.GetComponentsInChildren<UIElement>(true);
                for (var index = 0; index < owners.Length; index++)
                {
                    Type mounted = owners[index] == default ? null : owners[index].GetType();
                    if (mounted != null && mounted.FullName == fullName && mounted.Assembly.GetName().Name == assemblyName)
                        return true;
                }
            }

            return false;
        }

        /// <summary>按完整类型名和程序集名检查源码引用，跳过候选自己的用户脚本与 Designer。</summary>
        private static bool ReferencedBySource(Type type, string userPath, string designerPath)
        {
            string fullName = type.FullName;
            string assemblyQualified = fullName + ", " + type.Assembly.GetName().Name;
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!UIKitPanelCodeLayout.IsUnityAssetPath(path) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;
                string assetPath = UIKitPanelCodeLayout.ToAssetPath(path);
                if (string.Equals(assetPath, userPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(assetPath, designerPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                string absolutePath = UIKitPanelCodeLayout.ToAbsolutePath(assetPath);
                if (!File.Exists(absolutePath)) continue;
                string source = File.ReadAllText(absolutePath);
                if (source.IndexOf(fullName, StringComparison.Ordinal) >= 0
                    || source.IndexOf(assemblyQualified, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>判断类体在去掉注释后是否仍有模板之外的成员。</summary>
        private static bool HasUserCode(string source, string typeName)
        {
            int declaration = source.IndexOf("class " + typeName, StringComparison.Ordinal);
            int open = declaration < 0 ? -1 : source.IndexOf('{', declaration);
            int close = source.LastIndexOf('}');
            if (open < 0 || close <= open) return true;
            string body = source.Substring(open + 1, close - open - 1);
            body = Regex.Replace(body, @"//.*$", string.Empty, RegexOptions.Multiline);
            body = Regex.Replace(body, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            body = Regex.Replace(body, @"^\s*\[SerializeField\]\s*$", string.Empty, RegexOptions.Multiline);
            return !string.IsNullOrWhiteSpace(body);
        }

        /// <summary>删除单个资源；文件不存在时跳过，删除失败时保留已删清单并抛出。</summary>
        private static void Delete(string path, List<string> deleted)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(path))) return;
            if (!AssetDatabase.DeleteAsset(path))
                throw new IOException("无法删除 UIKit 遗留文件: " + path);
            deleted.Add(path);
        }

        /// <summary>把不能自动处理的文件写到 Console，避免用户以为它们已被判定为可删。</summary>
        private static void ReportBlocked(List<string> blocked)
        {
            if (blocked.Count == 0) return;
            Debug.LogWarning("UIKit 以下遗留文件未删除，需要人工确认：\n" + string.Join("\n", blocked));
        }

        /// <summary>生成确认对话框文本，过长时截断并保留总数。</summary>
        private static string Describe(List<UIKitGeneratedCleanupCandidate> candidates)
        {
            List<string> lines = new();
            int count = Math.Min(candidates.Count, MAX_DIALOG_ITEMS);
            for (var index = 0; index < count; index++)
                lines.Add(candidates[index].UserPath + "\n" + candidates[index].Reason);
            if (candidates.Count > count) lines.Add("其余 " + (candidates.Count - count) + " 项见 Console。");
            return "以下文件没有挂载身份证明其已改名或换位，也没有发现引用或业务代码。确认后才会删除：\n\n"
                + string.Join("\n\n", lines);
        }
    }
}
#endif
