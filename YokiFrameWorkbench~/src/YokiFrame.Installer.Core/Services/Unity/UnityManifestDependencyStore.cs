using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame.Installer.Core.IO;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 结构化读取并原子维护 Unity manifest 中唯一的 YokiFrame 安装来源依赖。
/// </summary>
internal sealed class UnityManifestDependencyStore
{
    internal const string PACKAGE_ID = "com.hinatayoki.yokiframe";
    internal const string EMBEDDED_PACKAGE_DEPENDENCY = "file:" + PACKAGE_ID;

    /// <summary>
    /// 只读解析 manifest，并保留原文用于计划判断或来源切换失败回滚。
    /// </summary>
    /// <param name="projectRoot">目标 Unity 项目根。</param>
    /// <returns>结构化 manifest 快照。</returns>
    internal UnityManifestSnapshot Read(string projectRoot)
    {
        var manifestPath = GetManifestPath(projectRoot);
        var originalText = File.ReadAllText(manifestPath);
        JsonObject root;
        try
        {
            root = JsonNode.Parse(originalText) as JsonObject
                ?? throw new InvalidDataException("Unity Packages/manifest.json root must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Unity Packages/manifest.json is not valid JSON: " + manifestPath, exception);
        }

        var dependencies = ReadDependencies(root);
        var currentDependency = ReadYokiFrameDependency(dependencies);
        return new UnityManifestSnapshot(manifestPath, originalText, root, dependencies, currentDependency);
    }

    /// <summary>
    /// 结构化设置 embedded package 的本地 file 依赖，使 Unity 将 Packages 下的投影加入解析和编译图。
    /// </summary>
    /// <param name="projectRoot">目标 Unity 项目根。</param>
    /// <returns>manifest 实际发生变化时返回 true。</returns>
    internal bool SetEmbeddedDependency(string projectRoot)
    {
        var snapshot = Read(projectRoot);
        if (string.Equals(snapshot.CurrentDependency, EMBEDDED_PACKAGE_DEPENDENCY, StringComparison.Ordinal))
        {
            return false;
        }

        return SetDependency(snapshot, EMBEDDED_PACKAGE_DEPENDENCY);
    }

    /// <summary>
    /// 在 embedded 包程序集图发生变化时原子重写相同 manifest 内容，要求本地 file 依赖已正确登记。
    /// 普通脚本更新不应调用此方法，避免无意义地触发 Unity 对全部依赖包的重新解析。
    /// </summary>
    /// <param name="projectRoot">目标 Unity 项目根。</param>
    internal void RefreshEmbeddedPackageGraph(string projectRoot)
    {
        var snapshot = Read(projectRoot);
        if (!string.Equals(snapshot.CurrentDependency, EMBEDDED_PACKAGE_DEPENDENCY, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Unity embedded dependency must be registered before refreshing its package graph.");
        }

        WriteAtomic(snapshot.ManifestPath, snapshot.OriginalText);
    }

    /// <summary>
    /// 结构化设置 YokiFrame Git URL；值未变化时不重写文件以保持逐字节幂等。
    /// </summary>
    /// <param name="projectRoot">目标 Unity 项目根。</param>
    /// <param name="gitUrl">待写入的 Git URL。</param>
    /// <returns>manifest 实际发生变化时返回 true。</returns>
    internal bool SetGitDependency(string projectRoot, string? gitUrl)
    {
        ValidateGitUrl(gitUrl);
        return SetDependency(Read(projectRoot), gitUrl!);
    }

    /// <summary>
    /// 在来源切换后续步骤失败时原子恢复 manifest 原文，避免格式和用户配置漂移。
    /// </summary>
    /// <param name="snapshot">写入前读取的 manifest 快照。</param>
    internal void RestoreOriginal(UnityManifestSnapshot snapshot)
    {
        WriteAtomic(snapshot.ManifestPath, snapshot.OriginalText);
    }

    /// <summary>
    /// 从正式 manifest 重新读取 embedded package 依赖并精确比对，防止投影存在但未加入 Unity 解析图。
    /// </summary>
    /// <param name="projectRoot">目标 Unity 项目根。</param>
    internal void VerifyEmbeddedDependency(string projectRoot)
    {
        VerifyDependency(
            projectRoot,
            EMBEDDED_PACKAGE_DEPENDENCY,
            "Unity embedded dependency post-write verification failed for ");
    }

    /// <summary>
    /// 从正式 manifest 重新读取 Git 依赖并精确比对，防止只验证内存对象就删除来源切换备份。
    /// </summary>
    /// <param name="projectRoot">目标 Unity 项目根。</param>
    /// <param name="expectedGitUrl">本次事务预期提交的 Git URL。</param>
    internal void VerifyGitDependency(string projectRoot, string? expectedGitUrl)
    {
        ValidateGitUrl(expectedGitUrl);
        VerifyDependency(
            projectRoot,
            expectedGitUrl!,
            "Unity Git dependency post-write verification failed for ");
    }

    /// <summary>
    /// 把已读取快照中的 YokiFrame 依赖替换为指定值，并原子提交完整 manifest。
    /// </summary>
    /// <param name="snapshot">写入前读取的 manifest 快照。</param>
    /// <param name="dependency">待写入的本地 file 或 Git 依赖值。</param>
    /// <returns>manifest 实际发生变化时返回 true。</returns>
    private bool SetDependency(UnityManifestSnapshot snapshot, string dependency)
    {
        if (string.Equals(snapshot.CurrentDependency, dependency, StringComparison.Ordinal))
        {
            return false;
        }

        var dependencies = snapshot.Dependencies ?? new JsonObject();
        if (snapshot.Dependencies == null)
        {
            snapshot.Root["dependencies"] = dependencies;
        }

        dependencies[PACKAGE_ID] = dependency;
        WriteAtomic(snapshot.ManifestPath, Serialize(snapshot.Root));
        return true;
    }

    /// <summary>
    /// 从磁盘重读已持久化的依赖值，避免仅依据内存对象确认来源切换成功。
    /// </summary>
    /// <param name="projectRoot">目标 Unity 项目根。</param>
    /// <param name="expectedDependency">本次事务预期写入的依赖值。</param>
    /// <param name="messagePrefix">失败消息的来源语义前缀。</param>
    private void VerifyDependency(string projectRoot, string expectedDependency, string messagePrefix)
    {
        var persisted = Read(projectRoot);
        if (!string.Equals(persisted.CurrentDependency, expectedDependency, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                messagePrefix + PACKAGE_ID + ".");
        }
    }

    /// <summary>
    /// 验证 Git URL 是明确允许的绝对 URI，避免把路径片段或非 Git 传输协议写入 manifest。
    /// </summary>
    /// <param name="gitUrl">待验证的 Git URL。</param>
    internal static void ValidateGitUrl(string? gitUrl)
    {
        if (string.IsNullOrWhiteSpace(gitUrl)
            || !string.Equals(gitUrl, gitUrl.Trim(), StringComparison.Ordinal)
            || gitUrl.Any(char.IsControl))
        {
            throw new ArgumentException("Unity Git URL must be a non-empty value without surrounding whitespace or control characters.", nameof(gitUrl));
        }

        if (!Uri.TryCreate(gitUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Unity Git URL must be an absolute URI.", nameof(gitUrl));
        }

        var isNetworkGitUri = (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, "git", StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(uri.Host);
        var isFileGitUri = string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)
            && uri.IsFile
            && gitUrl.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
        if (!isNetworkGitUri && !isFileGitUri)
        {
            throw new ArgumentException("Unity Git URL scheme must be file, https, or git.", nameof(gitUrl));
        }
    }

    /// <summary>
    /// 读取 dependencies 对象；缺失时允许后续 Git 模式创建，类型错误时明确拒绝。
    /// </summary>
    /// <param name="root">manifest 根对象。</param>
    /// <returns>现有 dependencies 对象；缺失时返回空。</returns>
    private static JsonObject? ReadDependencies(JsonObject root)
    {
        if (!root.TryGetPropertyValue("dependencies", out var node) || node == null)
        {
            return null;
        }

        return node as JsonObject
            ?? throw new InvalidDataException("Unity Packages/manifest.json dependencies must be a JSON object.");
    }

    /// <summary>
    /// 读取当前 YokiFrame 依赖字符串，拒绝非字符串值以保持来源判断确定性。
    /// </summary>
    /// <param name="dependencies">manifest dependencies 对象。</param>
    /// <returns>当前本地 file 或 Git 依赖；依赖不存在时返回空。</returns>
    private static string? ReadYokiFrameDependency(JsonObject? dependencies)
    {
        if (dependencies == null || !dependencies.TryGetPropertyValue(PACKAGE_ID, out var node) || node == null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var gitUrl))
        {
            return gitUrl;
        }

        throw new InvalidDataException("Unity YokiFrame manifest dependency must be a JSON string.");
    }

    /// <summary>
    /// 使用 Utf8JsonWriter 序列化 JsonNode，避免依赖运行时反射元数据并保持稳定缩进。
    /// </summary>
    /// <param name="root">待序列化的 manifest 根对象。</param>
    /// <returns>带平台换行结尾的完整 JSON。</returns>
    private static string Serialize(JsonObject root)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
        {
            root.WriteTo(writer);
            writer.Flush();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }

    /// <summary>
    /// 获取并验证 Unity Packages/manifest.json 固定路径。
    /// </summary>
    /// <param name="projectRoot">目标 Unity 项目根。</param>
    /// <returns>manifest 完整路径。</returns>
    private static string GetManifestPath(string projectRoot)
    {
        var fullProjectRoot = InstallerPathGuard.RequireFullPath(projectRoot, nameof(projectRoot));
        var manifestPath = InstallerPathGuard.CombineInside(fullProjectRoot, "Packages", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Unity Packages/manifest.json was not found.", manifestPath);
        }

        return manifestPath;
    }

    /// <summary>
    /// 使用同目录临时文件、落盘 flush 和原子重命名提交完整 manifest。
    /// </summary>
    /// <param name="targetPath">manifest 正式路径。</param>
    /// <param name="content">完整 JSON 或原始回滚文本。</param>
    private static void WriteAtomic(string targetPath, string content)
    {
        var temporaryPath = targetPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            WriteTemporaryFile(temporaryPath, content);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// 以无 BOM UTF-8 和 WriteThrough 写入临时文件，并在重命名前强制落盘。
    /// </summary>
    /// <param name="temporaryPath">同目录临时路径。</param>
    /// <param name="content">待写入完整内容。</param>
    private static void WriteTemporaryFile(string temporaryPath, string content)
    {
        using FileStream stream = new(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.WriteThrough);
        using StreamWriter writer = new(stream, new UTF8Encoding(false));
        writer.Write(content);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }
}

/// <summary>
/// 保存一次 Unity manifest 只读解析结果和原文，用于计划与失败回滚。
/// </summary>
internal sealed class UnityManifestSnapshot
{
    /// <summary>
    /// 创建结构化 manifest 快照。
    /// </summary>
    /// <param name="manifestPath">manifest 完整路径。</param>
    /// <param name="originalText">读取时的完整原文。</param>
    /// <param name="root">解析后的根对象。</param>
    /// <param name="dependencies">dependencies 对象；缺失时为空。</param>
    /// <param name="currentDependency">当前 YokiFrame 本地 file 或 Git 依赖；未登记时为空。</param>
    internal UnityManifestSnapshot(
        string manifestPath,
        string originalText,
        JsonObject root,
        JsonObject? dependencies,
        string? currentDependency)
    {
        ManifestPath = manifestPath;
        OriginalText = originalText;
        Root = root;
        Dependencies = dependencies;
        CurrentDependency = currentDependency;
    }

    /// <summary>
    /// 获取 manifest 完整路径。
    /// </summary>
    internal string ManifestPath { get; }

    /// <summary>
    /// 获取读取时的完整原文。
    /// </summary>
    internal string OriginalText { get; }

    /// <summary>
    /// 获取解析后的根对象。
    /// </summary>
    internal JsonObject Root { get; }

    /// <summary>
    /// 获取 dependencies 对象；缺失时为空。
    /// </summary>
    internal JsonObject? Dependencies { get; }

    /// <summary>
    /// 获取当前 YokiFrame 本地 file 或 Git 依赖；未登记时为空。
    /// </summary>
    internal string? CurrentDependency { get; }
}
