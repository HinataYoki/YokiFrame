using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests.Godot;

/// <summary>
/// 锁定 Godot UID 的 FNV-1a 64、63 位资源 ID 和 Godot 字母表编码事实。
/// </summary>
public sealed class GodotUidGeneratorTests
{
    /// <summary>
    /// 验证固定资源路径生成旧版同算法 UID，并且路径大小写不会改变结果。
    /// </summary>
    [Fact]
    public void GenerateUsesStableFnv1AAndIgnoresResourcePathCasing()
    {
        GodotUidGenerator generator = new();

        var lower = generator.Generate(
            "res://addons/yokiframe/package/YokiFrame/Core/marker.cs");
        var upper = generator.Generate(
            "RES://ADDONS/YOKIFRAME/PACKAGE/YOKIFRAME/Core/marker.cs");

        Assert.Equal("uid://c5vngeeb4aq1w", lower);
        Assert.Equal(lower, upper);
        Assert.True(generator.IsValid(lower));
    }

    /// <summary>
    /// 验证 UID 正文只接受 Godot 的 a-y 与 0-8 字母表，并允许文件末尾换行。
    /// </summary>
    [Theory]
    [InlineData("uid://abc123\n", true)]
    [InlineData("uid://y08", true)]
    [InlineData("uid://", false)]
    [InlineData("uid://contains-z", false)]
    [InlineData("abc123", false)]
    public void IsValidEnforcesGodotResourceUidText(string value, bool expected)
    {
        Assert.Equal(expected, new GodotUidGenerator().IsValid(value));
    }
}
