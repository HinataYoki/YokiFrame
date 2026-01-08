#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKit 代码生成器
    /// 在 Luban 生成代码后，生成配套的 TableKit 运行时代码
    /// 生成的代码完全独立，不依赖任何外部配置文件
    /// </summary>
    public static class TableKitCodeGenerator
    {
        /// <summary>
        /// 检测 YokiFrame 是否存在（作为 Package 或 Assets 文件夹）
        /// </summary>
        private static bool DetectYokiFrame()
        {
            var packagePath = "Packages/com.hinatayoki.yokiframe";
            if (Directory.Exists(packagePath)) return true;
            
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset YokiFrame");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == "YokiFrame")
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 生成所有 TableKit 运行时代码
        /// </summary>
        /// <param name="outputDir">输出目录</param>
        /// <param name="useAssemblyDefinition">是否生成 asmdef</param>
        /// <param name="generateExternalTypeUtil">是否生成外部类型工具</param>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="tablesNamespace">Tables 命名空间</param>
        /// <param name="runtimePathPattern">运行时路径模式，将嵌入生成代码</param>
        /// <param name="editorDataPath">编辑器数据路径，将嵌入生成代码</param>
        /// <param name="codeTarget">代码生成器类型，用于确定程序集引用</param>
        public static void Generate(
            string outputDir,
            bool useAssemblyDefinition,
            bool generateExternalTypeUtil,
            string assemblyName = "YokiFrame.TableKit",
            string tablesNamespace = "cfg",
            string runtimePathPattern = "{0}",
            string editorDataPath = "Assets/Art/Table/",
            string codeTarget = "cs-bin")
        {
            if (string.IsNullOrEmpty(outputDir))
            {
                Debug.LogError("[TableKit] 输出目录不能为空");
                return;
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 使用默认值
            if (string.IsNullOrEmpty(runtimePathPattern)) runtimePathPattern = "{0}";
            if (string.IsNullOrEmpty(editorDataPath)) editorDataPath = "Assets/Art/Table/";

            var hasYokiFrame = DetectYokiFrame();
            GenerateTableKit(outputDir, tablesNamespace, hasYokiFrame, runtimePathPattern, editorDataPath);
            
            if (generateExternalTypeUtil)
            {
                var utilPath = Path.Combine(outputDir, "ExternalTypeUtil.cs");
                if (!File.Exists(utilPath))
                {
                    GenerateExternalTypeUtil(outputDir);
                    Debug.Log("[TableKit] 已生成 ExternalTypeUtil.cs");
                }
            }

            if (useAssemblyDefinition)
            {
                CleanupOldAsmdef(outputDir, assemblyName);
                GenerateAssemblyDefinition(outputDir, assemblyName, hasYokiFrame, codeTarget);
            }
            else
            {
                CleanupOldAsmdef(outputDir, null);
            }
            
            CleanupOldFiles(outputDir);
            Debug.Log($"[TableKit] 代码生成完成: {outputDir}");
        }

        private static void CleanupOldFiles(string outputDir)
        {
            var oldFiles = new[] { "ITableLoader.cs", "TableLoadMode.cs", "TableExtensions.cs" };
            foreach (var file in oldFiles)
            {
                var path = Path.Combine(outputDir, file);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    var metaPath = path + ".meta";
                    if (File.Exists(metaPath)) File.Delete(metaPath);
                }
            }

            var loadersDir = Path.Combine(outputDir, "Loaders");
            if (Directory.Exists(loadersDir))
            {
                Directory.Delete(loadersDir, true);
                var metaPath = loadersDir + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }
        }

        private static void CleanupOldAsmdef(string outputDir, string keepAssemblyName)
        {
            var asmdefFiles = Directory.GetFiles(outputDir, "*.asmdef", SearchOption.TopDirectoryOnly);
            foreach (var asmdefPath in asmdefFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(asmdefPath);
                if (!string.IsNullOrEmpty(keepAssemblyName) && fileName == keepAssemblyName) continue;
                
                File.Delete(asmdefPath);
                var metaPath = asmdefPath + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }
        }

        private static void GenerateTableKit(string outputDir, string tablesNamespace, bool hasYokiFrame, string runtimePathPattern, string editorDataPath)
        {
            // 转义路径中的特殊字符
            var escapedRuntimePath = runtimePathPattern.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var escapedEditorPath = editorDataPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
            
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using Luban;");
            sb.AppendLine("using SimpleJSON;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// 配置表系统入口类");
            sb.AppendLine("/// 由 TableKit 工具自动生成，路径配置已嵌入代码");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public static class TableKit");
            sb.AppendLine("{");
            sb.AppendLine($"    private static {tablesNamespace}.Tables sTables;");
            sb.AppendLine("    private static Func<string, byte[]> sBinaryLoader;");
            sb.AppendLine("    private static Func<string, string> sJsonLoader;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 是否已初始化");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static bool Initialized { get; private set; }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 运行时路径模式（生成时嵌入）");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public static string RuntimePathPattern {{ get; set; }} = \"{escapedRuntimePath}\";");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 获取配置表实例");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public static {tablesNamespace}.Tables Tables");
            sb.AppendLine("    {");
            sb.AppendLine("        get");
            sb.AppendLine("        {");
            sb.AppendLine("            if (sTables == null) Init();");
            sb.AppendLine("            return sTables;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 设置二进制数据加载器");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static void SetBinaryLoader(Func<string, byte[]> loader)");
            sb.AppendLine("    {");
            sb.AppendLine("        sBinaryLoader = loader ?? throw new ArgumentNullException(nameof(loader));");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 设置 JSON 数据加载器");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static void SetJsonLoader(Func<string, string> loader)");
            sb.AppendLine("    {");
            sb.AppendLine("        sJsonLoader = loader ?? throw new ArgumentNullException(nameof(loader));");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 初始化配置表");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static void Init()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (Initialized) return;");
            sb.AppendLine();
            sb.AppendLine("        if (sBinaryLoader == null) sBinaryLoader = DefaultBinaryLoader;");
            sb.AppendLine("        if (sJsonLoader == null) sJsonLoader = DefaultJsonLoader;");
            sb.AppendLine();
            sb.AppendLine($"        var tablesCtor = typeof({tablesNamespace}.Tables).GetConstructors()[0];");
            sb.AppendLine("        var loaderReturnType = tablesCtor.GetParameters()[0].ParameterType.GetGenericArguments()[1];");
            sb.AppendLine();
            sb.AppendLine("        object loader = loaderReturnType == typeof(ByteBuf)");
            sb.AppendLine("            ? new Func<string, ByteBuf>(LoadBinary)");
            sb.AppendLine("            : new Func<string, JSONNode>(LoadJson);");
            sb.AppendLine();
            sb.AppendLine($"        sTables = ({tablesNamespace}.Tables)tablesCtor.Invoke(new object[] {{ loader }});");
            sb.AppendLine("        Initialized = true;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static JSONNode LoadJson(string fileName)");
            sb.AppendLine("    {");
            sb.AppendLine("        var json = sJsonLoader(fileName);");
            sb.AppendLine("        if (string.IsNullOrEmpty(json))");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.LogError($\"[TableKit] 加载配置表失败: {fileName}\");");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine("        return JSON.Parse(json);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static ByteBuf LoadBinary(string fileName)");
            sb.AppendLine("    {");
            sb.AppendLine("        var bytes = sBinaryLoader(fileName);");
            sb.AppendLine("        if (bytes == null || bytes.Length == 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.LogError($\"[TableKit] 加载配置表失败: {fileName}\");");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine("        return new ByteBuf(bytes);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #region 默认加载器");
            sb.AppendLine();

            // 根据 YokiFrame 检测结果生成不同的默认加载器
            if (hasYokiFrame)
            {
                sb.AppendLine("    // 默认加载器：使用 YokiFrame.ResKit");
                sb.AppendLine("    private static byte[] DefaultBinaryLoader(string fileName)");
                sb.AppendLine("    {");
                sb.AppendLine("        var path = string.Format(RuntimePathPattern, fileName);");
                sb.AppendLine("        var handler = YokiFrame.ResKit.LoadAsset<TextAsset>(path);");
                sb.AppendLine("        if (handler == null)");
                sb.AppendLine("        {");
                sb.AppendLine("            Debug.LogError($\"[TableKit] ResKit 加载失败: {path}\");");
                sb.AppendLine("            return null;");
                sb.AppendLine("        }");
                sb.AppendLine("        var textAsset = handler.Asset as TextAsset;");
                sb.AppendLine("        return textAsset != null ? textAsset.bytes : null;");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    private static string DefaultJsonLoader(string fileName)");
                sb.AppendLine("    {");
                sb.AppendLine("        var path = string.Format(RuntimePathPattern, fileName);");
                sb.AppendLine("        var handler = YokiFrame.ResKit.LoadAsset<TextAsset>(path);");
                sb.AppendLine("        if (handler == null)");
                sb.AppendLine("        {");
                sb.AppendLine("            Debug.LogError($\"[TableKit] ResKit 加载失败: {path}\");");
                sb.AppendLine("            return null;");
                sb.AppendLine("        }");
                sb.AppendLine("        var textAsset = handler.Asset as TextAsset;");
                sb.AppendLine("        return textAsset != null ? textAsset.text : null;");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine("    // 默认加载器：使用 Resources");
                sb.AppendLine("    private static byte[] DefaultBinaryLoader(string fileName)");
                sb.AppendLine("    {");
                sb.AppendLine("        var path = string.Format(RuntimePathPattern, fileName);");
                sb.AppendLine("        var asset = Resources.Load<TextAsset>(path);");
                sb.AppendLine("        return asset != null ? asset.bytes : null;");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    private static string DefaultJsonLoader(string fileName)");
                sb.AppendLine("    {");
                sb.AppendLine("        var path = string.Format(RuntimePathPattern, fileName);");
                sb.AppendLine("        var asset = Resources.Load<TextAsset>(path);");
                sb.AppendLine("        return asset != null ? asset.text : null;");
                sb.AppendLine("    }");
            }
            
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 重新加载配置表");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static void Reload(Action onComplete = null)");
            sb.AppendLine("    {");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine("            sTables = null;");
            sb.AppendLine("            Initialized = false;");
            sb.AppendLine("            Init();");
            sb.AppendLine("        }");
            sb.AppendLine("        catch (Exception ex)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.LogError($\"[TableKit] Reload failed: {ex.Message}\");");
            sb.AppendLine("        }");
            sb.AppendLine("        onComplete?.Invoke();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 清理所有数据");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static void Clear()");
            sb.AppendLine("    {");
            sb.AppendLine("        sTables = null;");
            sb.AppendLine("        Initialized = false;");
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("        sTablesEditor = null;");
            sb.AppendLine("#endif");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine($"    private static {tablesNamespace}.Tables sTablesEditor;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 编辑器数据路径（生成时嵌入）");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public static string EditorDataPath {{ get; set; }} = \"{escapedEditorPath}\";");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 获取编辑器模式下的配置表实例");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public static {tablesNamespace}.Tables TablesEditor");
            sb.AppendLine("    {");
            sb.AppendLine("        get");
            sb.AppendLine("        {");
            sb.AppendLine("            if (sTablesEditor == null) InitEditor();");
            sb.AppendLine("            return sTablesEditor;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static void InitEditor()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (sTablesEditor != null) return;");
            sb.AppendLine();
            sb.AppendLine($"        var tablesCtor = typeof({tablesNamespace}.Tables).GetConstructors()[0];");
            sb.AppendLine("        var loaderReturnType = tablesCtor.GetParameters()[0].ParameterType.GetGenericArguments()[1];");
            sb.AppendLine();
            sb.AppendLine("        object loader = loaderReturnType == typeof(ByteBuf)");
            sb.AppendLine("            ? new Func<string, ByteBuf>(LoadBinaryEditor)");
            sb.AppendLine("            : new Func<string, JSONNode>(LoadJsonEditor);");
            sb.AppendLine();
            sb.AppendLine($"        sTablesEditor = ({tablesNamespace}.Tables)tablesCtor.Invoke(new object[] {{ loader }});");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static JSONNode LoadJsonEditor(string fileName)");
            sb.AppendLine("    {");
            sb.AppendLine("        var path = $\"{EditorDataPath}{fileName}.json\";");
            sb.AppendLine("        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);");
            sb.AppendLine("        if (asset == null)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.LogError($\"[TableKit] 编辑器加载配置表失败: {path}\");");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine("        return JSON.Parse(asset.text);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static ByteBuf LoadBinaryEditor(string fileName)");
            sb.AppendLine("    {");
            sb.AppendLine("        var path = $\"{EditorDataPath}{fileName}.bytes\";");
            sb.AppendLine("        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);");
            sb.AppendLine("        if (asset == null)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.LogError($\"[TableKit] 编辑器加载配置表失败: {path}\");");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine("        return new ByteBuf(asset.bytes);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 刷新编辑器缓存");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static void RefreshEditor() => sTablesEditor = null;");
            sb.AppendLine("#endif");
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(outputDir, "TableKit.cs"), sb.ToString(), Encoding.UTF8);
        }

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
