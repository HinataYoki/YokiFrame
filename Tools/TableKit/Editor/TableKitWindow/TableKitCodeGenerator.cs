#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEngine;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKit 代码生成器
    /// 在 Luban 生成代码后，生成配套的 TableKit 运行时代码
    /// </summary>
    public static class TableKitCodeGenerator
    {
        /// <summary>
        /// 生成所有 TableKit 运行时代码
        /// </summary>
        /// <param name="outputDir">输出目录（与 Luban 代码相同目录）</param>
        /// <param name="useAssemblyDefinition">是否生成独立程序集</param>
        /// <param name="generateExternalTypeUtil">是否生成 ExternalTypeUtil</param>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="tablesNamespace">Tables 类的命名空间，默认 cfg</param>
        public static void Generate(
            string outputDir,
            bool useAssemblyDefinition,
            bool generateExternalTypeUtil,
            string assemblyName = "YokiFrame.TableKit",
            string tablesNamespace = "cfg")
        {
            if (string.IsNullOrEmpty(outputDir))
            {
                Debug.LogError("[TableKit] 输出目录不能为空");
                return;
            }

            // 确保目录存在
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 生成文件
            GenerateTableKit(outputDir, tablesNamespace);
            
            if (generateExternalTypeUtil)
            {
                // 只在文件不存在时生成，避免覆盖用户自定义的转换逻辑
                var utilPath = Path.Combine(outputDir, "ExternalTypeUtil.cs");
                if (!File.Exists(utilPath))
                {
                    GenerateExternalTypeUtil(outputDir);
                    Debug.Log("[TableKit] 已生成 ExternalTypeUtil.cs");
                }
            }

            if (useAssemblyDefinition)
            {
                // 先清理目录下所有旧的 asmdef 文件，避免改名后残留
                CleanupOldAsmdef(outputDir, assemblyName);
                GenerateAssemblyDefinition(outputDir, assemblyName);
            }
            else
            {
                // 如果不使用独立程序集，删除目录下所有 asmdef 文件
                CleanupOldAsmdef(outputDir, null);
            }
            
            // 清理旧文件
            CleanupOldFiles(outputDir);

            Debug.Log($"[TableKit] 代码生成完成: {outputDir}");
        }

        /// <summary>
        /// 清理旧版本生成的文件
        /// </summary>
        private static void CleanupOldFiles(string outputDir)
        {
            var oldFiles = new[]
            {
                "ITableLoader.cs",
                "TableLoadMode.cs",
                "TableExtensions.cs"
            };

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

            // 删除旧的 Loaders 目录
            var loadersDir = Path.Combine(outputDir, "Loaders");
            if (Directory.Exists(loadersDir))
            {
                Directory.Delete(loadersDir, true);
                var metaPath = loadersDir + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }
        }

        /// <summary>
        /// 清理旧的 asmdef 文件
        /// </summary>
        /// <param name="outputDir">输出目录</param>
        /// <param name="keepAssemblyName">要保留的程序集名称，null 表示删除所有</param>
        private static void CleanupOldAsmdef(string outputDir, string keepAssemblyName)
        {
            var asmdefFiles = Directory.GetFiles(outputDir, "*.asmdef", SearchOption.TopDirectoryOnly);
            foreach (var asmdefPath in asmdefFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(asmdefPath);
                // 如果指定了保留名称且当前文件就是要保留的，跳过
                if (!string.IsNullOrEmpty(keepAssemblyName) && fileName == keepAssemblyName)
                {
                    continue;
                }
                
                File.Delete(asmdefPath);
                var metaPath = asmdefPath + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }
        }

        private static void GenerateTableKit(string outputDir, string tablesNamespace)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using Luban;");
            sb.AppendLine("using SimpleJSON;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// 配置表系统入口类");
            sb.AppendLine("/// 由 TableKit 工具自动生成");
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
            sb.AppendLine("    /// <param name=\"loader\">加载器委托，参数为文件名（不含扩展名），返回二进制数据</param>");
            sb.AppendLine("    public static void SetBinaryLoader(Func<string, byte[]> loader)");
            sb.AppendLine("    {");
            sb.AppendLine("        sBinaryLoader = loader ?? throw new ArgumentNullException(nameof(loader));");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 设置 JSON 数据加载器");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"loader\">加载器委托，参数为文件名（不含扩展名），返回 JSON 字符串</param>");
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
            sb.AppendLine("        // 如果未设置加载器，使用默认加载器");
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
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 运行时路径模式，{0} 会被替换为文件名");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static string sRuntimePathPattern = \"{0}\";");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 设置运行时路径模式");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static void SetRuntimePathPattern(string pattern) => sRuntimePathPattern = pattern ?? \"{0}\";");
            sb.AppendLine();
            sb.AppendLine("#if YOKIFRAME");
            sb.AppendLine("    // YokiFrame 环境：使用 ResKit 加载");
            sb.AppendLine("    private static byte[] DefaultBinaryLoader(string fileName)");
            sb.AppendLine("    {");
            sb.AppendLine("        var path = string.Format(sRuntimePathPattern, fileName);");
            sb.AppendLine("        var handler = YokiFrame.ResKit.LoadAsset<TextAsset>(path);");
            sb.AppendLine("        if (handler?.Asset == null)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.LogError($\"[TableKit] ResKit 加载失败: {path}\");");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine("        return handler.Asset.bytes;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static string DefaultJsonLoader(string fileName)");
            sb.AppendLine("    {");
            sb.AppendLine("        var path = string.Format(sRuntimePathPattern, fileName);");
            sb.AppendLine("        var handler = YokiFrame.ResKit.LoadAsset<TextAsset>(path);");
            sb.AppendLine("        if (handler?.Asset == null)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.LogError($\"[TableKit] ResKit 加载失败: {path}\");");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine("        return handler.Asset.text;");
            sb.AppendLine("    }");
            sb.AppendLine("#else");
            sb.AppendLine("    // 非 YokiFrame 环境：使用 Resources 加载");
            sb.AppendLine("    private static byte[] DefaultBinaryLoader(string fileName)");
            sb.AppendLine("    {");
            sb.AppendLine("        var path = string.Format(sRuntimePathPattern, fileName);");
            sb.AppendLine("        var asset = Resources.Load<TextAsset>(path);");
            sb.AppendLine("        return asset != null ? asset.bytes : null;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static string DefaultJsonLoader(string fileName)");
            sb.AppendLine("    {");
            sb.AppendLine("        var path = string.Format(sRuntimePathPattern, fileName);");
            sb.AppendLine("        var asset = Resources.Load<TextAsset>(path);");
            sb.AppendLine("        return asset != null ? asset.text : null;");
            sb.AppendLine("    }");
            sb.AppendLine("#endif");
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
            sb.AppendLine("    private static string sEditorDataPath = \"Assets/Art/Table/\";");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 设置编辑器数据路径");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static void SetEditorDataPath(string path) => sEditorDataPath = path;");
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
            sb.AppendLine("        var path = $\"{sEditorDataPath}{fileName}.json\";");
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
            sb.AppendLine("        var path = $\"{sEditorDataPath}{fileName}.bytes\";");
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
    /// 用于将 Luban 生成的 vector 类型转换为 Unity 的 Vector 类型
    /// 由 TableKit 工具自动生成，请勿手动修改
    /// </summary>
    public static class ExternalTypeUtil
    {
        public static Vector2 NewVector2(vector2 v)
        {
            return new Vector2(v.X, v.Y);
        }

        public static Vector2Int NewVector2Int(vector2int v)
        {
            return new Vector2Int(v.X, v.Y);
        }

        public static Vector3 NewVector3(vector3 v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static Vector3Int NewVector3Int(vector3int v)
        {
            return new Vector3Int(v.X, v.Y, v.Z);
        }

        public static Vector4 NewVector4(vector4 v)
        {
            return new Vector4(v.X, v.Y, v.Z, v.W);
        }
    }
}
";
            File.WriteAllText(Path.Combine(outputDir, "ExternalTypeUtil.cs"), content, Encoding.UTF8);
        }

        private static void GenerateAssemblyDefinition(string outputDir, string assemblyName)
        {
            var content = $@"{{
    ""name"": ""{assemblyName}"",
    ""rootNamespace"": """",
    ""references"": [
        ""Luban.Runtime"",
        ""YokiFrame""
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [
        {{
            ""name"": ""com.hinatayoki.yokiframe"",
            ""expression"": """",
            ""define"": ""YOKIFRAME""
        }}
    ],
    ""noEngineReferences"": false
}}";
            File.WriteAllText(Path.Combine(outputDir, $"{assemblyName}.asmdef"), content, Encoding.UTF8);
        }
    }
}
#endif
