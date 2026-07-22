using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Services.AudioKit;

namespace YokiFrame.Cli;

/// <summary>提供 AudioKit 稳定音频索引扫描和生成 CLI。</summary>
internal static class CliAudioKitCommands
{
    /// <summary>判断命令是否属于 AudioKit 索引边界。</summary>
    internal static bool IsAudioIndexCommand(CliCommandLine commandLine)
    {
        return commandLine.IsCommand("audio", "index", "scan")
            || commandLine.IsCommand("audio", "index", "generate");
    }

    /// <summary>执行只读扫描或原子生成并输出 compact JSON。</summary>
    internal static int Dispatch(CliCommandLine commandLine, IYokiFrameClient client)
    {
        try
        {
            AudioIndexRequest request = CreateRequest(commandLine, client.Paths.ProjectRoot);
            AudioIndexService service = new();
            bool generate = commandLine.IsCommand("audio", "index", "generate");
            AudioIndexResult result = generate ? service.Generate(request) : service.Scan(request);
            JsonObject payload = new()
            {
                ["command"] = generate ? "audio index generate" : "audio index scan",
                ["projectRoot"] = client.Paths.ProjectRoot,
                ["generatedFile"] = result.GeneratedFile,
                ["manifestFile"] = result.ManifestFile,
                ["manifestChanged"] = result.ManifestChanged,
                ["entryCount"] = result.Entries.Count,
                ["entries"] = CliJsonOutput.ToJsonNode(result.Entries.ToArray())
            };
            return CliJsonOutput.WriteSuccess(payload);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "AudioIndexFailed",
                exception.Message,
                "Check project-contained paths, unique audio names, manifest IDs and write permissions.",
                new[] { client.Paths.ProjectRoot }));
        }
    }

    /// <summary>从 CLI 选项创建带稳定默认值的索引请求。</summary>
    private static AudioIndexRequest CreateRequest(CliCommandLine commandLine, string projectRoot)
    {
        return new AudioIndexRequest(
            projectRoot,
            commandLine.GetOption("scan", "Assets/Art/Audio"),
            commandLine.GetOption("output", "Assets/Scripts/Generated/AudioIds.cs"),
            commandLine.GetOption("manifest", "Assets/Settings/YokiFrame/audio-index.json"),
            commandLine.GetOption("namespace", "GameAudio"),
            commandLine.GetOption("class", "AudioIds"),
            commandLine.GetIntOption("start-id", 1001));
    }
}
