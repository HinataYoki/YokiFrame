#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using TypeMove = YokiFrame.UIKitConversionSource.TypeMove;

namespace YokiFrame
{
    /// <summary>以类型迁移事务执行 Element/Component 快速转换，普通生成不会隐式移动用户源码。</summary>
    internal static class UIKitBindConversion
    {
        /// <summary>预检转换范围、迁移源码与 GUID，并在编译失败时由事务恢复原文件和 Prefab。</summary>
        internal static void Convert(AbstractBind bind, BindType targetKind)
        {
            if (bind == default || bind.Bind == targetKind) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || UIKitConversionTransaction.IsPending)
                throw new InvalidOperationException("请等待当前生成或转换完成。");
            if (EditorUtility.scriptCompilationFailed)
                throw new InvalidOperationException("请先修复项目编译错误，再转换绑定类型。");
            BindType oldKind = bind.Bind;
            UIElement existingOwner = bind.GetComponent<UIElement>();
            if (oldKind == BindType.Member && IsGenerated(targetKind) && existingOwner != default)
                oldKind = existingOwner is UIComponent ? BindType.Component : BindType.Element;
            if (oldKind == targetKind) { SetKind(bind, targetKind); return; }
            bool migration = IsGenerated(oldKind) && IsGenerated(targetKind);
            if (!migration) { SetKind(bind, targetKind); return; }
            UIKitPanelCodeLayout previous = UIKitBindCodeService.ResolveLayout(bind);
            UIKitPanelCodeLayout next = targetKind == BindType.Component ? previous : ResolveElementParent(bind);
            string typeName = existingOwner != default ? existingOwner.GetType().Name : UIKitBindCodeService.GetTypeName(bind);
            if (!string.IsNullOrWhiteSpace(bind.CustomType) && bind.CustomType != typeName)
                throw new InvalidOperationException("现有脚本类型与 Bind 类名称不一致，请先恢复类名称: " + typeName);
            string oldPath = UIKitBindCodeService.GetScriptPath(previous, oldKind, typeName, false);
            if (!File.Exists(oldPath)) { SetKind(bind, targetKind); return; }
            List<TypeMove> moves = new();
            AddMove(moves, previous, next, oldKind, targetKind, typeName);
            CollectChildren(bind, previous, next, oldKind, targetKind, typeName, moves);
            RequireUnsharedBindings(bind, previous, moves);
            Dictionary<string, string> relocations = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> sources = BuildSources(moves, relocations);
            BindType saved = bind.Bind;
            try
            {
                bind.Bind = targetKind;
                foreach (var source in UIKitPanelCodeGenerator.BuildBindSources(next, bind))
                    if (!sources.ContainsKey(source.Key)) sources.Add(source.Key, source.Value);
            }
            finally { bind.Bind = saved; }
            string fullName = next.GetFullTypeName(UIKitBindCodeService.GetOutputKind(targetKind), typeName);
            UIKitConversionTransaction.Execute(bind, targetKind, next, fullName, relocations, sources);
        }

        /// <summary>Component 降为局部元素时必须具有可恢复的父作用域，独立公共组件不猜测所属 Panel。</summary>
        private static UIKitPanelCodeLayout ResolveElementParent(AbstractBind bind)
        {
            Transform current = bind.transform.parent;
            while (current != default)
            {
                AbstractBind ancestor = current.GetComponent<AbstractBind>();
                if (current.GetComponent<UIPanel>() != default || current.GetComponent<UIElement>() != default
                    || (ancestor != default && ancestor.Bind == BindType.Component))
                    return UIKitBindCodeService.ResolveLayout(bind, false);
                current = current.parent;
            }
            throw new InvalidOperationException("转为 Element 前，请把该 Component 放入所属 Panel 或 Component 的绑定层级。");
        }

        /// <summary>更新绑定类型及对应类型字段；Member 转换保留原脚本组件，不删除业务逻辑。</summary>
        internal static void SetKind(AbstractBind bind, BindType kind)
        {
            Undo.RecordObject(bind, "转换 UI 绑定");
            bind.Bind = kind;
            if (IsGenerated(kind)) bind.Type = UIKitBindCodeService.GetTypeName(bind);
            else if (kind == BindType.Leaf) bind.Type = string.Empty;
            EditorUtility.SetDirty(bind);
        }

        /// <summary>识别需要独立生成用户 partial 的两种绑定。</summary>
        private static bool IsGenerated(BindType kind) => kind == BindType.Element || kind == BindType.Component;

        /// <summary>收集根类型转换后作用域改变的子 Element，公共子 Component 仍保留独立作用域。</summary>
        private static void CollectChildren(AbstractBind bind, UIKitPanelCodeLayout previous, UIKitPanelCodeLayout next,
            BindType oldKind, BindType newKind, string typeName, List<TypeMove> moves)
        {
            UIKitBindScanResult scan = UIKitBindScanner.ScanOwner(bind.gameObject,
                oldKind == BindType.Element ? UIKitGeneratedOwnerKind.Element : UIKitGeneratedOwnerKind.Component);
            if (scan.HasErrors) throw new InvalidOperationException("请先修复绑定树错误，再执行类型转换。");
            CollectNodes(scan.Nodes, oldKind == BindType.Component ? previous.ForComponent(typeName) : previous,
                newKind == BindType.Component ? next.ForComponent(typeName) : next, moves);
        }

        /// <summary>递归建立旧类型到新类型的一对一迁移映射；重复实例共享同一个源码迁移。</summary>
        private static void CollectNodes(List<UIKitBindNode> nodes, UIKitPanelCodeLayout previous,
            UIKitPanelCodeLayout next, List<TypeMove> moves)
        {
            foreach (UIKitBindNode node in nodes)
            {
                if (node.Strategy.OutputKind == UIKitBindOutputKind.Component) continue;
                if (node.Strategy.OutputKind == UIKitBindOutputKind.Element)
                    AddMove(moves, previous, next, BindType.Element, BindType.Element, node.TypeName);
                CollectNodes(node.Children, previous, next, moves);
            }
        }

        /// <summary>验证迁移目标文件和程序集边界，并拒绝把两个已有业务类型静默合并。</summary>
        private static void AddMove(List<TypeMove> moves, UIKitPanelCodeLayout previous, UIKitPanelCodeLayout next,
            BindType oldKind, BindType newKind, string typeName)
        {
            string oldPath = UIKitBindCodeService.GetScriptPath(previous, oldKind, typeName, false);
            string newPath = UIKitBindCodeService.GetScriptPath(next, newKind, typeName, false);
            if (oldPath == newPath || !File.Exists(oldPath)) return;
            foreach (TypeMove existing in moves)
                if (existing.OldPath == oldPath) return;
            if (File.Exists(newPath) || File.Exists(newPath + ".meta"))
                throw new InvalidOperationException("转换目标已存在，不会覆盖用户代码: " + newPath);
            string oldAssembly = CompilationPipeline.GetAssemblyNameFromScriptPath(oldPath);
            string newAssembly = CompilationPipeline.GetAssemblyNameFromScriptPath(newPath);
            if (!string.Equals(oldAssembly, newAssembly, StringComparison.Ordinal))
                throw new InvalidOperationException("转换不能跨越程序集边界: " + oldPath + " -> " + newPath);
            moves.Add(new TypeMove
            {
                OldPath = oldPath, NewPath = newPath,
                OldName = previous.GetFullTypeName(UIKitBindCodeService.GetOutputKind(oldKind), typeName),
                NewName = next.GetFullTypeName(UIKitBindCodeService.GetOutputKind(newKind), typeName),
                NewBase = newKind == BindType.Element ? "UIElement" : "UIComponent",
            });
        }

        /// <summary>拒绝移动仍被其它绑定子树声明的类型；普通字段引用会随源码更新且保留原脚本 GUID。</summary>
        private static void RequireUnsharedBindings(AbstractBind selected, UIKitPanelCodeLayout layout, List<TypeMove> moves)
        {
            UIKitGeneratedOwnerCodeService.ResolvePrefab(selected, out _, out _, out string ownerPath);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (AbstractBind candidate in prefab.GetComponentsInChildren<AbstractBind>(true))
                {
                    UIElement component = candidate.GetComponent<UIElement>();
                    if (component == default || !IsGenerated(candidate.Bind)) continue;
                    foreach (TypeMove move in moves)
                    {
                        if (component.GetType().FullName != move.OldName) continue;
                        UIKitGeneratedOwnerCodeService.ResolvePrefabContext(candidate, prefab, path, out _, out _, out string candidatePath);
                        if (path == layout.PrefabPath && (ownerPath.Length == 0 || candidatePath == ownerPath
                            || candidatePath.StartsWith(ownerPath + "/", StringComparison.Ordinal))) continue;
                        throw new InvalidOperationException("类型仍被其它绑定使用，不能直接迁移: " + move.OldName + "，Prefab: " + path);
                    }
                }
            }
        }

        /// <summary>预构建迁移后的全部源码；写入前发现声明或路径冲突时原文件保持不变。</summary>
        internal static Dictionary<string, string> BuildSources(List<TypeMove> moves, Dictionary<string, string> relocations)
        {
            Dictionary<string, string> sources = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> remaining = FindRemainingNamespaces(moves);
            // 仅判断本次涉及的旧命名空间，其它 using 不属于转换所有权。
            bool NamespaceRemains(string name)
            {
                foreach (TypeMove move in moves)
                    if (name == move.OldNamespace) return remaining.Contains(name);
                return true;
            }
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".cs", StringComparison.Ordinal)) continue;
                string source = File.ReadAllText(path);
                TypeMove declaration = FindDeclaration(source, moves);
                string rewritten = UIKitConversionSource.Rewrite(source, moves, declaration, NamespaceRemains);
                string destination = path;
                if (declaration != null)
                {
                    destination = Path.GetDirectoryName(declaration.NewPath).Replace('\\', '/') + "/" + Path.GetFileName(path);
                    if (File.Exists(destination) || File.Exists(destination + ".meta"))
                        throw new InvalidOperationException("转换目标已存在: " + destination);
                    relocations.Add(path, destination);
                }
                if (source != rewritten || path != destination) sources.Add(destination, rewritten);
            }
            foreach (TypeMove move in moves)
                if (!relocations.ContainsKey(move.OldPath))
                    throw new InvalidOperationException("无法解析原用户 partial，未执行转换: " + move.OldPath);
            return sources;
        }

        /// <summary>定位属于待迁移类型的所有 partial，包括用户自行拆分但名称不同的 partial 文件。</summary>
        private static TypeMove FindDeclaration(string source, List<TypeMove> moves)
        {
            List<UIKitConversionSource.Token> tokens = UIKitConversionSource.Tokenize(source);
            string namespaceName = UIKitConversionSource.GetNamespace(tokens);
            foreach (TypeMove move in moves)
            {
                if (move.OldNamespace != namespaceName) continue;
                for (var index = 0; index < tokens.Count - 1; index++)
                    if (tokens[index].Text == "class" && tokens[index + 1].Text == move.Name) return move;
            }
            return null;
        }

        /// <summary>查明迁移后仍有类型的旧命名空间，保留业务代码对未迁移辅助类的引用。</summary>
        private static HashSet<string> FindRemainingNamespaces(List<TypeMove> moves)
        {
            HashSet<string> moved = new(StringComparer.Ordinal);
            foreach (TypeMove move in moves) moved.Add(move.OldName);
            HashSet<string> remaining = new(StringComparer.Ordinal);
            foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                Type type = script.GetClass();
                if (type != null && !moved.Contains(type.FullName) && !string.IsNullOrEmpty(type.Namespace))
                    remaining.Add(type.Namespace);
            }
            return remaining;
        }
    }
}
#endif
