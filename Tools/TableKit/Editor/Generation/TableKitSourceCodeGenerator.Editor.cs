using System;

namespace YokiFrame
{
    public static partial class TableKitSourceCodeGenerator
    {
        /// <summary>写入仅在 Unity Editor 或 Godot Tools 中存在的独立表缓存与文件读取入口。</summary>
        /// <param name="source">目标逐行生成器。</param>
        /// <param name="managerTypeName">完整 Luban manager 类型表达式。</param>
        /// <param name="editorPathLiteral">已转义的 Editor 默认数据路径。</param>
        private static void AppendFacadeEditorApi(
            CodeGenLineBuilder source,
            string managerTypeName,
            string editorPathLiteral)
        {
            source.AppendLine()
                .AppendLine("#if UNITY_EDITOR || (GODOT && TOOLS)")
                .AppendLine("        private static " + managerTypeName + " sTablesEditor;")
                .AppendLine("        private static string sEditorDataPath = \"" + editorPathLiteral + "\";")
                .AppendLine("        private static MethodInfo sEditorJsonParseMethod;")
                .AppendLine()
                .AppendLine("        /// <summary>获取或设置 Editor/Tools 直接读取配置表的数据目录。</summary>")
                .AppendLine("        public static string EditorDataPath")
                .AppendLine("        {")
                .AppendLine("            get => sEditorDataPath;")
                .AppendLine("            set => SetEditorDataPath(value);")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>获取 Editor/Tools 独立缓存的 Luban 表管理器。</summary>")
                .AppendLine("        public static " + managerTypeName + " TablesEditor")
                .AppendLine("        {")
                .AppendLine("            get")
                .AppendLine("            {")
                .AppendLine("                if (sTablesEditor == null) InitializeEditorTables();")
                .AppendLine("                return sTablesEditor;")
                .AppendLine("            }")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>设置 Editor 数据目录并清理此前缓存。</summary>")
                .AppendLine("        /// <param name=\"path\">项目相对目录、Godot `res://` 目录或绝对目录。</param>")
                .AppendLine("        public static void SetEditorDataPath(string path)")
                .AppendLine("        {")
                .AppendLine("            sEditorDataPath = NormalizeEditorDataPath(path);")
                .AppendLine("            sTablesEditor = null;")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>清理 Editor 表缓存；下次访问 TablesEditor 时重新读取数据。</summary>")
                .AppendLine("        public static void RefreshEditor()")
                .AppendLine("        {")
                .AppendLine("            sTablesEditor = null;")
                .AppendLine("        }")
                .AppendLine();
            AppendEditorInitialization(source, managerTypeName);
            AppendEditorJsonLoaderFactory(source);
            AppendEditorJsonParser(source);
            AppendEditorDataPathApi(source);
            AppendEditorFileReaders(source);
            AppendEditorGodotPathResolver(source);
            source.AppendLine("#endif");
        }

        /// <summary>写入 Editor manager 构造函数识别和 Loader 委托创建逻辑。</summary>
        /// <param name="source">目标逐行生成器。</param>
        /// <param name="managerTypeName">完整 Luban manager 类型表达式。</param>
        private static void AppendEditorInitialization(CodeGenLineBuilder source, string managerTypeName)
        {
            source.AppendLine("        /// <summary>按 Luban manager 构造函数创建 Editor 独立表缓存。</summary>")
                .AppendLine("        private static void InitializeEditorTables()")
                .AppendLine("        {")
                .AppendLine("            if (sTablesEditor != null) return;")
                .AppendLine("            ConstructorInfo tablesConstructor = FindEditorTablesConstructor();")
                .AppendLine("            Type loaderReturnType = tablesConstructor.GetParameters()[0].ParameterType.GetGenericArguments()[1];")
                .AppendLine("            object loader = loaderReturnType == typeof(global::Luban.ByteBuf)")
                .AppendLine("                ? new Func<string, global::Luban.ByteBuf>(LoadBinaryEditor)")
                .AppendLine("                : CreateEditorJsonLoader(loaderReturnType);")
                .AppendLine("            sTablesEditor = (" + managerTypeName + ")tablesConstructor.Invoke(new object[] { loader });")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>查找唯一接受 `Func&lt;string, T&gt;` Loader 的 Luban manager 构造函数。</summary>")
                .AppendLine("        /// <returns>匹配当前 Luban target 的 manager 构造函数。</returns>")
                .AppendLine("        private static ConstructorInfo FindEditorTablesConstructor()")
                .AppendLine("        {")
                .AppendLine("            ConstructorInfo[] constructors = typeof(" + managerTypeName + ").GetConstructors();")
                .AppendLine("            for (int index = 0; index < constructors.Length; index++)")
                .AppendLine("            {")
                .AppendLine("                ParameterInfo[] parameters = constructors[index].GetParameters();")
                .AppendLine("                if (parameters.Length != 1) continue;")
                .AppendLine("                Type loaderType = parameters[0].ParameterType;")
                .AppendLine("                if (!loaderType.IsGenericType || loaderType.GetGenericTypeDefinition() != typeof(Func<,>)) continue;")
                .AppendLine("                Type[] arguments = loaderType.GetGenericArguments();")
                .AppendLine("                if (arguments[0] == typeof(string)) return constructors[index];")
                .AppendLine("            }")
                .AppendLine("            throw new InvalidOperationException(\"TableKit 无法找到 Luban manager 的单参数 Loader 构造函数。\");")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 JSON Loader 的泛型委托适配与单表读取逻辑。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendEditorJsonLoaderFactory(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>按 Luban manager 要求的 JSON 节点类型创建强类型 Loader 委托。</summary>")
                .AppendLine("        /// <param name=\"jsonNodeType\">manager 构造函数声明的 JSON 返回类型。</param>")
                .AppendLine("        /// <returns>可传给 Luban manager 构造函数的 Loader。</returns>")
                .AppendLine("        private static object CreateEditorJsonLoader(Type jsonNodeType)")
                .AppendLine("        {")
                .AppendLine("            MethodInfo factoryMethod = typeof(TableKit).GetMethod(")
                .AppendLine("                nameof(CreateEditorJsonLoaderGeneric),")
                .AppendLine("                BindingFlags.NonPublic | BindingFlags.Static)")
                .AppendLine("                ?? throw new InvalidOperationException(\"TableKit 无法创建 Editor JSON Loader。\");")
                .AppendLine("            return factoryMethod.MakeGenericMethod(jsonNodeType).Invoke(null, null);")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>创建返回指定 JSON 节点类型的 Loader 委托。</summary>")
                .AppendLine("        /// <typeparam name=\"TJson\">Luban manager 需要的 JSON 节点类型。</typeparam>")
                .AppendLine("        /// <returns>强类型 JSON Loader。</returns>")
                .AppendLine("        private static Func<string, TJson> CreateEditorJsonLoaderGeneric<TJson>()")
                .AppendLine("        {")
                .AppendLine("            return LoadEditorJson<TJson>;")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>读取并转换一个 JSON 表文件。</summary>")
                .AppendLine("        /// <typeparam name=\"TJson\">目标 JSON 节点类型。</typeparam>")
                .AppendLine("        /// <param name=\"fileName\">Luban 表文件名。</param>")
                .AppendLine("        /// <returns>解析后的 JSON 节点。</returns>")
                .AppendLine("        private static TJson LoadEditorJson<TJson>(string fileName)")
                .AppendLine("        {")
                .AppendLine("            return (TJson)LoadEditorJsonDynamic(fileName);")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>通过当前 Luban JSON Runtime 解析一个 Editor 表文件。</summary>")
                .AppendLine("        /// <param name=\"fileName\">Luban 表文件名。</param>")
                .AppendLine("        /// <returns>解析器返回的节点对象。</returns>")
                .AppendLine("        private static object LoadEditorJsonDynamic(string fileName)")
                .AppendLine("        {")
                .AppendLine("            string json = ReadEditorText(BuildEditorDataFilePath(fileName, \".json\"));")
                .AppendLine("            return ResolveEditorJsonParseMethod().Invoke(null, new object[] { json });")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 Luban JSON Parse 方法的发现和缓存逻辑。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendEditorJsonParser(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>发现并缓存 Luban SimpleJson 的静态 Parse 方法。</summary>")
                .AppendLine("        /// <returns>接收字符串的静态 Parse 方法。</returns>")
                .AppendLine("        private static MethodInfo ResolveEditorJsonParseMethod()")
                .AppendLine("        {")
                .AppendLine("            if (sEditorJsonParseMethod != null) return sEditorJsonParseMethod;")
                .AppendLine("            string[] typeNames = { \"Luban.SimpleJson.JSON\", \"SimpleJSON.JSON\" };")
                .AppendLine("            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();")
                .AppendLine("            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)")
                .AppendLine("            {")
                .AppendLine("                if (assemblies[assemblyIndex].IsDynamic) continue;")
                .AppendLine("                for (int typeIndex = 0; typeIndex < typeNames.Length; typeIndex++)")
                .AppendLine("                {")
                .AppendLine("                    Type jsonType = assemblies[assemblyIndex].GetType(typeNames[typeIndex], false);")
                .AppendLine("                    if (jsonType == null) continue;")
                .AppendLine("                    sEditorJsonParseMethod = jsonType.GetMethod(")
                .AppendLine("                        \"Parse\",")
                .AppendLine("                        BindingFlags.Public | BindingFlags.Static,")
                .AppendLine("                        null,")
                .AppendLine("                        new[] { typeof(string) },")
                .AppendLine("                        null);")
                .AppendLine("                    if (sEditorJsonParseMethod != null) return sEditorJsonParseMethod;")
                .AppendLine("                }")
                .AppendLine("            }")
                .AppendLine("            throw new InvalidOperationException(\"TableKit 无法找到 Luban JSON Parse 方法。\");")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 Editor 二进制读取和数据路径规范化逻辑。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendEditorDataPathApi(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>读取一个二进制 Editor 表文件并包装为 Luban ByteBuf。</summary>")
                .AppendLine("        /// <param name=\"fileName\">Luban 表文件名。</param>")
                .AppendLine("        /// <returns>包含表文件字节的 ByteBuf。</returns>")
                .AppendLine("        private static global::Luban.ByteBuf LoadBinaryEditor(string fileName)")
                .AppendLine("        {")
                .AppendLine("            return new global::Luban.ByteBuf(ReadEditorBytes(BuildEditorDataFilePath(fileName, \".bytes\")));")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>组合规范化 Editor 数据目录、表名和扩展名。</summary>")
                .AppendLine("        /// <param name=\"fileName\">Luban 表文件名。</param>")
                .AppendLine("        /// <param name=\"extension\">包含点号的数据扩展名。</param>")
                .AppendLine("        /// <returns>项目资源路径。</returns>")
                .AppendLine("        private static string BuildEditorDataFilePath(string fileName, string extension)")
                .AppendLine("        {")
                .AppendLine("            return EditorDataPath + fileName + extension;")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>规范化用户提供的 Editor 数据目录。</summary>")
                .AppendLine("        /// <param name=\"path\">待规范化目录。</param>")
                .AppendLine("        /// <returns>使用正斜杠并以斜杠结尾的目录。</returns>")
                .AppendLine("        private static string NormalizeEditorDataPath(string path)")
                .AppendLine("        {")
                .AppendLine("            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(\"TableKit Editor 数据路径不能为空。\", nameof(path));")
                .AppendLine("            return path.Replace('\\\\', '/').TrimEnd('/') + \"/\";")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 Unity AssetDatabase 与 Godot Tools 文件系统的文本和字节读取。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendEditorFileReaders(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>从 Unity AssetDatabase 或 Godot Tools 文件系统读取文本。</summary>")
                .AppendLine("        /// <param name=\"path\">项目资源路径。</param>")
                .AppendLine("        /// <returns>UTF-8 文本内容。</returns>")
                .AppendLine("        private static string ReadEditorText(string path)")
                .AppendLine("        {")
                .AppendLine("#if UNITY_EDITOR")
                .AppendLine("            UnityEngine.TextAsset asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(path);")
                .AppendLine("            if (asset == null) throw new FileNotFoundException(\"TableKit Editor 表文件不存在。\", path);")
                .AppendLine("            return asset.text;")
                .AppendLine("#else")
                .AppendLine("            return File.ReadAllText(ResolveEditorFileSystemPath(path));")
                .AppendLine("#endif")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>从 Unity AssetDatabase 或 Godot Tools 文件系统读取字节。</summary>")
                .AppendLine("        /// <param name=\"path\">项目资源路径。</param>")
                .AppendLine("        /// <returns>表文件字节。</returns>")
                .AppendLine("        private static byte[] ReadEditorBytes(string path)")
                .AppendLine("        {")
                .AppendLine("#if UNITY_EDITOR")
                .AppendLine("            UnityEngine.TextAsset asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(path);")
                .AppendLine("            if (asset == null) throw new FileNotFoundException(\"TableKit Editor 表文件不存在。\", path);")
                .AppendLine("            return asset.bytes;")
                .AppendLine("#else")
                .AppendLine("            return File.ReadAllBytes(ResolveEditorFileSystemPath(path));")
                .AppendLine("#endif")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 Godot Tools 资源路径到文件系统路径的转换。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendEditorGodotPathResolver(CodeGenLineBuilder source)
        {
            source.AppendLine("#if GODOT && TOOLS")
                .AppendLine("        /// <summary>把 Godot res:// 或项目相对路径转换为文件系统绝对路径。</summary>")
                .AppendLine("        /// <param name=\"path\">Godot 资源路径或普通路径。</param>")
                .AppendLine("        /// <returns>可由 System.IO 读取的绝对路径。</returns>")
                .AppendLine("        private static string ResolveEditorFileSystemPath(string path)")
                .AppendLine("        {")
                .AppendLine("            string fileSystemPath = path.StartsWith(\"res://\", StringComparison.Ordinal)")
                .AppendLine("                ? path.Substring(6)")
                .AppendLine("                : path;")
                .AppendLine("            return Path.GetFullPath(fileSystemPath);")
                .AppendLine("        }")
                .AppendLine("#endif")
                .AppendLine();
        }

        /// <summary>规范化生成期 Editor 默认路径，避免模板依赖调用方尾斜杠。</summary>
        /// <param name="path">Workbench 草稿中的 Editor 数据目录。</param>
        /// <returns>使用正斜杠并以斜杠结尾的目录。</returns>
        private static string NormalizeDefaultEditorDataPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("TableKit Editor 数据路径不能为空。", nameof(path));
            }
            return path.Replace('\\', '/').TrimEnd('/') + "/";
        }

        /// <summary>把路径转义为可安全嵌入生成源码的 C# 字符串内容。</summary>
        /// <param name="value">待转义文本。</param>
        /// <returns>不包含外层引号的 C# 字符串内容。</returns>
        private static string EscapeCSharpString(string value)
        {
            return value.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
