using YokiFrame.Client.FileBridge.IO;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.ProjectModel;

/// <summary>
/// 承载 Project Model staging、目录替换和失败恢复逻辑。
/// </summary>
public sealed partial class ProjectModelFileStore
{
    /// <summary>
    /// 在提交失败后删除新目录并把旧 bundle 从备份恢复到固定目录。
    /// </summary>
    /// <param name="stagingPath">候选 staging 路径。</param>
    /// <param name="backupPath">旧 bundle 备份路径。</param>
    /// <param name="stagingExists">staging 是否仍存在。</param>
    /// <param name="backupExists">备份是否仍存在。</param>
    /// <param name="replacementInstalled">候选 bundle 是否已经移动到正式目录。</param>
    private void RestoreAfterCommitFailure(
        string stagingPath,
        string backupPath,
        bool stagingExists,
        bool backupExists,
        bool replacementInstalled)
    {
        try
        {
            if (stagingExists)
            {
                DeleteDirectory(stagingPath);
            }

            if (replacementInstalled)
            {
                DeleteDirectory(mPaths.ProjectModelRoot);
            }

            if (backupExists)
            {
                Directory.Move(backupPath, mPaths.ProjectModelRoot);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw CreateStoreException(
                "ProjectModelRollbackFailed",
                "Project Model rollback failed: " + exception.Message,
                new[] { stagingPath, backupPath, mPaths.ProjectModelRoot });
        }
    }

    /// <summary>
    /// 删除临时或备份目录；不存在时保持幂等。
    /// </summary>
    /// <param name="path">待删除目录。</param>
    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
