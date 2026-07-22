using System.Security.Cryptography;
using System.Text;

namespace YokiFrame.Tooling.Application.Services.Settings;

public sealed partial class YokiFrameProjectSettingsStore
{
    /// <summary>按稳定目标顺序加载全部物理配置文档。</summary>
    private LoadedSettingsDocument[] LoadDocuments(IReadOnlyList<YokiFrameProjectSettingsTarget> targets)
    {
        return targets.OrderBy(static target => target.Id, StringComparer.Ordinal).Select(LoadDocument).ToArray();
    }

    /// <summary>根据目标从已注册后端读取并转换为 Store 内部可变文档。</summary>
    private LoadedSettingsDocument LoadDocument(YokiFrameProjectSettingsTarget target)
    {
        YokiFrameProjectSettingsBackendDocument document = LoadBackendDocument(target);
        return new LoadedSettingsDocument(
            document.Target,
            document.Path,
            document.Exists,
            document.Exists
                ? (string.IsNullOrEmpty(document.Fingerprint)
                    ? ComputeFingerprint(Encoding.UTF8.GetBytes(document.OriginalText))
                    : document.Fingerprint)
                : MISSING_FINGERPRINT,
            document.OriginalText,
            document.Settings.ToList());
    }

    /// <summary>把一个目标的全部 owner patch 应用到内存条目，不接触物理文件。</summary>
    private static void ApplyPatches(
        LoadedSettingsDocument document,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        foreach (YokiFrameProjectSettingsPatch patch in patches)
        {
            document.Settings.RemoveAll(setting => patch.Owns(setting.Owner, setting.Key));
            foreach (YokiFrameProjectSettingValue value in patch.Values)
            {
                document.Settings.Add(new YokiFrameProjectSetting(patch.Owner, value.Key, value.Value ?? string.Empty));
            }
        }
    }

    /// <summary>确认所有正式文件仍与本次读取时的内容指纹一致。</summary>
    private static bool MatchOriginalFingerprints(IReadOnlyList<LoadedSettingsDocument> documents)
    {
        return documents.All(static document =>
            string.Equals(ReadCurrentFingerprint(document.Path), document.Fingerprint, StringComparison.Ordinal));
    }

    /// <summary>准备全部临时文件，复核指纹后按稳定顺序提交并在失败时回滚。</summary>
    private bool CommitDocuments(
        IReadOnlyList<LoadedSettingsDocument> documents,
        IReadOnlyDictionary<YokiFrameProjectSettingsTarget, IReadOnlyList<YokiFrameProjectSettingsPatch>> patches,
        CancellationToken cancellationToken)
    {
        string transactionId = Guid.NewGuid().ToString("N");
        List<PreparedSettingsFile> prepared = PrepareDocuments(documents, patches, transactionId);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!prepared.All(static item => item.MatchesOriginal())) return false;
            foreach (PreparedSettingsFile item in prepared)
            {
                cancellationToken.ThrowIfCancellationRequested();
                item.Commit();
            }

            return true;
        }
        catch (Exception commitException)
        {
            RollbackPrepared(prepared, commitException);
            throw;
        }
        finally
        {
            foreach (PreparedSettingsFile item in prepared) item.Cleanup();
        }
    }

    /// <summary>为每个目标序列化并创建已 flush 的同目录临时文件。</summary>
    private List<PreparedSettingsFile> PrepareDocuments(
        IReadOnlyList<LoadedSettingsDocument> documents,
        IReadOnlyDictionary<YokiFrameProjectSettingsTarget, IReadOnlyList<YokiFrameProjectSettingsPatch>> patches,
        string transactionId)
    {
        List<PreparedSettingsFile> prepared = new(documents.Count);
        try
        {
            foreach (LoadedSettingsDocument document in documents)
            {
                string content = SerializeDocument(document, patches[document.Target]);
                prepared.Add(PreparedSettingsFile.Create(document, content, transactionId));
            }

            return prepared;
        }
        catch
        {
            foreach (PreparedSettingsFile item in prepared) item.Cleanup();
            throw;
        }
    }

    /// <summary>根据物理目标选择稳定序列化实现。</summary>
    private string SerializeDocument(
        LoadedSettingsDocument document,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        return ResolveBackend(document.Target).Serialize(document.ToBackend(), patches);
    }

    /// <summary>按提交反序恢复已经替换的正式文件；回滚失败时保留两段异常证据。</summary>
    private static void RollbackPrepared(
        IReadOnlyList<PreparedSettingsFile> prepared,
        Exception commitException)
    {
        try
        {
            for (var index = prepared.Count - 1; index >= 0; index--) prepared[index].Rollback();
        }
        catch (Exception rollbackException)
        {
            throw new AggregateException("Settings commit and rollback both failed.", commitException, rollbackException);
        }
    }

    /// <summary>读取有界文件；缺失文件由调用方建立空文档。</summary>
    internal static byte[] ReadBoundedFile(string path)
    {
        FileInfo info = new(path);
        if (info.Length > MAX_DOCUMENT_BYTES) throw new InvalidDataException("Settings document exceeds 4 MiB.");
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length > MAX_DOCUMENT_BYTES) throw new InvalidDataException("Settings document exceeds 4 MiB.");
        return bytes;
    }

    /// <summary>读取当前正式文件指纹；缺失文件返回稳定 missing。</summary>
    private static string ReadCurrentFingerprint(string path)
    {
        return File.Exists(path) ? ComputeFingerprint(ReadBoundedFile(path)) : MISSING_FINGERPRINT;
    }

    /// <summary>计算字节内容的稳定 SHA-256 指纹。</summary>
    internal static string ComputeFingerprint(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>计算单目标兼容指纹或多目标稳定组合 revision。</summary>
    private static string ComputeCombinedFingerprint(IReadOnlyList<LoadedSettingsDocument> documents)
    {
        if (documents.Count == 1) return documents[0].Fingerprint;
        if (documents.All(static document => !document.Exists)) return MISSING_FINGERPRINT;
        StringBuilder builder = new();
        foreach (LoadedSettingsDocument document in documents.OrderBy(static item => item.Target.Id, StringComparer.Ordinal))
        {
            builder.Append(document.Target.Id).Append(':').Append(document.Fingerprint).Append('|');
        }

        return ComputeFingerprint(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    /// <summary>保存单个物理目标的原文、指纹和结构化条目。</summary>
    private sealed class LoadedSettingsDocument
    {
        /// <summary>创建一次读取期间使用的可变内存文档。</summary>
        internal LoadedSettingsDocument(
            YokiFrameProjectSettingsTarget target,
            string path,
            bool exists,
            string fingerprint,
            string originalText,
            List<YokiFrameProjectSetting> settings)
        {
            Target = target;
            Path = path;
            Exists = exists;
            Fingerprint = fingerprint;
            OriginalText = originalText;
            Settings = settings;
        }

        internal YokiFrameProjectSettingsTarget Target { get; }
        internal string Path { get; }
        internal bool Exists { get; }
        internal string Fingerprint { get; }
        internal string OriginalText { get; }
        internal List<YokiFrameProjectSetting> Settings { get; }

        /// <summary>转换为后端可消费的只读结构化文档。</summary>
        internal YokiFrameProjectSettingsBackendDocument ToBackend()
        {
            return new YokiFrameProjectSettingsBackendDocument(
                Target,
                Path,
                Exists,
                OriginalText,
                Settings.ToArray());
        }

        /// <summary>复制条目并创建对调用方只读的文档。</summary>
        internal YokiFrameProjectSettingsDocument ToPublic()
        {
            return new YokiFrameProjectSettingsDocument(Target, Path, Exists, Fingerprint, Settings.ToArray());
        }
    }

    /// <summary>封装一个正式文件在事务中的临时文件、备份和回滚状态。</summary>
    private sealed class PreparedSettingsFile
    {
        private readonly string mTargetPath;
        private readonly string mTemporaryPath;
        private readonly string mBackupPath;
        private readonly string mExpectedFingerprint;
        private readonly bool mOriginallyExisted;
        private bool mCommitted;

        /// <summary>保存已准备文件的事务状态。</summary>
        private PreparedSettingsFile(
            string targetPath,
            string temporaryPath,
            string backupPath,
            string expectedFingerprint,
            bool originallyExisted)
        {
            mTargetPath = targetPath;
            mTemporaryPath = temporaryPath;
            mBackupPath = backupPath;
            mExpectedFingerprint = expectedFingerprint;
            mOriginallyExisted = originallyExisted;
        }

        /// <summary>创建并强制刷新同目录临时文件。</summary>
        internal static PreparedSettingsFile Create(
            LoadedSettingsDocument document,
            string content,
            string transactionId)
        {
            string directory = Path.GetDirectoryName(document.Path)
                ?? throw new InvalidOperationException("Settings directory is unavailable.");
            Directory.CreateDirectory(directory);
            string temporaryPath = document.Path + ".tmp-" + transactionId;
            string backupPath = document.Path + ".bak-" + transactionId;
            WriteTemporary(temporaryPath, content);
            return new PreparedSettingsFile(
                document.Path, temporaryPath, backupPath, document.Fingerprint, document.Exists);
        }

        /// <summary>确认正式文件仍为准备前读取的版本。</summary>
        internal bool MatchesOriginal()
        {
            return string.Equals(ReadCurrentFingerprint(mTargetPath), mExpectedFingerprint, StringComparison.Ordinal);
        }

        /// <summary>以原子替换提交临时文件，并为既有目标保留事务备份。</summary>
        internal void Commit()
        {
            if (mOriginallyExisted) File.Replace(mTemporaryPath, mTargetPath, mBackupPath);
            else File.Move(mTemporaryPath, mTargetPath);
            mCommitted = true;
        }

        /// <summary>恢复已经提交的目标；未提交目标保持原状。</summary>
        internal void Rollback()
        {
            if (!mCommitted) return;
            if (mOriginallyExisted) File.Replace(mBackupPath, mTargetPath, null);
            else if (File.Exists(mTargetPath)) File.Delete(mTargetPath);
            mCommitted = false;
        }

        /// <summary>删除未被提交或回滚消费的临时文件和备份。</summary>
        internal void Cleanup()
        {
            if (File.Exists(mTemporaryPath)) File.Delete(mTemporaryPath);
            if (File.Exists(mBackupPath)) File.Delete(mBackupPath);
        }

        /// <summary>使用无 BOM UTF-8 写入临时文件并强制刷新到底层存储。</summary>
        private static void WriteTemporary(string path, string content)
        {
            using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using StreamWriter writer = new(stream, new UTF8Encoding(false));
            writer.Write(content);
            writer.Flush();
            stream.Flush(true);
        }
    }
}
