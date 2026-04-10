#if UNITY_EDITOR
using System.Text;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// Async template-generation helpers for TableKit code generation.
    /// </summary>
    public static partial class TableKitCodeGenerator
    {
        private static void AppendAsyncLoadingSection(StringBuilder sb, string tablesNamespace, bool hasYokiFrame,
            string[] tableFileNames)
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
            AppendDefaultAsyncLoaders(sb, hasYokiFrame);

            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("#endif");
        }

        private static void AppendTableFileNames(StringBuilder sb, string[] tableFileNames)
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

        private static void AppendAsyncLoaderSetters(StringBuilder sb)
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

        private static void AppendInitAsync(StringBuilder sb, string tablesNamespace)
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
            sb.AppendLine("                        Debug.LogError($\"[TableKit] 寮傛缂撳瓨鏈懡涓? {name}\");");
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
            sb.AppendLine("                    Debug.LogError($\"[TableKit] 寮傛缂撳瓨鏈懡涓? {name}\");");
            sb.AppendLine("                    return null;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                if (sJsonParseMethod == null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var jsonType = AppDomain.CurrentDomain.GetAssemblies()");
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

        private static void AppendAsyncCacheHelpers(StringBuilder sb)
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

        private static void AppendDefaultAsyncLoaders(StringBuilder sb, bool hasYokiFrame)
        {
            sb.AppendLine();
            if (hasYokiFrame)
            {
                sb.AppendLine("    // 榛樿寮傛鍔犺浇鍣細浣跨敤 YokiFrame.ResKit");
                sb.AppendLine("    private static async UniTask<byte[]> DefaultAsyncBinaryLoader(string fileName, CancellationToken ct)");
                sb.AppendLine("    {");
                sb.AppendLine("        var path = string.Format(RuntimePathPattern, fileName);");
                sb.AppendLine("        var handler = await YokiFrame.ResKit.LoadAssetUniTaskAsync<TextAsset>(path, ct);");
                sb.AppendLine("        if (handler == default)");
                sb.AppendLine("        {");
                sb.AppendLine("            Debug.LogError($\"[TableKit] ResKit 寮傛鍔犺浇澶辫触: {path}\");");
                sb.AppendLine("            return null;");
                sb.AppendLine("        }");
                sb.AppendLine("        var textAsset = handler.Asset as TextAsset;");
                sb.AppendLine("        var bytes = textAsset != null ? textAsset.bytes : null;");
                sb.AppendLine("        handler.Release();");
                sb.AppendLine("        return bytes;");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    private static async UniTask<string> DefaultAsyncJsonLoader(string fileName, CancellationToken ct)");
                sb.AppendLine("    {");
                sb.AppendLine("        var path = string.Format(RuntimePathPattern, fileName);");
                sb.AppendLine("        var handler = await YokiFrame.ResKit.LoadAssetUniTaskAsync<TextAsset>(path, ct);");
                sb.AppendLine("        if (handler == default)");
                sb.AppendLine("        {");
                sb.AppendLine("            Debug.LogError($\"[TableKit] ResKit 寮傛鍔犺浇澶辫触: {path}\");");
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
                sb.AppendLine("    // 榛樿寮傛鍔犺浇鍣細浣跨敤 Resources.LoadAsync");
                sb.AppendLine("    private static async UniTask<byte[]> DefaultAsyncBinaryLoader(string fileName, CancellationToken ct)");
                sb.AppendLine("    {");
                sb.AppendLine("        var path = string.Format(RuntimePathPattern, fileName);");
                sb.AppendLine("        var request = Resources.LoadAsync<TextAsset>(path);");
                sb.AppendLine("        await request.ToUniTask(cancellationToken: ct);");
                sb.AppendLine("        var textAsset = request.asset as TextAsset;");
                sb.AppendLine("        return textAsset != null ? textAsset.bytes : null;");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    private static async UniTask<string> DefaultAsyncJsonLoader(string fileName, CancellationToken ct)");
                sb.AppendLine("    {");
                sb.AppendLine("        var path = string.Format(RuntimePathPattern, fileName);");
                sb.AppendLine("        var request = Resources.LoadAsync<TextAsset>(path);");
                sb.AppendLine("        await request.ToUniTask(cancellationToken: ct);");
                sb.AppendLine("        var textAsset = request.asset as TextAsset;");
                sb.AppendLine("        return textAsset != null ? textAsset.text : null;");
                sb.AppendLine("    }");
            }
        }

    }
}
#endif
