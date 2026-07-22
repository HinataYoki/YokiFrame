using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.IO;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.ProjectModel;

/// <summary>
/// 以五文件 bundle 管理项目模型，并把 project-model.json 作为最后提交的 commit root。
/// </summary>
public sealed partial class ProjectModelFileStore
{
    private static readonly UTF8Encoding sStrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly YokiFramePaths mPaths;

    /// <summary>
    /// 使用已解析的 YokiFrame 路径创建 Project Model store。
    /// </summary>
    /// <param name="paths">项目内固定路径集合。</param>
    public ProjectModelFileStore(YokiFramePaths paths)
    {
        mPaths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    /// <summary>
    /// 根据项目根创建 Project Model store。
    /// </summary>
    /// <param name="projectRoot">Unity 或 Godot 项目根目录。</param>
    public ProjectModelFileStore(string projectRoot)
        : this(new YokiFramePaths(projectRoot))
    {
    }

    /// <summary>
    /// 返回五个固定模型文件作为错误和审计证据，调用方不能传入任意文件名。
    /// </summary>
    /// <returns>按提交顺序排列的五个文件路径。</returns>
    public IReadOnlyList<string> GetEvidencePaths()
    {
        return new[]
        {
            mPaths.ProjectModelManifestPath,
            mPaths.ProjectArchitecturePath,
            mPaths.ProjectCapabilitiesPath,
            mPaths.ProjectDependenciesPath,
            mPaths.ProjectValidationProfilePath
        };
    }

    /// <summary>
    /// 在项目级独占锁内读取并验证完整五文件 bundle。
    /// </summary>
    /// <returns>经过 schema、generation、modelId 和 hash 校验的模型快照。</returns>
    public ProjectModelBundle Read()
    {
        using var lockStream = AcquireProjectLock();
        try
        {
            return ReadBundleUnlocked(mPaths.ProjectModelRoot);
        }
        catch (YokiFrameProtocolException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw CreateStoreException(
                "ProjectModelReadFailed",
                "Project Model bundle cannot be read: " + exception.Message,
                new[] { mPaths.ProjectModelRoot });
        }
    }

    /// <summary>
    /// 在同卷 staging 中写入四个叶文档，最后写 manifest，再以目录备份和替换提交。
    /// </summary>
    /// <param name="bundle">待提交的五文件模型。</param>
    public void Commit(ProjectModelBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        using var lockStream = AcquireProjectLock();
        ValidateBundleForCommit(bundle);

        CommitUnlocked(bundle);
    }

    /// <summary>
    /// 在调用方持有项目锁时完成 staging 写入、目录替换、验证和失败恢复。
    /// </summary>
    /// <param name="bundle">已通过结构校验的五文件模型。</param>
    private void CommitUnlocked(ProjectModelBundle bundle)
    {
        var stagingPath = CreateSiblingPath(".project-staging-");
        var backupPath = CreateSiblingPath(".project-backup-");
        var stagingExists = false;
        var backupExists = false;
        var replacementInstalled = false;
        try
        {
            Directory.CreateDirectory(stagingPath);
            stagingExists = true;
            var leafBytes = SerializeLeaves(bundle);
            WriteStagedLeaves(stagingPath, leafBytes);
            bundle.Manifest.Documents = CreateDocumentReferences(leafBytes);
            WriteStagedFile(
                Path.Combine(stagingPath, ProjectModelContract.PROJECT_MODEL_FILE_NAME),
                SerializeUtf8(bundle.Manifest.ToJson()));
            _ = ReadBundleUnlocked(stagingPath);

            if (Directory.Exists(mPaths.ProjectModelRoot))
            {
                EnsureManagedDirectory(mPaths.ProjectModelRoot);
                Directory.Move(mPaths.ProjectModelRoot, backupPath);
                backupExists = true;
            }

            Directory.Move(stagingPath, mPaths.ProjectModelRoot);
            stagingExists = false;
            replacementInstalled = true;
            _ = ReadBundleUnlocked(mPaths.ProjectModelRoot);
            DeleteDirectory(backupPath);
            backupExists = false;
        }
        catch (YokiFrameProtocolException)
        {
            RestoreAfterCommitFailure(stagingPath, backupPath, stagingExists, backupExists, replacementInstalled);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            RestoreAfterCommitFailure(stagingPath, backupPath, stagingExists, backupExists, replacementInstalled);
            throw CreateStoreException(
                "ProjectModelCommitFailed",
                "Project Model commit failed: " + exception.Message,
                new[] { stagingPath, backupPath });
        }
    }

    /// <summary>
    /// 读取不再重复打开锁的完整 bundle；仅由已持锁的公开操作或 staging 校验调用。
    /// </summary>
    /// <param name="bundleRoot">待验证的 bundle 目录。</param>
    /// <returns>已解析且完整性通过的模型 bundle。</returns>
    private ProjectModelBundle ReadBundleUnlocked(string bundleRoot)
    {
        EnsureNoReparsePoint(bundleRoot);
        var manifestBytes = ReadRequiredFile(Path.Combine(bundleRoot, ProjectModelContract.PROJECT_MODEL_FILE_NAME));
        var manifest = ParseManifest(manifestBytes, Path.Combine(bundleRoot, ProjectModelContract.PROJECT_MODEL_FILE_NAME));
        ValidateManifest(manifest, bundleRoot);
        var architecture = ReadArchitecture(bundleRoot, manifest);
        var capabilities = ReadCapabilities(bundleRoot, manifest);
        var dependencies = ReadDependencies(bundleRoot, manifest);
        var validationProfile = ReadValidationProfile(bundleRoot, manifest);
        return new ProjectModelBundle
        {
            Manifest = manifest,
            Architecture = architecture,
            Capabilities = capabilities,
            Dependencies = dependencies,
            ValidationProfile = validationProfile
        };
    }

    /// <summary>
    /// 创建项目根内的临时或备份兄弟目录，避免跨卷移动和路径逃逸。
    /// </summary>
    /// <param name="prefix">受控目录名前缀。</param>
    /// <returns>尚不存在的目录路径。</returns>
    private string CreateSiblingPath(string prefix)
    {
        EnsureNoReparsePoint(mPaths.YokiFrameRoot);
        var path = Path.Combine(mPaths.YokiFrameRoot, prefix + Guid.NewGuid().ToString("N"));
        return PathSecurity.EnsureInside(mPaths.YokiFrameRoot, path);
    }

    /// <summary>
    /// 创建项目级独占锁；锁文件位于 bundle 目录外，防止目录替换时被移走。
    /// </summary>
    /// <returns>持有期间阻止其它读写操作的文件流。</returns>
    private FileStream AcquireProjectLock()
    {
        Directory.CreateDirectory(mPaths.YokiFrameRoot);
        EnsureNoReparsePoint(mPaths.ProjectModelLockPath);
        try
        {
            return new FileStream(
                mPaths.ProjectModelLockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.WriteThrough);
        }
        catch (IOException exception)
        {
            throw CreateStoreException(
                "ProjectModelBusy",
                "Project Model is locked by another process: " + exception.Message,
                new[] { mPaths.ProjectModelLockPath });
        }
        catch (UnauthorizedAccessException exception)
        {
            throw CreateStoreException(
                "ProjectModelLockDenied",
                "Project Model lock cannot be opened: " + exception.Message,
                new[] { mPaths.ProjectModelLockPath });
        }
    }

    /// <summary>
    /// 将字符串转为无 BOM UTF-8 字节，供 hash 和磁盘内容使用同一事实。
    /// </summary>
    /// <param name="json">待写入的 compact JSON。</param>
    /// <returns>无 BOM UTF-8 字节。</returns>
    private static byte[] SerializeUtf8(string json)
    {
        return sStrictUtf8.GetBytes(json);
    }

    /// <summary>
    /// 计算叶文档的 SHA-256 小写十六进制摘要。
    /// </summary>
    /// <param name="bytes">完整 UTF-8 文件字节。</param>
    /// <returns>小写 SHA-256 摘要。</returns>
    private static string ComputeHash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
