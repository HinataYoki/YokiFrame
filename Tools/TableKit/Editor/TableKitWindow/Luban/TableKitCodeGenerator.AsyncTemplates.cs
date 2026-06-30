#if UNITY_EDITOR

namespace YokiFrame.Unity
{
    /// <summary>
    /// Async template-generation helpers for TableKit code generation.
    /// </summary>
    public static partial class TableKitCodeGenerator
    {
        private static void AppendAsyncLoadingSection(CodeGenLineBuilder sb, string tablesNamespace, bool hasYokiFrame,
            string[] tableFileNames, bool useRawResourceLoading)
        {
            sb.AppendLine();
            sb.AppendLine("#if YOKIFRAME_UNITASK_SUPPORT");
            sb.AppendLine();
            sb.AppendLine("    #region 寮傛鍔犺浇");
            sb.AppendLine();

            // 琛ㄦ枃浠跺悕鏁扮粍
            AppendTableFileNames(sb, tableFileNames);

            // 寮傛鍔犺浇鍣ㄥ鎵?
            AppendAsyncLoaderSetters(sb);

            // InitAsync 鏂规硶
            AppendInitAsync(sb, tablesNamespace);

            // 缂撳瓨杈呭姪鏂规硶
            AppendAsyncCacheHelpers(sb);

            // 榛樿寮傛鍔犺浇鍣?
            AppendDefaultAsyncLoaders(sb, hasYokiFrame, useRawResourceLoading);

            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("#endif");
        }

        private static void AppendTableFileNames(CodeGenLineBuilder sb, string[] tableFileNames)
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Generated table file names used for async preloading.");
            sb.AppendLine("    /// </summary>");

            if (tableFileNames == null || tableFileNames.Length == 0)
            {
                sb.AppendLine("    private static string[] TABLE_FILE_NAMES = System.Array.Empty<string>();");
            }
            else
            {
                sb.Append("    private static string[] TABLE_FILE_NAMES = { ");
                for (int i = 0; i < tableFileNames.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append($"\"{tableFileNames[i]}\"");
                }
                sb.AppendLine(" };");
            }
            sb.AppendLine("    private static string[] sCustomTableFileNames;");
            sb.AppendLine();
        }

        private static void AppendAsyncLoaderSetters(CodeGenLineBuilder sb)
        {
            sb.AppendLine("    private static Func<string, CancellationToken, UniTask<byte[]>> sAsyncBinaryLoader;");
            sb.AppendLine("    private static Func<string, CancellationToken, UniTask<string>> sAsyncJsonLoader;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 璁剧疆寮傛浜岃繘鍒舵暟鎹姞杞藉櫒");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static void SetAsyncBinaryLoader(Func<string, CancellationToken, UniTask<byte[]>> loader)");
            sb.AppendLine("    {");
            sb.AppendLine("        sAsyncBinaryLoader = loader ?? throw new ArgumentNullException(nameof(loader));");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Sets the async JSON data loader.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static void SetAsyncJsonLoader(Func<string, CancellationToken, UniTask<string>> loader)");
            sb.AppendLine("    {");
            sb.AppendLine("        sAsyncJsonLoader = loader ?? throw new ArgumentNullException(nameof(loader));");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 璁剧疆琛ㄦ枃浠跺悕鍒楄〃锛堣鐩栫敓鎴愭椂宓屽叆鐨勯粯璁ゅ垪琛級");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static void SetTableFileNames(string[] fileNames)");
            sb.AppendLine("    {");
            sb.AppendLine("        sCustomTableFileNames = fileNames ?? throw new ArgumentNullException(nameof(fileNames));");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void AppendInitAsync(CodeGenLineBuilder sb, string tablesNamespace)
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Initializes tables asynchronously using a preload-then-build strategy.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static async UniTask InitAsync(CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (Initialized) return;");
            sb.AppendLine();
            sb.AppendLine("        if (sAsyncBinaryLoader == null) sAsyncBinaryLoader = DefaultAsyncBinaryLoader;");
            sb.AppendLine("        if (sAsyncJsonLoader == null) sAsyncJsonLoader = DefaultAsyncJsonLoader;");
            sb.AppendLine();
            sb.AppendLine($"        var tablesCtor = typeof({tablesNamespace}.Tables).GetConstructors()[0];");
            sb.AppendLine("        var loaderReturnType = tablesCtor.GetParameters()[0].ParameterType.GetGenericArguments()[1];");
            sb.AppendLine("        var isBinary = loaderReturnType == typeof(ByteBuf);");
            sb.AppendLine();
            sb.AppendLine("        var fileNames = sCustomTableFileNames ?? TABLE_FILE_NAMES;");
            sb.AppendLine();
            sb.AppendLine("        // Phase 1: asynchronously preload all table payloads into cache.");
            sb.AppendLine("        if (isBinary)");
            sb.AppendLine("        {");
            sb.AppendLine("            var cache = new Dictionary<string, byte[]>(fileNames.Length);");
            sb.AppendLine("            var tasks = new UniTask[fileNames.Length];");
            sb.AppendLine("            for (int i = 0; i < fileNames.Length; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                var fileName = fileNames[i];");
            sb.AppendLine("                tasks[i] = LoadAndCacheBinaryAsync(fileName, cache, cancellationToken);");
            sb.AppendLine("            }");
            sb.AppendLine("            await UniTask.WhenAll(tasks);");
            sb.AppendLine();
            sb.AppendLine("            // Phase 2: 鐢ㄧ紦瀛樻暟鎹悓姝ユ瀯閫?Tables");
            sb.AppendLine($"            sTables = ({tablesNamespace}.Tables)tablesCtor.Invoke(new object[]");
            sb.AppendLine("            {");
            sb.AppendLine("                new Func<string, ByteBuf>(name =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (!cache.TryGetValue(name, out var bytes) || bytes == null)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        YokiFrame.LogKit.Error($\"[TableKit] 异步缓存未命中: {name}\");");
            sb.AppendLine("                        return null;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    return new ByteBuf(bytes);");
            sb.AppendLine("                })");
            sb.AppendLine("            });");
            sb.AppendLine("        }");
            sb.AppendLine("        else");
            sb.AppendLine("        {");
            sb.AppendLine("            var cache = new Dictionary<string, string>(fileNames.Length);");
            sb.AppendLine("            var tasks = new UniTask[fileNames.Length];");
            sb.AppendLine("            for (int i = 0; i < fileNames.Length; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                var fileName = fileNames[i];");
            sb.AppendLine("                tasks[i] = LoadAndCacheJsonAsync(fileName, cache, cancellationToken);");
            sb.AppendLine("            }");
            sb.AppendLine("            await UniTask.WhenAll(tasks);");
            sb.AppendLine();
            sb.AppendLine("            // Build the dynamic JSON loader delegate.");
            sb.AppendLine("            var funcType = typeof(Func<,>).MakeGenericType(typeof(string), loaderReturnType);");
            sb.AppendLine("            var delegateMethod = new Func<string, object>(name =>");
            sb.AppendLine("            {");
            sb.AppendLine("                if (!cache.TryGetValue(name, out var json) || string.IsNullOrEmpty(json))");
            sb.AppendLine("                {");
            sb.AppendLine("                    YokiFrame.LogKit.Error($\"[TableKit] 异步缓存未命中: {name}\");");
            sb.AppendLine("                    return null;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                if (sJsonParseMethod == null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var jsonType = GetLoadedAssemblies()");
            sb.AppendLine("                        .Where(a => !a.IsDynamic)");
            sb.AppendLine("                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })");
            sb.AppendLine("                        .FirstOrDefault(t => t.FullName == \"Luban.SimpleJson.JSON\" || t.FullName == \"SimpleJSON.JSON\");");
            sb.AppendLine();
            sb.AppendLine("                    if (jsonType == null)");
            sb.AppendLine("                        throw new InvalidOperationException(\"鏃犳硶鎵惧埌 JSON 绫诲瀷\");");
            sb.AppendLine();
            sb.AppendLine("                    sJsonParseMethod = jsonType.GetMethod(\"Parse\", BindingFlags.Public | BindingFlags.Static,");
            sb.AppendLine("                        null, new[] { typeof(string) }, null);");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                return sJsonParseMethod.Invoke(null, new object[] { json });");
            sb.AppendLine("            });");
            sb.AppendLine();
            sb.AppendLine("            var loaderDelegate = Delegate.CreateDelegate(funcType, delegateMethod.Target, delegateMethod.Method);");
            sb.AppendLine($"            sTables = ({tablesNamespace}.Tables)tablesCtor.Invoke(new object[] {{ loaderDelegate }});");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        Initialized = true;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void AppendAsyncCacheHelpers(CodeGenLineBuilder sb)
        {
            sb.AppendLine("    private static async UniTask LoadAndCacheBinaryAsync(");
            sb.AppendLine("        string fileName, Dictionary<string, byte[]> cache, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        var bytes = await sAsyncBinaryLoader(fileName, ct);");
            sb.AppendLine("        cache[fileName] = bytes;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static async UniTask LoadAndCacheJsonAsync(");
            sb.AppendLine("        string fileName, Dictionary<string, string> cache, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        var json = await sAsyncJsonLoader(fileName, ct);");
            sb.AppendLine("        cache[fileName] = json;");
            sb.AppendLine("    }");
        }

        private static void AppendDefaultAsyncLoaders(CodeGenLineBuilder sb, bool hasYokiFrame, bool useRawResourceLoading)
        {
            sb.AppendLine();
            sb.AppendLine("    // 默认异步加载器：统一委托给 ResKit，具体 Unity/Godot/自定义后端由 ResKit Provider 决定。");
            sb.AppendLine("    private static async UniTask<byte[]> DefaultAsyncBinaryLoader(string fileName, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        var path = string.Format(RuntimePathPattern, fileName);");
            if (useRawResourceLoading)
            {
                sb.AppendLine("        return await YokiFrame.ResKit.LoadRawAsync(path, ct);");
            }
            else
            {
                sb.AppendLine("        var asset = await YokiFrame.ResKit.LoadAsync<UnityEngine.TextAsset>(path, ct);");
                sb.AppendLine("        if (asset == null) return null;");
                sb.AppendLine("        try");
                sb.AppendLine("        {");
                sb.AppendLine("            return asset.bytes;");
                sb.AppendLine("        }");
                sb.AppendLine("        finally");
                sb.AppendLine("        {");
                sb.AppendLine("            YokiFrame.ResKit.Release(asset);");
                sb.AppendLine("        }");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static async UniTask<string> DefaultAsyncJsonLoader(string fileName, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        var path = string.Format(RuntimePathPattern, fileName);");
            if (useRawResourceLoading)
            {
                sb.AppendLine("        return await YokiFrame.ResKit.LoadRawTextAsync(path, ct);");
            }
            else
            {
                sb.AppendLine("        var asset = await YokiFrame.ResKit.LoadAsync<UnityEngine.TextAsset>(path, ct);");
                sb.AppendLine("        if (asset == null) return null;");
                sb.AppendLine("        try");
                sb.AppendLine("        {");
                sb.AppendLine("            return asset.text;");
                sb.AppendLine("        }");
                sb.AppendLine("        finally");
                sb.AppendLine("        {");
                sb.AppendLine("            YokiFrame.ResKit.Release(asset);");
                sb.AppendLine("        }");
            }
            sb.AppendLine("    }");
        }

    }
}
#endif
