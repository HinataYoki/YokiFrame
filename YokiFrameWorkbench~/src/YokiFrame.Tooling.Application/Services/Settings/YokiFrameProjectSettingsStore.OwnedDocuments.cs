using System.Text;

namespace YokiFrame.Tooling.Application.Services.Settings;

public sealed partial class YokiFrameProjectSettingsStore
{
    /// <summary>读取复杂 Workbench owned-document 的原文和 revision，不把内容投影成 Kit 条目。</summary>
    /// <param name="target">必须是支持 owned-document 的目标。</param>
    /// <returns>受项目锁保护的原文快照。</returns>
    public YokiFrameProjectOwnedDocumentSnapshot ReadOwnedDocument(YokiFrameProjectSettingsTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        EnsureOwnedDocumentBackend(target);
        using var lockLease = AcquireProjectLock();
        return ReadOwnedDocumentCore(target);
    }

    /// <summary>通过统一项目锁和原子替换提交复杂 owned-document。</summary>
    /// <param name="target">owned-document 目标。</param>
    /// <param name="content">待写入完整文本。</param>
    /// <param name="mode">合并最新或要求 revision。</param>
    /// <param name="expectedRevision">RequireRevision 模式下的读取 revision。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>提交结果和最新原文快照。</returns>
    public async Task<YokiFrameProjectOwnedDocumentWriteResult> WriteOwnedDocumentAsync(
        YokiFrameProjectSettingsTarget target,
        string content,
        YokiFrameProjectSettingsWriteMode mode = YokiFrameProjectSettingsWriteMode.MergeLatest,
        string expectedRevision = "",
        CancellationToken cancellationToken = default)
    {
        ValidateOwnedDocumentWrite(target, content, mode, expectedRevision);
        return await Task.Run(
            () => WriteOwnedDocumentCore(target, content, mode, expectedRevision, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>校验 owned-document 目标、内容大小和 revision 参数。</summary>
    /// <param name="target">owned-document 目标。</param>
    /// <param name="content">待提交完整文本。</param>
    /// <param name="mode">并发写入模式。</param>
    /// <param name="expectedRevision">RequireRevision 模式要求的指纹。</param>
    private void ValidateOwnedDocumentWrite(
        YokiFrameProjectSettingsTarget target,
        string content,
        YokiFrameProjectSettingsWriteMode mode,
        string expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(content);
        EnsureOwnedDocumentBackend(target);
        if (Encoding.UTF8.GetByteCount(content) > MAX_DOCUMENT_BYTES)
            throw new InvalidDataException("Owned settings document exceeds 4 MiB.");
        if (mode == YokiFrameProjectSettingsWriteMode.RequireRevision
            && string.IsNullOrWhiteSpace(expectedRevision))
            throw new ArgumentException("A revision is required for checked owned-document writes.", nameof(expectedRevision));
    }

    /// <summary>确认目标后端明确允许完整 owned-document 读写，避免绕过 Runtime/Editor patch 规则。</summary>
    /// <param name="target">待验证目标。</param>
    private void EnsureOwnedDocumentBackend(YokiFrameProjectSettingsTarget target)
    {
        if (ResolveBackend(target) is not IYokiFrameProjectOwnedDocumentBackend)
            throw new InvalidOperationException(
                "Target " + target.Id + " does not support owned-document access.");
    }

    /// <summary>读取 owned-document 的当前原文和指纹。</summary>
    /// <param name="target">owned-document 目标。</param>
    /// <returns>当前原文快照。</returns>
    private YokiFrameProjectOwnedDocumentSnapshot ReadOwnedDocumentCore(YokiFrameProjectSettingsTarget target)
    {
        YokiFrameProjectSettingsBackendDocument document = LoadBackendDocument(target);
        string fingerprint = document.Exists
            ? ResolveBackendFingerprint(document)
            : MISSING_FINGERPRINT;
        return new YokiFrameProjectOwnedDocumentSnapshot(
            target, document.Path, document.Exists, fingerprint, document.OriginalText);
    }

    /// <summary>在项目级锁和跨进程 Mutex 内提交 owned-document。</summary>
    /// <param name="target">owned-document 目标。</param>
    /// <param name="content">待提交完整文本。</param>
    /// <param name="mode">并发写入模式。</param>
    /// <param name="expectedRevision">RequireRevision 模式要求的指纹。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>提交状态和最新快照。</returns>
    private YokiFrameProjectOwnedDocumentWriteResult WriteOwnedDocumentCore(
        YokiFrameProjectSettingsTarget target,
        string content,
        YokiFrameProjectSettingsWriteMode mode,
        string expectedRevision,
        CancellationToken cancellationToken)
    {
        using var lockLease = AcquireProjectLock(cancellationToken);
        return WriteOwnedDocumentLocked(target, content, mode, expectedRevision, cancellationToken);
    }

    /// <summary>复用统一准备文件对象校验 revision、原子提交，并在读取新快照失败时回滚。</summary>
    /// <param name="target">owned-document 目标。</param>
    /// <param name="content">待提交完整文本。</param>
    /// <param name="mode">并发写入模式。</param>
    /// <param name="expectedRevision">RequireRevision 模式要求的指纹。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>提交状态和最新快照。</returns>
    private YokiFrameProjectOwnedDocumentWriteResult WriteOwnedDocumentLocked(
        YokiFrameProjectSettingsTarget target,
        string content,
        YokiFrameProjectSettingsWriteMode mode,
        string expectedRevision,
        CancellationToken cancellationToken)
    {
        YokiFrameProjectOwnedDocumentSnapshot current = ReadOwnedDocumentCore(target);
        if (mode == YokiFrameProjectSettingsWriteMode.RequireRevision
            && !string.Equals(current.Fingerprint, expectedRevision, StringComparison.Ordinal))
            return new YokiFrameProjectOwnedDocumentWriteResult(false, true, current);

        LoadedSettingsDocument document = new(
            target, current.Path, current.Exists, current.Fingerprint, current.Content,
            new List<YokiFrameProjectSetting>());
        PreparedSettingsFile prepared = PreparedSettingsFile.Create(
            document, content, Guid.NewGuid().ToString("N"));
        try
        {
            if (!prepared.MatchesOriginal())
                return new YokiFrameProjectOwnedDocumentWriteResult(false, true, ReadOwnedDocumentCore(target));
            cancellationToken.ThrowIfCancellationRequested();
            prepared.Commit();
            try
            {
                return new YokiFrameProjectOwnedDocumentWriteResult(true, false, ReadOwnedDocumentCore(target));
            }
            catch
            {
                prepared.Rollback();
                throw;
            }
        }
        finally
        {
            prepared.Cleanup();
        }
    }

    /// <summary>优先使用后端的原始字节指纹，缺失时按 UTF-8 原文计算。</summary>
    /// <param name="document">后端读取文档。</param>
    /// <returns>可用于 revision 校验的稳定指纹。</returns>
    private static string ResolveBackendFingerprint(YokiFrameProjectSettingsBackendDocument document)
    {
        return string.IsNullOrEmpty(document.Fingerprint)
            ? ComputeFingerprint(Encoding.UTF8.GetBytes(document.OriginalText))
            : document.Fingerprint;
    }
}
