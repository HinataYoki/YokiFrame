namespace YokiFrame.Protocol.Tests;

/// <summary>
/// 覆盖 Protocol DTO 在 Native AOT 下必须使用 source-generated JSON 元数据的契约。
/// </summary>
public sealed class ProtocolJsonAotTests
{
    /// <summary>
    /// 验证协议 DTO 的序列化入口不再使用反射型 JsonSerializerOptions，避免 Native AOT 发布版运行时报错。
    /// </summary>
    [Fact]
    public void ProtocolDtosUseSourceGeneratedJsonContext()
    {
        var sources = new[]
        {
            ReadProtocolSource("FileBridge", "CommandEnvelope.cs"),
            ReadProtocolSource("FileBridge", "CommandResponse.cs"),
            ReadProtocolSource("FileBridge", "EngineRegistryEntry.cs"),
            ReadProtocolSource("FastChannel", "FastChannelEndpoint.cs"),
            ReadProtocolSource("FastChannel", "FastChannelHandshake.cs")
        };
        var combinedSource = string.Join(Environment.NewLine, sources);

        Assert.Contains("YokiFrameProtocolJsonContext.Default.CommandEnvelope", combinedSource);
        Assert.Contains("YokiFrameProtocolJsonContext.Default.CommandResponse", combinedSource);
        Assert.Contains("YokiFrameProtocolJsonContext.Default.EngineRegistryEntry", combinedSource);
        Assert.Contains("YokiFrameProtocolJsonContext.Default.FastChannelEndpoint", combinedSource);
        Assert.Contains("YokiFrameProtocolJsonContext.Default.FastChannelSessionIdentity", combinedSource);
        Assert.DoesNotContain("JsonSerializer.Deserialize<CommandEnvelope>(json, YokiFrameJson.CompactOptions)", combinedSource);
        Assert.DoesNotContain("JsonSerializer.Serialize(this, YokiFrameJson.CompactOptions)", combinedSource);
        Assert.DoesNotContain("JsonSerializer.Deserialize<CommandResponse>(json, YokiFrameJson.CompactOptions)", combinedSource);
        Assert.DoesNotContain("JsonSerializer.Deserialize<EngineRegistryEntry>(json, YokiFrameJson.CompactOptions)", combinedSource);
        Assert.DoesNotContain("JsonSerializer.Deserialize<FastChannelEndpoint>(json, YokiFrameJson.CompactOptions)", combinedSource);
    }

    /// <summary>
    /// 验证 Protocol 项目声明了 JSON source generator 上下文，保证 AOT 编译时能生成 DTO 元数据。
    /// </summary>
    [Fact]
    public void ProtocolDeclaresSourceGeneratedJsonContext()
    {
        var source = ReadProtocolSource("Common", "YokiFrameProtocolJsonContext.cs");

        Assert.Contains("[JsonSourceGenerationOptions", source);
        Assert.Contains("[JsonSerializable(typeof(CommandEnvelope))]", source);
        Assert.Contains("[JsonSerializable(typeof(CommandResponse))]", source);
        Assert.Contains("[JsonSerializable(typeof(EngineRegistryEntry))]", source);
        Assert.Contains("[JsonSerializable(typeof(FastChannelEndpoint))]", source);
        Assert.Contains("[JsonSerializable(typeof(FastChannelSessionIdentity))]", source);
        Assert.Contains("[JsonSerializable(typeof(ProjectModelManifest))]", source);
        Assert.Contains("[JsonSerializable(typeof(ProjectArchitectureDocument))]", source);
        Assert.Contains("[JsonSerializable(typeof(ProjectCapabilitiesDocument))]", source);
        Assert.Contains("[JsonSerializable(typeof(ProjectCapabilityDescriptor))]", source);
        Assert.Contains("[JsonSerializable(typeof(ProjectDependenciesDocument))]", source);
        Assert.Contains("[JsonSerializable(typeof(ProjectValidationProfileDocument))]", source);
        Assert.Contains("[JsonSerializable(typeof(ProjectModelBundle))]", source);
    }

    /// <summary>
    /// 从当前测试目录向上查找 Protocol 源码文件，用于验证 AOT 序列化契约。
    /// </summary>
    /// <param name="folder">Protocol 下的一级目录。</param>
    /// <param name="fileName">源码文件名。</param>
    /// <returns>源码文本。</returns>
    private static string ReadProtocolSource(string folder, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateProtocolSourceCandidates(directory.FullName, folder, fileName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 Protocol 源码文件: " + fileName);
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 Protocol 源码路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <param name="folder">Protocol 下的一级目录。</param>
    /// <param name="fileName">源码文件名。</param>
    /// <returns>候选源码路径。</returns>
    private static IEnumerable<string> CreateProtocolSourceCandidates(string directory, string folder, string fileName)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Protocol",
            folder,
            fileName);
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Protocol",
            folder,
            fileName);
    }
}
