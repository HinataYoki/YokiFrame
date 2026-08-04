namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 提供 Unity 包和 Godot add-on 事务共同使用的同卷目录提交操作。
/// </summary>
internal static class InstallerDirectoryTransaction
{
    private const int MAX_MOVE_ATTEMPTS = 20;
    private const int MOVE_RETRY_DELAY_MILLISECONDS = 100;

    /// <summary>
    /// 将 staging 或备份目录移动到目标位置，并只重试短暂占用冲突。
    /// </summary>
    /// <param name="sourcePath">必须仍存在的源目录。</param>
    /// <param name="destinationPath">必须尚不存在的目标目录。</param>
    internal static void MoveWithRetry(string sourcePath, string destinationPath)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(sourcePath, destinationPath);
                return;
            }
            catch (Exception exception) when (CanRetry(sourcePath, destinationPath, attempt, exception))
            {
                Thread.Sleep(MOVE_RETRY_DELAY_MILLISECONDS);
            }
        }
    }

    /// <summary>
    /// 判断当前目录移动异常是否仍处于有界、可恢复的短暂冲突窗口。
    /// </summary>
    /// <param name="sourcePath">目录移动源路径。</param>
    /// <param name="destinationPath">目录移动目标路径。</param>
    /// <param name="attempt">当前尝试序号。</param>
    /// <param name="exception">本次移动异常。</param>
    /// <returns>可以安全重试时返回 true。</returns>
    private static bool CanRetry(
        string sourcePath,
        string destinationPath,
        int attempt,
        Exception exception)
    {
        return attempt < MAX_MOVE_ATTEMPTS
            && exception is IOException or UnauthorizedAccessException
            && exception is not DirectoryNotFoundException
            && exception is not PathTooLongException
            && Directory.Exists(sourcePath)
            && !Directory.Exists(destinationPath)
            && !File.Exists(destinationPath);
    }
}
