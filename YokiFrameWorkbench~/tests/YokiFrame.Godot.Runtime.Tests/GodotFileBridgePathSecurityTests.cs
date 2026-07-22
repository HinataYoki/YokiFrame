using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 覆盖 Godot Runtime Host 在创建协议目录前的物理路径边界。
/// </summary>
[Collection(GodotFileBridgeHostCollection.NAME)]
public sealed class GodotFileBridgePathSecurityTests
{
    /// <summary>
    /// 验证 godot-runtime engine 根是目录链接时，Host 构造不会接受包外协议根。
    /// </summary>
    [Fact]
    public void HostRejectsReparsePointEngineRoot()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        var outsideRoot = fixture.ProjectRoot + "-outside-engine";
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.EngineRoot)!);
        Directory.CreateDirectory(outsideRoot);
        try
        {
            if (!TryCreateDirectoryLink(fixture.EngineRoot, outsideRoot))
            {
                return;
            }

            var exception = Assert.Throws<IOException>(
                () => new GodotFileBridgeHost(fixture.ProjectRoot, "4.7.0"));

            Assert.Contains("symbolic link or junction", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(fixture.EngineRoot))
            {
                Directory.Delete(fixture.EngineRoot);
            }

            if (Directory.Exists(outsideRoot))
            {
                Directory.Delete(outsideRoot, recursive: true);
            }
        }
    }

    /// <summary>尝试创建目录符号链接；当前宿主不支持或权限不足时跳过专项断言。</summary>
    private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
                                          or PlatformNotSupportedException
                                          or NotSupportedException
                                          or IOException)
        {
            return false;
        }
    }
}
