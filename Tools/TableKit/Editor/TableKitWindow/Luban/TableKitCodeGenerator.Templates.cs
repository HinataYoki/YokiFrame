#if UNITY_EDITOR
using System.IO;
using System.Text;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitCodeGenerator - 代码模板生成
    /// </summary>
    public static partial class TableKitCodeGenerator
    {
        /// <summary>
        /// 生成 TableKit.cs 主文件
        /// </summary>
        private static void GenerateTableKit(string outputDir, string tablesNamespace, bool hasYokiFrame, 
            string runtimePathPattern, string editorDataPath)
        {
            // 转义路径中的特殊字符
            var escapedRuntimePath = runtimePathPattern.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var escapedEditorPath = editorDataPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
            
            var sb = new StringBuilder();
            AppendTableKitHeader(sb, tablesNamespace, escapedRuntimePath);
            AppendTableKitLoaderSetters(sb);
            AppendTableKitInit(sb, tablesNamespace);
            AppendTableKitLoadMethods(sb);
            AppendDefaultLoaders(sb, hasYokiFrame);
            AppendTableKitReloadAndClear(sb);
            AppendTableKitEditorSection(sb, tablesNamespace, escapedEditorPath);
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(outputDir, "TableKit.cs"), sb.ToString(), Encoding.UTF8);
        }

        private static void AppendTableKitHeader(StringBuilder sb, string tablesNamespace, string escapedRuntimePath)
        {
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
        }

        private static void AppendTableKitLoaderSetters(StringBuilder sb)
        {
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
        }

        private static void AppendTableKitInit(StringBuilder sb, string tablesNamespace)
        {
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
        }

        private static void AppendTableKitLoadMethods(StringBuilder sb)
        {
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
        }

        private static void AppendDefaultLoaders(StringBuilder sb, bool hasYokiFrame)
        {
            sb.AppendLine();
            sb.AppendLine("    #region 默认加载器");
            sb.AppendLine();

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
                sb.AppendLine("        var bytes = textAsset != null ? textAsset.bytes : null;");
                sb.AppendLine("        handler.Release();");
                sb.AppendLine("        return bytes;");
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
                sb.AppendLine("        var text = textAsset != null ? textAsset.text : null;");
                sb.AppendLine("        handler.Release();");
                sb.AppendLine("        return text;");
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
        }

        private static void AppendTableKitReloadAndClear(StringBuilder sb)
        {
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
        }

        private static void AppendTableKitEditorSection(StringBuilder sb, string tablesNamespace, string escapedEditorPath)
        {
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
        }

    }
}
#endif
