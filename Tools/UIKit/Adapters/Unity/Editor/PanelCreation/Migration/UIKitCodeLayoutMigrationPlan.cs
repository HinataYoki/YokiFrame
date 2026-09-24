#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>只读构建事务内旧目录到新目录的有序移动计划；不改源码、类型名或程序集。</summary>
    internal sealed class UIKitCodeLayoutMigrationPlan
    {
        internal string Root { get; }
        internal List<Move> Moves { get; } = new();
        internal List<FileState> Files { get; } = new();
        private readonly Dictionary<string, string> mProjection = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>读取共同生成根的文件清单，拒绝越界、重解析点与程序集定义以免移动改变编译边界。</summary>
        internal UIKitCodeLayoutMigrationPlan(string root)
        {
            Root = root.Replace('\\', '/').TrimEnd('/');
            if (!UIKitPanelCodeLayout.IsUnityAssetPath(Root) || Root.Contains("..")
                || !AssetDatabase.IsValidFolder(Root))
                throw new InvalidOperationException("请选择项目 Assets 或 Packages 内明确的共同脚本根目录。");
            for (string ancestor = Root; !string.IsNullOrEmpty(ancestor); ancestor = Path.GetDirectoryName(ancestor))
                RejectLink(ancestor);
            Collect(Root);
        }

        /// <summary>逐层读取目录；先拒绝链接再递归，避免枚举逃逸选中范围。</summary>
        private void Collect(string directory)
        {
            string absoluteDirectory = UIKitPanelCodeLayout.ToAbsolutePath(directory);
            foreach (string entry in Directory.GetFileSystemEntries(absoluteDirectory))
            {
                string path = UIKitPanelCodeLayout.ToAssetPath(entry);
                RejectLink(path);
                mProjection.Add(path, path);
                if (Directory.Exists(entry)) Collect(path);
            }
        }

        /// <summary>路径迁移不跟随链接，避免脚本或资源落到授权根之外。</summary>
        private static void RejectLink(string path)
        {
            string absolutePath = UIKitPanelCodeLayout.ToAbsolutePath(path);
            if ((File.GetAttributes(absolutePath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("目录迁移不支持重解析点: " + path);
        }

        /// <summary>把一步移动应用到内存投影，连同目录 .meta 计算最终位置并提前拒绝冲突。</summary>
        internal void Add(string source, string destination)
        {
            if (!source.StartsWith(Root + "/", StringComparison.Ordinal)
                || !destination.StartsWith(Root + "/", StringComparison.Ordinal)
                || source.Contains("..") || destination.Contains(".."))
                throw new InvalidOperationException("迁移路径超出选中根目录。");
            string currentSource = ResolveCurrentPath(source);
            foreach (string current in mProjection.Values)
                if (string.Equals(current, destination, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(current, destination + ".meta", StringComparison.OrdinalIgnoreCase)
                    || current.StartsWith(destination + "/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("迁移目标已存在，不会覆盖: " + destination);
            foreach (string original in new List<string>(mProjection.Keys))
            {
                string current = mProjection[original];
                if (current == currentSource || current == currentSource + ".meta"
                    || current.StartsWith(currentSource + "/", StringComparison.Ordinal))
                    mProjection[original] = destination + current.Substring(currentSource.Length);
            }
            Moves.Add(new Move { source = currentSource, destination = destination });
        }

        /// <summary>把原始计划路径解析为前序目录移动后的当前路径，支持父目录与子目录连续迁移。</summary>
        private string ResolveCurrentPath(string source)
        {
            if (mProjection.TryGetValue(source, out string current))
                return current;
            foreach (string value in mProjection.Values)
                if (string.Equals(value, source, StringComparison.Ordinal))
                    return value;
            throw new InvalidOperationException("迁移源不存在: " + source);
        }

        /// <summary>冻结完整文件证据，验证程序集、只读属性和未导入文件；确认后写入前必须再校验。</summary>
        internal void Validate()
        {
            Files.Clear();
            foreach (var entry in mProjection)
            {
                if (entry.Key == entry.Value || Directory.Exists(UIKitPanelCodeLayout.ToAbsolutePath(entry.Key))) continue;
                string extension = Path.GetExtension(entry.Key);
                if (extension == ".asmdef" || extension == ".asmref")
                    throw new InvalidOperationException("迁移范围含程序集定义，需先人工规划: " + entry.Key);
                string absoluteSource = UIKitPanelCodeLayout.ToAbsolutePath(entry.Key);
                if ((File.GetAttributes(absoluteSource) & FileAttributes.ReadOnly) != 0)
                    throw new InvalidOperationException("迁移源只读: " + entry.Key);
                bool meta = entry.Key.EndsWith(".meta", StringComparison.Ordinal);
                string guid = meta ? string.Empty : AssetDatabase.AssetPathToGUID(entry.Key);
                if (!meta && (string.IsNullOrEmpty(guid)
                    || !File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(entry.Key + ".meta"))))
                    throw new InvalidOperationException("请先完成资源导入再迁移: " + entry.Key);
                if (extension == ".cs" && CompilationPipeline.GetAssemblyNameFromScriptPath(entry.Key)
                    != CompilationPipeline.GetAssemblyNameFromScriptPath(entry.Value))
                    throw new InvalidOperationException("迁移不能改变程序集: " + entry.Key + " -> " + entry.Value);
                MonoScript script = extension == ".cs" ? AssetDatabase.LoadAssetAtPath<MonoScript>(entry.Key) : default;
                Type type = script == default ? null : script.GetClass();
                Files.Add(new FileState { source = entry.Key, destination = entry.Value, guid = guid,
                    bytes = Convert.ToBase64String(File.ReadAllBytes(absoluteSource)), type = type?.AssemblyQualifiedName });
            }
        }

        /// <summary>形成完整移动预览；文件数量包含伴随文件与 GUID 元数据。</summary>
        internal string Describe()
        {
            var lines = new List<string> { "共同根: " + Root, "文件（含 .meta）: " + Files.Count.ToString(),
                "不修改源码、命名空间或程序集；编译或验证失败将回滚。", string.Empty };
            foreach (FileState file in Files) lines.Add(file.source + "\n  → " + file.destination);
            return string.Join("\n", lines);
        }

        [Serializable]
        internal sealed class Move { public string source; public string destination; }

        [Serializable]
        internal sealed class FileState
        {
            public string source;
            public string destination;
            public string guid;
            public string bytes;
            public string type;
        }
    }
}
#endif
