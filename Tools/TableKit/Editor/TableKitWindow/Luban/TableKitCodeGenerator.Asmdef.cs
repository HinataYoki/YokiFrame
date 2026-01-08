#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitCodeGenerator - 程序集定义与辅助文件生成
    /// </summary>
    public static partial class TableKitCodeGenerator
    {
        /// <summary>
        /// 生成 ExternalTypeUtil.cs
        /// </summary>
        private static void GenerateExternalTypeUtil(string outputDir)
        {
            var content = @"using UnityEngine;

namespace cfg
{
    /// <summary>
    /// Luban 外部类型转换工具
    /// 由 TableKit 工具自动生成
    /// </summary>
    public static class ExternalTypeUtil
    {
        public static Vector2 NewVector2(vector2 v) => new(v.X, v.Y);
        public static Vector2Int NewVector2Int(vector2int v) => new(v.X, v.Y);
        public static Vector3 NewVector3(vector3 v) => new(v.X, v.Y, v.Z);
        public static Vector3Int NewVector3Int(vector3int v) => new(v.X, v.Y, v.Z);
        public static Vector4 NewVector4(vector4 v) => new(v.X, v.Y, v.Z, v.W);
    }
}
";
            File.WriteAllText(Path.Combine(outputDir, "ExternalTypeUtil.cs"), content, Encoding.UTF8);
        }

        /// <summary>
        /// 生成程序集定义文件
        /// </summary>
        private static void GenerateAssemblyDefinition(string outputDir, string assemblyName, bool hasYokiFrame, string codeTarget)
        {
            // 基础引用
            var referencesList = new List<string> { "\"Luban.Runtime\"" };
            
            if (hasYokiFrame)
                referencesList.Add("\"YokiFrame\"");
            
            // 根据代码生成器类型添加对应的 JSON 库引用
            if (codeTarget == "cs-newtonsoft-json")
                referencesList.Add("\"Newtonsoft.Json\"");
            // cs-simple-json 使用 Luban.Runtime 内置的 SimpleJSON，无需额外引用
            // cs-bin 不需要 JSON 库

            var references = string.Join(",\n        ", referencesList);

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
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";
            File.WriteAllText(Path.Combine(outputDir, $"{assemblyName}.asmdef"), content, Encoding.UTF8);
        }
    }
}
#endif
