#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace YokiFrame.Unity
{
    /// <summary>
    /// TableKitCodeGenerator - 程序集定义与辅助文件生成
    /// </summary>
    public static partial class TableKitCodeGenerator
    {
        /// <summary>
        /// 生成 ExternalTypeUtil.cs
        /// </summary>
        private static void GenerateExternalTypeUtil(string outputDir, string tablesNamespace)
        {
            CodeGenKit.GenerateToFile(Path.Combine(outputDir, "ExternalTypeUtil.cs"), root =>
            {
                CodeGenLineBuilder sb = CodeGenKit.Lines(root);
                sb.AppendLine("using UnityEngine;");
                sb.AppendLine();
                sb.AppendLine($"namespace {tablesNamespace}");
                sb.AppendLine("{");
                sb.AppendLine("    /// <summary>");
                sb.AppendLine("    /// Luban 外部类型转换工具");
                sb.AppendLine("    /// 由 TableKit 工具自动生成");
                sb.AppendLine("    /// </summary>");
                sb.AppendLine("    public static class ExternalTypeUtil");
                sb.AppendLine("    {");
                sb.AppendLine("        public static Vector2 NewVector2(vector2 v) => new Vector2(v.X, v.Y);");
                sb.AppendLine("        public static Vector2Int NewVector2Int(vector2int v) => new Vector2Int(v.X, v.Y);");
                sb.AppendLine("        public static Vector3 NewVector3(vector3 v) => new Vector3(v.X, v.Y, v.Z);");
                sb.AppendLine("        public static Vector3Int NewVector3Int(vector3int v) => new Vector3Int(v.X, v.Y, v.Z);");
                sb.AppendLine("        public static Vector4 NewVector4(vector4 v) => new Vector4(v.X, v.Y, v.Z, v.W);");
                sb.AppendLine("    }");
                sb.AppendLine("}");
            });
        }

        /// <summary>
        /// 同步已存在的 ExternalTypeUtil.cs 命名空间至 tablesNamespace
        /// 仅替换包裹 ExternalTypeUtil 类的 namespace 声明行，方法体不变，保留用户自定义
        /// </summary>
        /// <returns>是否实际进行了替换</returns>
        private static bool SyncExternalTypeUtilNamespace(string utilPath, string tablesNamespace)
        {
            var content = File.ReadAllText(utilPath);

            var classMatch = Regex.Match(content, @"\bclass\s+ExternalTypeUtil\b");
            if (!classMatch.Success)
            {
                LogKit.Warning($"[TableKit] {utilPath} 未找到 ExternalTypeUtil 类声明，跳过命名空间同步");
                return false;
            }

            // 在 class 之前定位最近的一处 namespace 声明
            var contentBefore = content.Substring(0, classMatch.Index);
            var nsMatches = Regex.Matches(contentBefore, @"\bnamespace\s+([\w\.]+)");
            if (nsMatches.Count == 0)
            {
                LogKit.Warning($"[TableKit] {utilPath} 未找到命名空间声明，跳过同步");
                return false;
            }

            var lastNs = nsMatches[nsMatches.Count - 1];
            var currentNs = lastNs.Groups[1].Value;
            if (currentNs == tablesNamespace) return false;

            var newContent = content.Substring(0, lastNs.Index)
                + $"namespace {tablesNamespace}"
                + content.Substring(lastNs.Index + lastNs.Length);
            WriteGeneratedText(utilPath, newContent);
            return true;
        }

        /// <summary>
        /// 生成程序集定义文件
        /// </summary>
        private static void GenerateAssemblyDefinition(string outputDir, string assemblyName, bool hasYokiFrame,
            string codeTarget, bool useAsyncLoading = false)
        {
            // 生成的 TableKit.cs 默认使用 YokiFrame.ResKit / LogKit。
            var referencesList = new List<string> { "\"Luban.Runtime\"", "\"YokiFrame\"" };

            // 根据代码生成器类型添加对应的 JSON 库引用
            if (codeTarget == "cs-newtonsoft-json")
                referencesList.Add("\"Newtonsoft.Json\"");
            // cs-simple-json 使用 Luban.Runtime 内置的 SimpleJSON，无需额外引用
            // cs-bin 不需要 JSON 库

            // 异步加载需要 UniTask 引用
            if (useAsyncLoading)
                referencesList.Add("\"UniTask\"");

            var references = string.Join(",\n        ", referencesList);

            // 异步加载时添加 versionDefines 自动定义 YOKIFRAME_UNITASK_SUPPORT
            var versionDefines = "[]";
            if (useAsyncLoading)
            {
                versionDefines = @"[
        {
            ""name"": ""com.cysharp.unitask"",
            ""expression"": """",
            ""define"": ""YOKIFRAME_UNITASK_SUPPORT""
        }
    ]";
            }

            var content = $@"{{
    ""name"": ""{assemblyName}"",
    ""rootNamespace"": """",
    ""references"": [
        {references}
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": {versionDefines},
    ""noEngineReferences"": false
}}";
            WriteGeneratedText(Path.Combine(outputDir, $"{assemblyName}.asmdef"), content);
        }

        private static void WriteGeneratedText(string path, string content)
        {
            CodeGenKit.GenerateToFile(path, root =>
            {
                CodeGenLineBuilder sb = CodeGenKit.Lines(root);
                string normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
                string[] lines = normalized.Split('\n');
                int lineCount = lines.Length;
                if (lineCount > 0 && lines[lineCount - 1].Length == 0)
                {
                    lineCount--;
                }

                for (int i = 0; i < lineCount; i++)
                {
                    sb.AppendLine(lines[i]);
                }
            });
        }
    }
}
#endif
