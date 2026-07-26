using System;

namespace YokiFrame
{
    public static partial class TableKitSourceCodeGenerator
    {
        /// <summary>写入通过 ResKit 同步或异步读取数据的默认 TableKit Loader。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultResKitLoaderSource(CodeGenLineBuilder source)
        {
            AppendDefaultLoaderDeclaration(source);
            AppendDefaultLoaderSynchronousApi(source);
            AppendDefaultLoaderAsynchronousApi(source);
            AppendDefaultLoaderManagerFactory(source);
            AppendDefaultLoaderJsonFactory(source);
            AppendDefaultLoaderJsonParser(source);
            AppendDefaultLoaderSystemTextJsonParser(source);
            AppendDefaultLoaderSynchronousResources(source);
            AppendDefaultLoaderAsynchronousResources(source);
            AppendDefaultLoaderAssetResources(source);
            AppendDefaultLoaderAsynchronousAssetResources(source);
            AppendDefaultLoaderValidation(source);
            source.AppendLine("    }");
        }

        /// <summary>写入默认 Loader 类型、表名快照和构造函数。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderDeclaration(CodeGenLineBuilder source)
        {
            source.AppendLine("    /// <summary>使用当前 ResKit Provider 创建 Luban 表管理器的默认 Loader。</summary>")
                .AppendLine("    internal sealed class ResKitTableDataLoader : ITableDataLoader")
                .AppendLine("    {")
                .AppendLine("        private readonly string[] mTableNames;")
                .AppendLine()
                .AppendLine("        /// <summary>保存 manager 实际请求的表资源名，供异步入口先完成数据预加载。</summary>")
                .AppendLine("        /// <param name=\"tableNames\">从 Luban manager 生成源码提取的资源名。</param>")
                .AppendLine("        public ResKitTableDataLoader(IReadOnlyList<string> tableNames)")
                .AppendLine("        {")
                .AppendLine("            if (tableNames == null) throw new ArgumentNullException(nameof(tableNames));")
                .AppendLine("            mTableNames = new string[tableNames.Count];")
                .AppendLine("            for (int index = 0; index < tableNames.Count; index++)")
                .AppendLine("            {")
                .AppendLine("                string tableName = tableNames[index];")
                .AppendLine("                if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException(\"TableKit 表资源名不能为空。\", nameof(tableNames));")
                .AppendLine("                mTableNames[index] = tableName;")
                .AppendLine("            }")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入默认 Loader 的同步 manager 创建入口。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderSynchronousApi(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>通过 ResKit 同步读取 manager 请求的每个表资源。</summary>")
                .AppendLine("        /// <typeparam name=\"TTables\">当前 Luban manager 类型。</typeparam>")
                .AppendLine("        /// <param name=\"resourcePathPattern\">包含 `{0}` 表名占位符的资源路径。</param>")
                .AppendLine("        /// <param name=\"resourceLoadMode\">普通资源对象或 Raw 读取模式。</param>")
                .AppendLine("        /// <returns>完成解析的强类型表管理器。</returns>")
                .AppendLine("        public TTables Load<TTables>(string resourcePathPattern, TableDataResourceLoadMode resourceLoadMode) where TTables : class")
                .AppendLine("        {")
                .AppendLine("            ValidatePathPattern(resourcePathPattern);")
                .AppendLine("            ConstructorInfo constructor = FindTablesConstructor(typeof(TTables));")
                .AppendLine("            if (GetLoaderReturnType(constructor) == typeof(global::Luban.ByteBuf))")
                .AppendLine("            {")
                .AppendLine("                return CreateTables<TTables>(constructor, fileName =>")
                .AppendLine("                    LoadBytes(BuildResourcePath(resourcePathPattern, fileName), resourceLoadMode), null);")
                .AppendLine("            }")
                .AppendLine("            return CreateTables<TTables>(constructor, null, fileName =>")
                .AppendLine("                LoadText(BuildResourcePath(resourcePathPattern, fileName), resourceLoadMode));")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 Task/UniTask 异步预加载入口。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderAsynchronousApi(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>先异步读取全部表资源，再同步交给 Luban manager 完成解析。</summary>")
                .AppendLine("        /// <typeparam name=\"TTables\">当前 Luban manager 类型。</typeparam>")
                .AppendLine("        /// <param name=\"resourcePathPattern\">包含 `{0}` 表名占位符的资源路径。</param>")
                .AppendLine("        /// <param name=\"resourceLoadMode\">普通资源对象或 Raw 读取模式。</param>")
                .AppendLine("        /// <returns>异步返回完成解析的强类型表管理器。</returns>")
                .AppendLine("#if YOKIFRAME_UNITASK_SUPPORT")
                .AppendLine("        public async UniTask<TTables> LoadAsync<TTables>(string resourcePathPattern, TableDataResourceLoadMode resourceLoadMode) where TTables : class")
                .AppendLine("#else")
                .AppendLine("        public async Task<TTables> LoadAsync<TTables>(string resourcePathPattern, TableDataResourceLoadMode resourceLoadMode) where TTables : class")
                .AppendLine("#endif")
                .AppendLine("        {")
                .AppendLine("            ValidatePathPattern(resourcePathPattern);")
                .AppendLine("            ConstructorInfo constructor = FindTablesConstructor(typeof(TTables));")
                .AppendLine("            if (GetLoaderReturnType(constructor) == typeof(global::Luban.ByteBuf))")
                .AppendLine("            {")
                .AppendLine("                return await LoadBinaryTablesAsync<TTables>(constructor, resourcePathPattern, resourceLoadMode);")
                .AppendLine("            }")
                .AppendLine("            return await LoadJsonTablesAsync<TTables>(constructor, resourcePathPattern, resourceLoadMode);")
                .AppendLine("        }")
                .AppendLine();
            AppendDefaultLoaderBinaryPreload(source);
            AppendDefaultLoaderJsonPreload(source);
        }

        /// <summary>写入二进制表的真实异步预加载实现。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderBinaryPreload(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>异步预加载全部二进制表，再从内存字典构造 manager。</summary>")
                .AppendLine("#if YOKIFRAME_UNITASK_SUPPORT")
                .AppendLine("        private async UniTask<TTables> LoadBinaryTablesAsync<TTables>(ConstructorInfo constructor, string pathPattern, TableDataResourceLoadMode loadMode) where TTables : class")
                .AppendLine("#else")
                .AppendLine("        private async Task<TTables> LoadBinaryTablesAsync<TTables>(ConstructorInfo constructor, string pathPattern, TableDataResourceLoadMode loadMode) where TTables : class")
                .AppendLine("#endif")
                .AppendLine("        {")
                .AppendLine("            Dictionary<string, byte[]> tables = new(StringComparer.Ordinal);")
                .AppendLine("            for (int index = 0; index < mTableNames.Length; index++)")
                .AppendLine("            {")
                .AppendLine("                string tableName = mTableNames[index];")
                .AppendLine("                string path = BuildResourcePath(pathPattern, tableName);")
                .AppendLine("                tables.Add(tableName, await LoadBytesAsync(path, loadMode));")
                .AppendLine("            }")
                .AppendLine("            return CreateTables<TTables>(constructor, fileName => GetPreloadedBytes(tables, fileName), null);")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 JSON 表的真实异步预加载实现。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderJsonPreload(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>异步预加载全部 JSON 表，再从内存字典构造 manager。</summary>")
                .AppendLine("#if YOKIFRAME_UNITASK_SUPPORT")
                .AppendLine("        private async UniTask<TTables> LoadJsonTablesAsync<TTables>(ConstructorInfo constructor, string pathPattern, TableDataResourceLoadMode loadMode) where TTables : class")
                .AppendLine("#else")
                .AppendLine("        private async Task<TTables> LoadJsonTablesAsync<TTables>(ConstructorInfo constructor, string pathPattern, TableDataResourceLoadMode loadMode) where TTables : class")
                .AppendLine("#endif")
                .AppendLine("        {")
                .AppendLine("            Dictionary<string, string> tables = new(StringComparer.Ordinal);")
                .AppendLine("            for (int index = 0; index < mTableNames.Length; index++)")
                .AppendLine("            {")
                .AppendLine("                string tableName = mTableNames[index];")
                .AppendLine("                string path = BuildResourcePath(pathPattern, tableName);")
                .AppendLine("                tables.Add(tableName, await LoadTextAsync(path, loadMode));")
                .AppendLine("            }")
                .AppendLine("            return CreateTables<TTables>(constructor, null, fileName => GetPreloadedText(tables, fileName));")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 Luban manager 构造函数发现和委托适配逻辑。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderManagerFactory(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>使用二进制或 JSON Loader 委托调用当前 manager 构造函数。</summary>")
                .AppendLine("        private static TTables CreateTables<TTables>(ConstructorInfo constructor, Func<string, byte[]> bytesLoader, Func<string, string> textLoader) where TTables : class")
                .AppendLine("        {")
                .AppendLine("            Type returnType = GetLoaderReturnType(constructor);")
                .AppendLine("            object loader = returnType == typeof(global::Luban.ByteBuf)")
                .AppendLine("                ? new Func<string, global::Luban.ByteBuf>(fileName => CreateByteBuf(fileName, bytesLoader))")
                .AppendLine("                : CreateJsonLoader(returnType, textLoader);")
                .AppendLine("            object tables = constructor.Invoke(new[] { loader });")
                .AppendLine("            return tables as TTables ?? throw new InvalidOperationException(\"TableKit 默认 Loader 创建了不匹配的表管理器类型。\");")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>查找唯一接受 `Func&lt;string, T&gt;` 的 Luban manager 构造函数。</summary>")
                .AppendLine("        private static ConstructorInfo FindTablesConstructor(Type tablesType)")
                .AppendLine("        {")
                .AppendLine("            ConstructorInfo[] constructors = tablesType.GetConstructors();")
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
                .AppendLine()
                .AppendLine("        /// <summary>取得 manager Loader 委托声明的资源返回类型。</summary>")
                .AppendLine("        private static Type GetLoaderReturnType(ConstructorInfo constructor)")
                .AppendLine("        {")
                .AppendLine("            return constructor.GetParameters()[0].ParameterType.GetGenericArguments()[1];")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入二进制与 JSON Loader 委托的强类型创建逻辑。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderJsonFactory(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>把读取到的二进制表包装为 Luban ByteBuf。</summary>")
                .AppendLine("        private static global::Luban.ByteBuf CreateByteBuf(string fileName, Func<string, byte[]> loader)")
                .AppendLine("        {")
                .AppendLine("            if (loader == null) throw new InvalidOperationException(\"TableKit 二进制 Loader 未准备完成。\");")
                .AppendLine("            byte[] bytes = loader(fileName);")
                .AppendLine("            if (bytes == null) throw new FileNotFoundException(\"TableKit 表资源不存在。\", fileName);")
                .AppendLine("            return new global::Luban.ByteBuf(bytes);")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>按 manager 声明的节点类型创建 JSON Loader 委托。</summary>")
                .AppendLine("        private static object CreateJsonLoader(Type jsonNodeType, Func<string, string> textLoader)")
                .AppendLine("        {")
                .AppendLine("            if (textLoader == null) throw new InvalidOperationException(\"TableKit JSON Loader 未准备完成。\");")
                .AppendLine("            MethodInfo factory = typeof(ResKitTableDataLoader).GetMethod(nameof(CreateJsonLoaderGeneric), BindingFlags.NonPublic | BindingFlags.Static)")
                .AppendLine("                ?? throw new InvalidOperationException(\"TableKit 无法创建 JSON Loader。\");")
                .AppendLine("            return factory.MakeGenericMethod(jsonNodeType).Invoke(null, new object[] { textLoader });")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>创建返回指定 JSON 节点类型的强类型委托。</summary>")
                .AppendLine("        private static Func<string, TJson> CreateJsonLoaderGeneric<TJson>(Func<string, string> textLoader)")
                .AppendLine("        {")
                .AppendLine("            return fileName => (TJson)ParseJson(typeof(TJson), RequireText(textLoader(fileName), fileName));")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 SimpleJSON、Newtonsoft 等静态 Parse 入口的发现逻辑。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderJsonParser(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>按 Luban code target 的节点类型解析 JSON 文本。</summary>")
                .AppendLine("        private static object ParseJson(Type jsonNodeType, string json)")
                .AppendLine("        {")
                .AppendLine("            MethodInfo parse = FindStringParseMethod(jsonNodeType, jsonNodeType);")
                .AppendLine("            if (parse != null) return parse.Invoke(null, new object[] { json });")
                .AppendLine("            object systemTextJson = TryParseSystemTextJsonElement(jsonNodeType, json);")
                .AppendLine("            if (systemTextJson != null) return systemTextJson;")
                .AppendLine("            string[] parserTypes = { \"Luban.SimpleJson.JSON\", \"SimpleJSON.JSON\" };")
                .AppendLine("            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();")
                .AppendLine("            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)")
                .AppendLine("            {")
                .AppendLine("                if (assemblies[assemblyIndex].IsDynamic) continue;")
                .AppendLine("                for (int typeIndex = 0; typeIndex < parserTypes.Length; typeIndex++)")
                .AppendLine("                {")
                .AppendLine("                    Type parserType = assemblies[assemblyIndex].GetType(parserTypes[typeIndex], false);")
                .AppendLine("                    MethodInfo candidate = FindStringParseMethod(parserType, jsonNodeType);")
                .AppendLine("                    if (candidate != null) return candidate.Invoke(null, new object[] { json });")
                .AppendLine("                }")
                .AppendLine("            }")
                .AppendLine("            throw new InvalidOperationException(\"TableKit 无法找到当前 Luban JSON code target 的 Parse 方法。\");")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>查找接收单个字符串且返回目标节点类型的静态 Parse 方法。</summary>")
                .AppendLine("        private static MethodInfo FindStringParseMethod(Type parserType, Type resultType)")
                .AppendLine("        {")
                .AppendLine("            if (parserType == null) return null;")
                .AppendLine("            MethodInfo method = parserType.GetMethod(\"Parse\", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);")
                .AppendLine("            return method != null && resultType.IsAssignableFrom(method.ReturnType) ? method : null;")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 System.Text.Json JsonElement 的反射解析适配。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderSystemTextJsonParser(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>在不强制依赖 System.Text.Json 的情况下解析 JsonElement。</summary>")
                .AppendLine("        private static object TryParseSystemTextJsonElement(Type jsonNodeType, string json)")
                .AppendLine("        {")
                .AppendLine("            if (!string.Equals(jsonNodeType.FullName, \"System.Text.Json.JsonElement\", StringComparison.Ordinal)) return null;")
                .AppendLine("            Type documentType = jsonNodeType.Assembly.GetType(\"System.Text.Json.JsonDocument\", true);")
                .AppendLine("            MethodInfo parse = FindJsonDocumentParseMethod(documentType);")
                .AppendLine("            ParameterInfo[] parameters = parse.GetParameters();")
                .AppendLine("            object[] arguments = new object[parameters.Length];")
                .AppendLine("            arguments[0] = json;")
                .AppendLine("            for (int index = 1; index < parameters.Length; index++)")
                .AppendLine("            {")
                .AppendLine("                arguments[index] = parameters[index].HasDefaultValue")
                .AppendLine("                    ? parameters[index].DefaultValue")
                .AppendLine("                    : Activator.CreateInstance(parameters[index].ParameterType);")
                .AppendLine("            }")
                .AppendLine("            object document = parse.Invoke(null, arguments);")
                .AppendLine("            try")
                .AppendLine("            {")
                .AppendLine("                object root = documentType.GetProperty(\"RootElement\").GetValue(document, null);")
                .AppendLine("                MethodInfo clone = jsonNodeType.GetMethod(\"Clone\", Type.EmptyTypes);")
                .AppendLine("                return clone != null ? clone.Invoke(root, null) : root;")
                .AppendLine("            }")
                .AppendLine("            finally")
                .AppendLine("            {")
                .AppendLine("                IDisposable disposable = document as IDisposable;")
                .AppendLine("                if (disposable != null) disposable.Dispose();")
                .AppendLine("            }")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>选择首个以字符串为输入的 JsonDocument.Parse 重载。</summary>")
                .AppendLine("        private static MethodInfo FindJsonDocumentParseMethod(Type documentType)")
                .AppendLine("        {")
                .AppendLine("            MethodInfo[] methods = documentType.GetMethods(BindingFlags.Public | BindingFlags.Static);")
                .AppendLine("            for (int index = 0; index < methods.Length; index++)")
                .AppendLine("            {")
                .AppendLine("                ParameterInfo[] parameters = methods[index].GetParameters();")
                .AppendLine("                if (methods[index].Name == \"Parse\" && parameters.Length > 0 && parameters[0].ParameterType == typeof(string)) return methods[index];")
                .AppendLine("            }")
                .AppendLine("            throw new InvalidOperationException(\"TableKit 无法找到 JsonDocument.Parse(string) 重载。\");")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 Raw 与普通资源对象的同步选择逻辑。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderSynchronousResources(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>按资源模式同步读取二进制表。</summary>")
                .AppendLine("        private static byte[] LoadBytes(string path, TableDataResourceLoadMode loadMode)")
                .AppendLine("        {")
                .AppendLine("            byte[] bytes = loadMode == TableDataResourceLoadMode.Raw")
                .AppendLine("                ? ResKit.LoadRaw(path)")
                .AppendLine("                : LoadAssetBytes(path);")
                .AppendLine("            return RequireBytes(bytes, path);")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>按资源模式同步读取 JSON 表文本。</summary>")
                .AppendLine("        private static string LoadText(string path, TableDataResourceLoadMode loadMode)")
                .AppendLine("        {")
                .AppendLine("            string text = loadMode == TableDataResourceLoadMode.Raw")
                .AppendLine("                ? ResKit.LoadRawText(path)")
                .AppendLine("                : LoadAssetText(path);")
                .AppendLine("            return RequireText(text, path);")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 Raw 与普通资源对象的 Task/UniTask 选择逻辑。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderAsynchronousResources(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>按资源模式异步读取二进制表。</summary>")
                .AppendLine("#if YOKIFRAME_UNITASK_SUPPORT")
                .AppendLine("        private static async UniTask<byte[]> LoadBytesAsync(string path, TableDataResourceLoadMode loadMode)")
                .AppendLine("#else")
                .AppendLine("        private static async Task<byte[]> LoadBytesAsync(string path, TableDataResourceLoadMode loadMode)")
                .AppendLine("#endif")
                .AppendLine("        {")
                .AppendLine("            byte[] bytes = loadMode == TableDataResourceLoadMode.Raw")
                .AppendLine("                ? await ResKit.LoadRawAsync(path)")
                .AppendLine("                : await LoadAssetBytesAsync(path);")
                .AppendLine("            return RequireBytes(bytes, path);")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>按资源模式异步读取 JSON 表文本。</summary>")
                .AppendLine("#if YOKIFRAME_UNITASK_SUPPORT")
                .AppendLine("        private static async UniTask<string> LoadTextAsync(string path, TableDataResourceLoadMode loadMode)")
                .AppendLine("#else")
                .AppendLine("        private static async Task<string> LoadTextAsync(string path, TableDataResourceLoadMode loadMode)")
                .AppendLine("#endif")
                .AppendLine("        {")
                .AppendLine("            string text = loadMode == TableDataResourceLoadMode.Raw")
                .AppendLine("                ? await ResKit.LoadRawTextAsync(path)")
                .AppendLine("                : await LoadAssetTextAsync(path);")
                .AppendLine("            return RequireText(text, path);")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 Unity 普通 TextAsset 的同步读取与释放。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderAssetResources(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>通过 ResKit 普通资源 API 同步读取 TextAsset 字节。</summary>")
                .AppendLine("        private static byte[] LoadAssetBytes(string path)")
                .AppendLine("        {")
                .AppendLine("#if UNITY_2022_3_OR_NEWER")
                .AppendLine("            UnityEngine.TextAsset asset = ResKit.Load<UnityEngine.TextAsset>(path);")
                .AppendLine("            if (asset == default) return null;")
                .AppendLine("            try { return asset.bytes; }")
                .AppendLine("            finally { ResKit.Release(asset); }")
                .AppendLine("#else")
                .AppendLine("            throw CreateAssetModeNotSupportedException();")
                .AppendLine("#endif")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>通过 ResKit 普通资源 API 同步读取 TextAsset 文本。</summary>")
                .AppendLine("        private static string LoadAssetText(string path)")
                .AppendLine("        {")
                .AppendLine("#if UNITY_2022_3_OR_NEWER")
                .AppendLine("            UnityEngine.TextAsset asset = ResKit.Load<UnityEngine.TextAsset>(path);")
                .AppendLine("            if (asset == default) return null;")
                .AppendLine("            try { return asset.text; }")
                .AppendLine("            finally { ResKit.Release(asset); }")
                .AppendLine("#else")
                .AppendLine("            throw CreateAssetModeNotSupportedException();")
                .AppendLine("#endif")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入 Unity 普通 TextAsset 的异步读取与释放。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderAsynchronousAssetResources(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>通过 ResKit 普通资源 API 异步读取 TextAsset 字节。</summary>")
                .AppendLine("#if YOKIFRAME_UNITASK_SUPPORT")
                .AppendLine("        private static async UniTask<byte[]> LoadAssetBytesAsync(string path)")
                .AppendLine("#else")
                .AppendLine("        private static async Task<byte[]> LoadAssetBytesAsync(string path)")
                .AppendLine("#endif")
                .AppendLine("        {")
                .AppendLine("#if UNITY_2022_3_OR_NEWER")
                .AppendLine("            UnityEngine.TextAsset asset = await ResKit.LoadAsync<UnityEngine.TextAsset>(path);")
                .AppendLine("            if (asset == default) return null;")
                .AppendLine("            try { return asset.bytes; }")
                .AppendLine("            finally { ResKit.Release(asset); }")
                .AppendLine("#else")
                .AppendLine("            throw CreateAssetModeNotSupportedException();")
                .AppendLine("#endif")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>通过 ResKit 普通资源 API 异步读取 TextAsset 文本。</summary>")
                .AppendLine("#if YOKIFRAME_UNITASK_SUPPORT")
                .AppendLine("        private static async UniTask<string> LoadAssetTextAsync(string path)")
                .AppendLine("#else")
                .AppendLine("        private static async Task<string> LoadAssetTextAsync(string path)")
                .AppendLine("#endif")
                .AppendLine("        {")
                .AppendLine("#if UNITY_2022_3_OR_NEWER")
                .AppendLine("            UnityEngine.TextAsset asset = await ResKit.LoadAsync<UnityEngine.TextAsset>(path);")
                .AppendLine("            if (asset == default) return null;")
                .AppendLine("            try { return asset.text; }")
                .AppendLine("            finally { ResKit.Release(asset); }")
                .AppendLine("#else")
                .AppendLine("            throw CreateAssetModeNotSupportedException();")
                .AppendLine("#endif")
                .AppendLine("        }")
                .AppendLine();
        }

        /// <summary>写入路径校验、资源空值检查和异步预加载字典读取。</summary>
        /// <param name="source">目标逐行生成器。</param>
        private static void AppendDefaultLoaderValidation(CodeGenLineBuilder source)
        {
            source.AppendLine("        /// <summary>确保默认 Loader 能把 Luban 表名写入资源路径。</summary>")
                .AppendLine("        private static void ValidatePathPattern(string pathPattern)")
                .AppendLine("        {")
                .AppendLine("            if (string.IsNullOrWhiteSpace(pathPattern)) throw new ArgumentException(\"TableKit 资源路径模板不能为空。\", nameof(pathPattern));")
                .AppendLine("            if (pathPattern.IndexOf(\"{0}\", StringComparison.Ordinal) < 0) throw new ArgumentException(\"TableKit 默认 ResKit Loader 要求资源路径模板包含 `{0}`。\", nameof(pathPattern));")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>把 Luban 表资源名写入运行时路径模板。</summary>")
                .AppendLine("        private static string BuildResourcePath(string pathPattern, string tableName) => pathPattern.Replace(\"{0}\", tableName);")
                .AppendLine()
                .AppendLine("        /// <summary>拒绝资源系统返回空二进制数据。</summary>")
                .AppendLine("        private static byte[] RequireBytes(byte[] bytes, string path) => bytes ?? throw new FileNotFoundException(\"TableKit 表资源不存在。\", path);")
                .AppendLine()
                .AppendLine("        /// <summary>拒绝资源系统返回空文本数据。</summary>")
                .AppendLine("        private static string RequireText(string text, string path) => text ?? throw new FileNotFoundException(\"TableKit 表资源不存在。\", path);")
                .AppendLine()
                .AppendLine("        /// <summary>读取异步预加载的二进制表并报告生成清单漂移。</summary>")
                .AppendLine("        private static byte[] GetPreloadedBytes(Dictionary<string, byte[]> tables, string tableName)")
                .AppendLine("        {")
                .AppendLine("            if (tables.TryGetValue(tableName, out byte[] bytes)) return bytes;")
                .AppendLine("            throw new InvalidOperationException(\"TableKit 异步表清单与 Luban manager 不一致，请重新生成 TableKit。\");")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>读取异步预加载的 JSON 表并报告生成清单漂移。</summary>")
                .AppendLine("        private static string GetPreloadedText(Dictionary<string, string> tables, string tableName)")
                .AppendLine("        {")
                .AppendLine("            if (tables.TryGetValue(tableName, out string text)) return text;")
                .AppendLine("            throw new InvalidOperationException(\"TableKit 异步表清单与 Luban manager 不一致，请重新生成 TableKit。\");")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        /// <summary>创建非 Unity 宿主使用普通资源模式时的明确错误。</summary>")
                .AppendLine("        private static NotSupportedException CreateAssetModeNotSupportedException()")
                .AppendLine("        {")
                .AppendLine("            return new NotSupportedException(\"TableKit 默认 ResKit Loader 的 Asset 模式当前仅支持 Unity TextAsset；其它宿主请使用 Raw 模式或显式 SetLoader。\");")
                .AppendLine("        }");
        }
    }
}
