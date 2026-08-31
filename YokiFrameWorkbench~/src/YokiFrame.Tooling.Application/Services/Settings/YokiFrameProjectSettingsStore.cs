using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace YokiFrame.Tooling.Application.Services.Settings;

/// <summary>
/// 以规范化项目根为边界统一读取、合并和提交 YokiFrame 项目配置。
/// 所有 Workbench 配置服务必须通过该 Store 写入共享 Runtime/Editor 文件。
/// </summary>
public sealed partial class YokiFrameProjectSettingsStore
{
    internal const string MISSING_FINGERPRINT = "missing";
    internal const int FORMAT_VERSION = 1;
    private const int MAX_DOCUMENT_BYTES = 4 * 1024 * 1024;
    private const int MAX_WRITE_ATTEMPTS = 3;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> sProjectLocks =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly string mProjectRoot;
    private readonly string mMutexName;
    private readonly SemaphoreSlim mProjectLock;
    private readonly IYokiFrameProjectSettingsBackend[] mBackends;

    /// <summary>创建绑定一个规范化项目根的共享配置 Store。</summary>
    /// <param name="projectRoot">当前引擎项目根；路径会在 Store 内规范化并绑定。</param>
    public YokiFrameProjectSettingsStore(
        string projectRoot,
        IEnumerable<IYokiFrameProjectSettingsBackend>? backends = null)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("Project root is required.", nameof(projectRoot));
        mProjectRoot = Path.GetFullPath(projectRoot);
        mProjectLock = sProjectLocks.GetOrAdd(mProjectRoot, static _ => new SemaphoreSlim(1, 1));
        mMutexName = CreateMutexName(mProjectRoot);
        mBackends = (backends ?? YokiFrameProjectSettingsBackendRegistry.CreateSnapshot()).ToArray();
        if (mBackends.Length == 0) throw new ArgumentException("At least one settings backend is required.", nameof(backends));
    }

    /// <summary>获取当前 Store 绑定的规范化项目根。</summary>
    public string ProjectRoot => mProjectRoot;

    /// <summary>获取指定目标的项目内配置绝对路径。</summary>
    /// <param name="target">配置目标。</param>
    /// <returns>受路径守卫约束的绝对路径。</returns>
    public string GetPath(YokiFrameProjectSettingsTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        IYokiFrameProjectSettingsBackend backend = ResolveBackend(target);
        return ResolveInside(backend.GetRelativePath(target));
    }

    /// <summary>读取一个或多个目标，返回同一项目锁保护下的结构化快照。</summary>
    /// <param name="targets">待读取的配置目标。</param>
    /// <returns>按目标和组合 revision 索引的快照。</returns>
    public YokiFrameProjectSettingsSnapshot Read(params YokiFrameProjectSettingsTarget[] targets)
    {
        ValidateTargets(targets);
        using var lockLease = AcquireProjectLock();
        return ReadCore(targets);
    }

    /// <summary>
    /// 通过唯一写入入口提交一批 owner patch；Store 会重读最新文件、校验 revision、原子提交并返回新快照。
    /// </summary>
    /// <param name="update">并发策略和结构化 patch。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>提交状态、冲突状态和最新快照。</returns>
    public async Task<YokiFrameProjectSettingsWriteResult> WriteAsync(
        YokiFrameProjectSettingsUpdate update,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        return await Task.Run(() => WriteCore(update, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>在项目级进程锁和跨进程 Mutex 内执行写入重试。</summary>
    private YokiFrameProjectSettingsWriteResult WriteCore(
        YokiFrameProjectSettingsUpdate update,
        CancellationToken cancellationToken)
    {
        ValidateUpdate(update);
        using var lockLease = AcquireProjectLock(cancellationToken);
        for (var attempt = 0; attempt < MAX_WRITE_ATTEMPTS; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteAttemptResult result = TryWriteOnce(update, cancellationToken);
            if (result.Saved) return new YokiFrameProjectSettingsWriteResult(true, false, result.Snapshot!);
            if (update.Mode == YokiFrameProjectSettingsWriteMode.RequireRevision || !result.ConflictDetected)
            {
                return new YokiFrameProjectSettingsWriteResult(false, result.ConflictDetected, result.CurrentSnapshot!);
            }
        }

        YokiFrameProjectSettingsSnapshot latest = ReadCore(GetTargets(update));
        return new YokiFrameProjectSettingsWriteResult(false, true, latest);
    }

    /// <summary>在持锁状态下执行一次读取、合并、指纹复核和原子提交。</summary>
    private WriteAttemptResult TryWriteOnce(
        YokiFrameProjectSettingsUpdate update,
        CancellationToken cancellationToken)
    {
        YokiFrameProjectSettingsTarget[] targets = GetTargets(update);
        if (update.Mode == YokiFrameProjectSettingsWriteMode.RequireRevision)
        {
            string rawRevision = ComputeRevisionFromPaths(targets);
            if (!string.Equals(rawRevision, update.ExpectedRevision, StringComparison.Ordinal))
            {
                return new WriteAttemptResult(
                    false,
                    true,
                    null,
                    ReadSnapshotForConflict(targets));
            }
        }

        LoadedSettingsDocument[] documents = LoadDocuments(targets);
        YokiFrameProjectSettingsSnapshot current = CreateSnapshot(documents);
        if (update.Mode == YokiFrameProjectSettingsWriteMode.RequireRevision
            && !string.Equals(current.Revision, update.ExpectedRevision, StringComparison.Ordinal))
        {
            return new WriteAttemptResult(false, true, null, current);
        }

        Dictionary<YokiFrameProjectSettingsTarget, IReadOnlyList<YokiFrameProjectSettingsPatch>> patches =
            update.Patches.GroupBy(static patch => patch.Target)
                .ToDictionary(static group => group.Key, static group => (IReadOnlyList<YokiFrameProjectSettingsPatch>)group.ToArray());
        foreach (LoadedSettingsDocument document in documents)
        {
            ApplyPatches(document, patches[document.Target]);
        }

        if (!MatchOriginalFingerprints(documents))
        {
            return new WriteAttemptResult(false, true, null, ReadCore(targets));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return CommitDocuments(documents, patches, cancellationToken)
            ? new WriteAttemptResult(true, false, CreateSnapshot(LoadDocuments(targets)), null)
            : new WriteAttemptResult(false, true, null, ReadCore(targets));
    }

    /// <summary>读取指定目标并计算稳定组合 revision。</summary>
    private YokiFrameProjectSettingsSnapshot ReadCore(IReadOnlyList<YokiFrameProjectSettingsTarget> targets)
    {
        return CreateSnapshot(LoadDocuments(targets));
    }

    /// <summary>仅读取文件指纹计算 revision，供损坏外部文件的冲突响应使用。</summary>
    private string ComputeRevisionFromPaths(IReadOnlyList<YokiFrameProjectSettingsTarget> targets)
    {
        return ComputeCombinedFingerprint(LoadDocumentsByFingerprint(targets));
    }

    /// <summary>读取冲突后的最新快照；文档损坏时返回只含路径和指纹的空投影。</summary>
    private YokiFrameProjectSettingsSnapshot ReadSnapshotForConflict(IReadOnlyList<YokiFrameProjectSettingsTarget> targets)
    {
        try
        {
            return ReadCore(targets);
        }
        catch (InvalidDataException)
        {
            return CreateSnapshot(LoadDocumentsByFingerprint(targets));
        }
    }

    /// <summary>按文件指纹建立不解析损坏正文的冲突快照。</summary>
    private LoadedSettingsDocument[] LoadDocumentsByFingerprint(IReadOnlyList<YokiFrameProjectSettingsTarget> targets)
    {
        return targets.OrderBy(static target => target.Id, StringComparer.Ordinal).Select(target =>
        {
            string path = GetPath(target);
            bool exists = File.Exists(path);
            return new LoadedSettingsDocument(
                target,
                path,
                exists,
                exists ? ReadCurrentFingerprint(path) : MISSING_FINGERPRINT,
                string.Empty,
                new List<YokiFrameProjectSetting>());
        }).ToArray();
    }

    /// <summary>把已加载文档转换为调用方可消费的结构化快照。</summary>
    private static YokiFrameProjectSettingsSnapshot CreateSnapshot(IReadOnlyList<LoadedSettingsDocument> documents)
    {
        Dictionary<YokiFrameProjectSettingsTarget, YokiFrameProjectSettingsDocument> result = new();
        foreach (LoadedSettingsDocument document in documents)
        {
            result.Add(document.Target, document.ToPublic());
        }

        string revision = ComputeCombinedFingerprint(documents);
        return new YokiFrameProjectSettingsSnapshot(result, revision);
    }

    /// <summary>获取更新涉及的去重目标，并按稳定枚举顺序排列。</summary>
    private static YokiFrameProjectSettingsTarget[] GetTargets(YokiFrameProjectSettingsUpdate update)
    {
        return update.Patches.Select(static patch => patch.Target).Distinct().OrderBy(static target => target.Id, StringComparer.Ordinal).ToArray();
    }

    /// <summary>校验目标列表非空且不包含重复目标。</summary>
    private static void ValidateTargets(IReadOnlyList<YokiFrameProjectSettingsTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            throw new ArgumentException("At least one settings target is required.", nameof(targets));
        HashSet<YokiFrameProjectSettingsTarget> seen = new(targets.Count);
        foreach (var target in targets)
            if (!seen.Add(target))
                throw new ArgumentException("Settings targets must be unique.", nameof(targets));
    }

    /// <summary>校验更新 patch 的目标和并发参数。</summary>
    private static void ValidateUpdate(YokiFrameProjectSettingsUpdate update)
    {
        if (update.Mode == YokiFrameProjectSettingsWriteMode.RequireRevision
            && string.IsNullOrWhiteSpace(update.ExpectedRevision))
        {
            throw new ArgumentException("A revision is required for checked settings writes.", nameof(update));
        }

    }

    /// <summary>按项目路径语义建立跨进程 Mutex。</summary>
    private Mutex CreateMutex() => new(false, mMutexName);

    /// <summary>取得项目内锁和跨进程 Mutex；任一等待失败时会立即释放已经取得的本地锁。</summary>
    /// <param name="cancellationToken">等待两个锁时使用的取消令牌。</param>
    /// <returns>负责按相反顺序释放两个锁的租约。</returns>
    private ProjectLockLease AcquireProjectLock(CancellationToken cancellationToken = default)
    {
        mProjectLock.Wait(cancellationToken);
        Mutex? mutex = null;
        try
        {
            mutex = CreateMutex();
            AcquireMutex(mutex, cancellationToken);
            return new ProjectLockLease(mProjectLock, mutex);
        }
        catch
        {
            mutex?.Dispose();
            mProjectLock.Release();
            throw;
        }
    }

    /// <summary>等待跨进程 Mutex，并把 abandoned 状态视为当前进程已取得锁。</summary>
    /// <param name="mutex">当前项目的命名 Mutex。</param>
    /// <param name="cancellationToken">等待取消令牌。</param>
    private static void AcquireMutex(Mutex mutex, CancellationToken cancellationToken = default)
    {
        try
        {
            while (!mutex.WaitOne(TimeSpan.FromMilliseconds(250)))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        catch (AbandonedMutexException)
        {
            // AbandonedMutexException 表示当前线程已经取得所有权，交由租约正常释放。
        }
    }

    /// <summary>创建绑定规范化项目根的稳定跨进程 Mutex 名称。</summary>
    /// <param name="projectRoot">项目根路径。</param>
    /// <returns>不泄露绝对路径的 Mutex 名称。</returns>
    internal static string CreateMutexName(string projectRoot)
    {
        return "YokiFrame.ProjectSettings." + ComputeProjectKey(Path.GetFullPath(projectRoot));
    }

    /// <summary>使用 SHA-256 生成不泄露项目绝对路径的 Mutex 名称片段。</summary>
    private static string ComputeProjectKey(string projectRoot)
    {
        string normalizedRoot = OperatingSystem.IsWindows() ? projectRoot.ToUpperInvariant() : projectRoot;
        byte[] bytes = Encoding.UTF8.GetBytes(normalizedRoot);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>解析后端相对路径并确认结果仍位于当前项目根目录。</summary>
    private string ResolveInside(string relativePath)
    {
        string candidate = Path.GetFullPath(Path.Combine(mProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string projectPrefix = mProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!candidate.StartsWith(projectPrefix, comparison))
        {
            throw new InvalidOperationException("Settings path escaped the project root.");
        }

        return candidate;
    }

    /// <summary>根据目标从当前 Store 的后端快照中选择唯一实现。</summary>
    /// <param name="target">待解析目标。</param>
    /// <returns>负责该目标的后端。</returns>
    private IYokiFrameProjectSettingsBackend ResolveBackend(YokiFrameProjectSettingsTarget target)
    {
        IYokiFrameProjectSettingsBackend? backend = mBackends.SingleOrDefault(item => item.CanHandle(target));
        return backend ?? throw new InvalidOperationException(
            "No project settings backend is registered for target " + target.Id + ".");
    }

    /// <summary>由后端读取一个目标并验证后端返回的目标、路径与 Store 请求一致。</summary>
    /// <param name="target">待读取目标。</param>
    /// <returns>后端结构化文档。</returns>
    private YokiFrameProjectSettingsBackendDocument LoadBackendDocument(YokiFrameProjectSettingsTarget target)
    {
        IYokiFrameProjectSettingsBackend backend = ResolveBackend(target);
        string path = GetPath(target);
        YokiFrameProjectSettingsBackendDocument document = backend.Read(target, path);
        if (document.Target != target || !string.Equals(document.Path, path, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Project settings backend returned a mismatched document.");
        }

        return document;
    }

    /// <summary>校验 Core 允许的 owner/key 安全标识。</summary>
    internal static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128 || value is "." or "..")
        {
            throw new ArgumentException("Settings identifiers must be 1-128 characters.", parameterName);
        }

        foreach (char character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            {
                throw new ArgumentException("Settings identifiers must use safe ASCII characters.", parameterName);
            }
        }
    }

    /// <summary>持有项目内锁与跨进程 Mutex，并保证异常释放 Mutex 时仍会归还本地锁。</summary>
    private sealed class ProjectLockLease : IDisposable
    {
        private readonly SemaphoreSlim mProjectLock;
        private readonly Mutex mMutex;
        private int mDisposed;

        /// <summary>记录已经成功取得的两级锁。</summary>
        /// <param name="projectLock">进程内项目锁。</param>
        /// <param name="mutex">跨进程项目 Mutex。</param>
        internal ProjectLockLease(SemaphoreSlim projectLock, Mutex mutex)
        {
            mProjectLock = projectLock;
            mMutex = mutex;
        }

        /// <summary>按跨进程到进程内的顺序释放锁，并保证多次调用无副作用。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref mDisposed, 1) != 0)
            {
                return;
            }

            try
            {
                mMutex.ReleaseMutex();
            }
            finally
            {
                mMutex.Dispose();
                mProjectLock.Release();
            }
        }
    }

    private sealed record WriteAttemptResult(
        bool Saved,
        bool ConflictDetected,
        YokiFrameProjectSettingsSnapshot? Snapshot,
        YokiFrameProjectSettingsSnapshot? CurrentSnapshot);
}
