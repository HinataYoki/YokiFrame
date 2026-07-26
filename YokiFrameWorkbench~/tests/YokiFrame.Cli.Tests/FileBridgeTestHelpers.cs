namespace YokiFrame.Cli.Tests;

/// <summary>
/// FileBridge 命令队列轮询工具，消除跨 CLI 测试的重复等待逻辑。
/// </summary>
internal static class FileBridgeTestHelpers
{
    /// <summary>
    /// 轮询等待指定 commands 目录出现唯一 pending JSON 命令文件。
    /// 使用 FirstOrDefault 而非 SingleOrDefault，避免 SUT 写入多文件时抛出误导性异常。
    /// </summary>
    /// <param name="commandsRoot">FileBridge commands 目录。</param>
    /// <param name="attempts">最大轮询次数。</param>
    /// <param name="delayMs">每次轮询间隔毫秒数。</param>
    /// <returns>已完成写入的命令文件路径。</returns>
    internal static async Task<string> WaitForSingleCommandAsync(
        string commandsRoot,
        int attempts = 200,
        int delayMs = 25)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (Directory.Exists(commandsRoot))
            {
                var commandPath = Directory.EnumerateFiles(commandsRoot, "*.json").FirstOrDefault();
                if (commandPath != null)
                {
                    return commandPath;
                }
            }

            await Task.Delay(delayMs);
        }

        throw new TimeoutException($"等待 pending command 文件超时（commandsRoot={commandsRoot}）。");
    }
}
