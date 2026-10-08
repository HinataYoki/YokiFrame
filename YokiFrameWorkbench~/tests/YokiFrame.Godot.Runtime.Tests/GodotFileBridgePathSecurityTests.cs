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
    [SymbolicLinkFact(SymbolicLinkProbeKind.Directory)]
    public void HostRejectsReparsePointEngineRoot()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        var outsideRoot = fixture.ProjectRoot + "-outside-engine";
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.EngineRoot)!);
        Directory.CreateDirectory(outsideRoot);
        try
        {
            Directory.CreateSymbolicLink(fixture.EngineRoot, outsideRoot);

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

}
