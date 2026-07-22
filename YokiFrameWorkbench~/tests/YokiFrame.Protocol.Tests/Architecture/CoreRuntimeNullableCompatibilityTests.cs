namespace YokiFrame.Protocol.Tests.Architecture;

/// <summary>
/// 防止 Core Runtime 在关闭 Nullable 注解的宿主项目中重新引入会升级为编译错误的语法。
/// </summary>
public sealed class CoreRuntimeNullableCompatibilityTests
{
    /// <summary>
    /// 验证 FastChannel 请求队列在 Core 项目关闭 Nullable 时不使用引用类型可空注解，保持 Unity 和 Godot 的 C# 9 编译兼容。
    /// </summary>
    [Fact]
    public void FastChannelRequestQueueAvoidsNullableReferenceAnnotationsWhenCoreDisablesNullable()
    {
        var projectSource = ReadWorkspaceFile("Assets/YokiFrame/Core/Runtime/YokiFrame.csproj");
        var queueSource = ReadWorkspaceFile("Assets/YokiFrame/Core/Editor/CommandBridge/FastChannel/Queue/YokiFrameFastChannelRequestQueue.cs");

        Assert.Contains("<Nullable>disable</Nullable>", projectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PendingRequest?", queueSource, StringComparison.Ordinal);
        Assert.DoesNotContain("pendingRequest = null;", queueSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 从测试输出目录向上定位 Unity 工作区，并读取指定的源码或项目文件。
    /// </summary>
    /// <param name="relativePath">相对于 Unity 工作区根目录的路径。</param>
    /// <returns>完整文件文本。</returns>
    private static string ReadWorkspaceFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidatePath = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidatePath))
            {
                return File.ReadAllText(candidatePath);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位测试所需的工作区文件。", relativePath);
    }
}
