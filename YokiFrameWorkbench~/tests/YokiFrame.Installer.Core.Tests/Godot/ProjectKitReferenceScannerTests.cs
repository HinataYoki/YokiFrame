using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests.Godot;

/// <summary>
/// 锁定 Godot 安装前的 Kit 引用扫描只认可发布投影可包含的 Runtime 实现。
/// </summary>
public sealed class ProjectKitReferenceScannerTests
{
    /// <summary>
    /// 验证 Unity 专属、Editor、测试和波浪号工具目录中的 UIKit 源码不能让 Godot 误判为可用。
    /// </summary>
    /// <param name="implementationPath">唯一 UIKit 实现文件在源包内的相对路径。</param>
    [Theory]
    [InlineData("Tools/UIKit/Adapters/Unity/Runtime/UIKit.cs")]
    [InlineData("Tools/UIKit/Integrations/Unity/DOTween/Runtime/UIKit.cs")]
    [InlineData("Tools/UIKit/Editor/UIKit.cs")]
    [InlineData("Tools/UIKit/Tests/UIKit.cs")]
    [InlineData("Tools/UIKit/Samples~/UIKit.cs")]
    public void ScanRejectsNonProjectableUIKitImplementation(string implementationPath)
    {
        using ScannerFixture fixture = ScannerFixture.Create();
        fixture.WriteUIKitReference();
        fixture.WriteSourceFile(implementationPath);

        var conflict = Assert.Single(new ProjectKitReferenceScanner().Scan(
            fixture.ProjectRoot,
            fixture.SourcePackageRoot));

        Assert.Equal("UIKit", conflict.KitName);
        Assert.Equal("UIKit", conflict.Identifier);
        Assert.Equal("Scripts/UIKitSmoke.cs", conflict.ProjectRelativePath);
    }

    /// <summary>
    /// 验证源包存在可投影的 UIKit Runtime 实现时，Godot 用户脚本不再产生缺失 Kit 冲突。
    /// </summary>
    [Fact]
    public void ScanAcceptsProjectableUIKitRuntimeImplementation()
    {
        using ScannerFixture fixture = ScannerFixture.Create();
        fixture.WriteUIKitReference();
        fixture.WriteSourceFile("Tools/UIKit/Runtime/UIKit.cs");

        var conflicts = new ProjectKitReferenceScanner().Scan(
            fixture.ProjectRoot,
            fixture.SourcePackageRoot);

        Assert.Empty(conflicts);
    }

    /// <summary>
    /// 验证不经过 UIKit 门面的旧 Panel 契约同样会阻止 Godot takeover 静默产生编译失败。
    /// </summary>
    /// <param name="identifier">旧版 UIKit 暴露的公共类型标识符。</param>
    [Theory]
    [InlineData("UIPanel")]
    [InlineData("UILevel")]
    [InlineData("UIRoot")]
    [InlineData("IUIData")]
    [InlineData("IPanel")]
    public void ScanRecognizesLegacyUIKitContractIdentifiers(string identifier)
    {
        using ScannerFixture fixture = ScannerFixture.Create();
        fixture.WriteUIKitIdentifierReference(identifier, useAlias: false);

        var conflict = Assert.Single(new ProjectKitReferenceScanner().Scan(
            fixture.ProjectRoot,
            fixture.SourcePackageRoot));

        Assert.Equal("UIKit", conflict.KitName);
        Assert.Equal(identifier, conflict.Identifier);
    }

    /// <summary>
    /// 验证 namespace alias 仍被识别为明确的 YokiFrame 代码引用。
    /// </summary>
    [Fact]
    public void ScanRecognizesYokiFrameNamespaceAlias()
    {
        using ScannerFixture fixture = ScannerFixture.Create();
        fixture.WriteUIKitIdentifierReference("IUIData", useAlias: true);

        var conflict = Assert.Single(new ProjectKitReferenceScanner().Scan(
            fixture.ProjectRoot,
            fixture.SourcePackageRoot));

        Assert.Equal("UIKit", conflict.KitName);
        Assert.Equal("IUIData", conflict.Identifier);
    }

    /// <summary>
    /// 为扫描器测试提供仅含当前用例文件的隔离项目和源包目录。
    /// </summary>
    private sealed class ScannerFixture : IDisposable
    {
        /// <summary>
        /// 创建临时目录并建立空的 Godot 用户项目与 YokiFrame 源包根。
        /// </summary>
        private ScannerFixture()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "yokiframe-kit-reference-tests",
                Guid.NewGuid().ToString("N"));
            ProjectRoot = Path.Combine(Root, "project");
            SourcePackageRoot = Path.Combine(Root, "source", "YokiFrame");
            Directory.CreateDirectory(ProjectRoot);
            Directory.CreateDirectory(SourcePackageRoot);
        }

        /// <summary>获取测试临时根目录。</summary>
        private string Root { get; }

        /// <summary>获取模拟 Godot 用户项目根。</summary>
        internal string ProjectRoot { get; }

        /// <summary>获取仅由当前用例填充的 YokiFrame 源包根。</summary>
        internal string SourcePackageRoot { get; }

        /// <summary>
        /// 创建新的隔离扫描 fixture。
        /// </summary>
        /// <returns>已建立空目录结构的 fixture。</returns>
        internal static ScannerFixture Create()
        {
            return new ScannerFixture();
        }

        /// <summary>
        /// 写入明确引用 YokiFrame UIKit 的 Godot 用户脚本。
        /// </summary>
        internal void WriteUIKitReference()
        {
            WriteText(
                Path.Combine(ProjectRoot, "Scripts", "UIKitSmoke.cs"),
                "using YokiFrame;\npublic sealed class UIKitSmoke { public void Run() { UIKit.CloseAllPanel(); } }\n");
        }

        /// <summary>
        /// 写入直接使用旧 UIKit 契约类型的脚本，可选择普通 using 或 namespace alias。
        /// </summary>
        /// <param name="identifier">需要命中的旧公共类型标识符。</param>
        /// <param name="useAlias">是否使用 using YF = YokiFrame。</param>
        internal void WriteUIKitIdentifierReference(string identifier, bool useAlias)
        {
            string usingLine = useAlias ? "using YF = YokiFrame;" : "using YokiFrame;";
            string typeName = useAlias ? "YF." + identifier : identifier;
            WriteText(
                Path.Combine(ProjectRoot, "Scripts", "UIKitContractSmoke.cs"),
                usingLine + "\npublic sealed class UIKitContractSmoke { public " + typeName + " Value { get; } }\n");
        }

        /// <summary>
        /// 在源包写入当前用例唯一的 UIKit C# 实现文件。
        /// </summary>
        /// <param name="relativePath">使用正斜杠的源包相对路径。</param>
        internal void WriteSourceFile(string relativePath)
        {
            WriteText(
                Path.Combine(SourcePackageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)),
                "namespace YokiFrame; public static class UIKit { }\n");
        }

        /// <summary>
        /// 删除当前用例创建的全部临时文件。
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        /// <summary>
        /// 写入测试文件并确保父目录存在。
        /// </summary>
        /// <param name="path">目标文件绝对路径。</param>
        /// <param name="content">完整文件内容。</param>
        private static void WriteText(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
