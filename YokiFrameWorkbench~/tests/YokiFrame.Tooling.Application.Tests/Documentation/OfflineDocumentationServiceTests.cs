using System.Diagnostics;
using YokiFrame.Tooling.Application.Documentation;

namespace YokiFrame.Tooling.Application.Tests.Documentation;

/// <summary>
/// 覆盖离线文档目录、Markdown 读取、关键词搜索和 API 索引扩展入口。
/// </summary>
public sealed class OfflineDocumentationServiceTests
{
    /// <summary>
    /// 验证用户目录只收录受控公开 Markdown，不暴露内部架构资料和 Workbench 开发文档。
    /// </summary>
    [Fact]
    public void GetIndexReadsOnlyWorkbenchPublicDocumentsAndPackageVersion()
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WritePackageDocument("Api/02-Core/FsmKit.md", "# FsmKit 指南\n");
        fixture.WritePackageDocument("Guides/AI-Install.md", "# AI 安装\n");
        fixture.WritePackageDocument("Architecture_Internal.md", "# 内部架构\n");
        fixture.WriteWorkbenchDocument("operations/bridge.md", "# Bridge 运维\n");
        fixture.WriteOutsideDocument("private.md", "# 不应收录\n");
        var service = fixture.CreateService();

        var index = service.GetIndex();

        Assert.Equal("2.3.4-preview", index.PackageVersion);
        Assert.Equal(2, index.Documents.Count);
        Assert.Contains(index.Documents, static item => item.RelativePath == "Documentation~/Api/02-Core/FsmKit.md");
        Assert.Contains(index.Documents, static item => item.RelativePath == "Documentation~/Guides/AI-Install.md");
        Assert.DoesNotContain(index.Documents, static item => item.RelativePath.Contains("Architecture_Internal", StringComparison.Ordinal));
        Assert.DoesNotContain(index.Documents, static item => item.SourceKind == DocumentationSourceKind.WorkbenchDocumentation);
        Assert.DoesNotContain(index.Documents, static item => item.RelativePath.Contains("private.md", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证唯一框架概览进入 Workbench 导航，旧 GettingStarted 辅助页和 Guides 仍可读取但不展示；Kit 续篇仍拼接到主页面。
    /// </summary>
    [Fact]
    public void GetIndexKeepsGettingStartedLimitedAndMergesKitContinuations()
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WritePackageDocument("Api/00-GettingStarted/FrameworkOverview.md", "# 框架概览\n");
        fixture.WritePackageDocument("Api/00-GettingStarted/Entrypoints.md", "# 旧入口\n");
        fixture.WritePackageDocument("Api/00-GettingStarted/Kit_Status.md", "# 状态与入口\n");
        fixture.WritePackageDocument("Api/01-Architecture/Architecture.md", "# Architecture\n");
        fixture.WritePackageDocument("Api/02-Core/FsmKit.md", "# FsmKit\n");
        fixture.WritePackageDocument("Api/02-Core/FsmKit.part.md", "## 实践与诊断\n");
        fixture.WritePackageDocument("Api/03-Tool/ActionKit.md", "# ActionKit\n");
        fixture.WritePackageDocument("Api/03-Tool/ActionKit.part.md", "## 进阶示例\n");
        fixture.WritePackageDocument("Api/03-Tool/UIKit.md", "# UIKit\n");
        fixture.WritePackageDocument("Api/03-Tool/UIKit.part.md", "## 扩展边界\n");
        fixture.WritePackageDocument("Api/04-Reference/ThirdParty.md", "# 第三方库索引\n");
        fixture.WritePackageDocument("Guides/AI-Install.md", "# AI 安装\n");

        var service = fixture.CreateService();
        var documents = service.GetIndex().Documents;

        var gettingStarted = documents
            .Where(static item => item.RelativePath.Contains("/Api/00-GettingStarted/", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(3, gettingStarted.Length);
        Assert.True(
            Assert.Single(
                gettingStarted,
                static item => item.RelativePath.EndsWith("/FrameworkOverview.md", StringComparison.OrdinalIgnoreCase))
                .IsNavigationVisible);
        Assert.All(
            gettingStarted.Where(static item => item.RelativePath.EndsWith("/Entrypoints.md", StringComparison.OrdinalIgnoreCase)
                                                || item.RelativePath.EndsWith("/Kit_Status.md", StringComparison.OrdinalIgnoreCase)),
            static item => Assert.False(item.IsNavigationVisible));
        Assert.Equal("入门", Assert.Single(gettingStarted, static item => item.IsNavigationVisible).Group);
        Assert.Equal(
            "Documentation~/Api/00-GettingStarted/FrameworkOverview.md",
            service.GetIndex().NavigationDocuments[0].RelativePath);
        Assert.DoesNotContain(documents, static item => item.RelativePath.EndsWith(".part.md", StringComparison.OrdinalIgnoreCase));
        Assert.False(Assert.Single(documents, static item => item.RelativePath.EndsWith("/Guides/AI-Install.md")).IsNavigationVisible);
        Assert.Contains("## 实践与诊断", service.ReadDocument("Documentation~/Api/02-Core/FsmKit.md").Markdown, StringComparison.Ordinal);
        Assert.Contains("## 进阶示例", service.ReadDocument("Documentation~/Api/03-Tool/ActionKit.md").Markdown, StringComparison.Ordinal);
        Assert.Contains("## 扩展边界", service.ReadDocument("Documentation~/Api/03-Tool/UIKit.md").Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证超长 Kit 文档的续篇会拼接到正文，但不会作为第二个用户目录条目出现。
    /// </summary>
    [Fact]
    public void GetIndexMergesContinuationWithoutDuplicateNavigationEntry()
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WritePackageDocument("Api/03-Tool/ActionKit.md", "# ActionKit 动作\n\n## 基础动作\n");
        fixture.WritePackageDocument("Api/03-Tool/ActionKit.part.md", "## 进阶动作\n");
        var service = fixture.CreateService();

        var entry = Assert.Single(service.GetIndex().Documents);
        var document = service.ReadDocument(entry.RelativePath);

        Assert.Equal("ActionKit 动作", entry.Title);
        Assert.Contains("## 基础动作", document.Markdown, StringComparison.Ordinal);
        Assert.Contains("## 进阶动作", document.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Markdown 读取结果包含标题、正文、稳定目录锚点和可直接复制的代码文本。
    /// </summary>
    [Fact]
    public void ReadDocumentReturnsHeadingsAndCopyReadyCodeBlocks()
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WritePackageDocument(
            "Guides/Markdown.md",
            "# Markdown 指南\n\n## 快速开始\n\n```csharp\nvar fsm = new FSM<State>();\nfsm.Change(State.Run);\n```\n\n## 快速开始\n");
        var service = fixture.CreateService();

        var document = service.ReadDocument("Documentation~/Guides/Markdown.md");

        Assert.Equal("Markdown 指南", document.Title);
        Assert.Equal("Documentation~/Guides/Markdown.md", document.RelativePath);
        Assert.Contains("fsm.Change", document.Markdown, StringComparison.Ordinal);
        Assert.Collection(
            document.Headings,
            heading => Assert.Equal((1, "Markdown 指南", "markdown-指南"), (heading.Level, heading.Title, heading.Anchor)),
            heading => Assert.Equal((2, "快速开始", "快速开始"), (heading.Level, heading.Title, heading.Anchor)),
            heading => Assert.Equal((2, "快速开始", "快速开始-1"), (heading.Level, heading.Title, heading.Anchor)));
        var codeBlock = Assert.Single(document.CodeBlocks);
        Assert.Equal("csharp", codeBlock.Language);
        Assert.Equal("var fsm = new FSM<State>();\nfsm.Change(State.Run);", codeBlock.CopyText);
        Assert.Equal(codeBlock.Code, codeBlock.CopyText);
        Assert.Contains(document.Blocks, static block => block is DocumentationHeadingBlock heading && heading.Title == "快速开始");
        Assert.Contains(document.Blocks, static block => block is DocumentationCodeBlockBlock);
    }

    /// <summary>
    /// 验证内部架构资料与 Workbench 源码侧资料不能通过手工相对路径绕过公开目录。
    /// </summary>
    [Fact]
    public void ReadDocumentRejectsInternalPackageAndWorkbenchSourceDocuments()
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WritePackageDocument("Architecture_Internal.md", "# 内部架构\n");
        fixture.WriteWorkbenchDocument("operations/bridge.md", "# Bridge 运维\n");
        var service = fixture.CreateService();

        Assert.Throws<UnauthorizedAccessException>(
            () => service.ReadDocument("Documentation~/Architecture_Internal.md"));
        Assert.Throws<UnauthorizedAccessException>(
            () => service.ReadDocument("YokiFrameWorkbench~/docs/operations/bridge.md"));
    }

    /// <summary>
    /// 验证表格、列表和普通段落会转换为 Workbench 可直接绑定的正文块。
    /// </summary>
    [Fact]
    public void ReadDocumentProjectsTablesListsAndParagraphs()
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WritePackageDocument(
            "Guides/Layout.md",
            "# Layout\n\n说明文本。\n\n- 第一项\n- 第二项\n\n| 部分 | 作用 |\n| --- | --- |\n| Core | API |\n");
        var document = fixture.CreateService().ReadDocument("Documentation~/Guides/Layout.md");

        Assert.Contains(document.Blocks, static block => block is DocumentationParagraphBlock paragraph && paragraph.Text == "说明文本。");
        Assert.Contains(document.Blocks, static block => block is DocumentationParagraphBlock paragraph && paragraph.IsListItem && paragraph.Text == "第一项");
        var table = Assert.Single(document.Blocks.OfType<DocumentationTableBlock>());
        Assert.Equal(new[] { "部分", "作用" }, table.Rows[0].Cells);
        Assert.Equal(new[] { "Core", "API" }, table.Rows[1].Cells);
    }

    /// <summary>
    /// 验证文档读取拒绝通过父目录跳出受控根。
    /// </summary>
    [Fact]
    public void ReadDocumentRejectsParentDirectoryEscape()
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WriteOutsideDocument("secret.md", "secret");
        var service = fixture.CreateService();

        Assert.Throws<UnauthorizedAccessException>(
            () => service.ReadDocument("Documentation~/../secret.md"));
    }

    /// <summary>
    /// 验证文档读取拒绝绝对路径，即使目标文件真实存在。
    /// </summary>
    [Fact]
    public void ReadDocumentRejectsAbsolutePath()
    {
        using var fixture = DocumentationFixture.Create();
        var outsidePath = fixture.WriteOutsideDocument("secret.md", "secret");
        var service = fixture.CreateService();

        Assert.Throws<UnauthorizedAccessException>(() => service.ReadDocument(outsidePath));
    }

    /// <summary>
    /// 验证 Documentation~ 根自身是符号链接或 Junction 时不能把索引重定向到受控根之外。
    /// </summary>
    [Fact]
    public void GetIndexRejectsReparsePointAtControlledRoot()
    {
        using var fixture = DocumentationFixture.Create();
        var outsideRoot = Path.Combine(fixture.PackageRoot, "private-documents");
        Directory.CreateDirectory(outsideRoot);
        File.WriteAllText(Path.Combine(outsideRoot, "secret.md"), "# secret");
        var documentationRoot = Path.Combine(fixture.PackageRoot, "Documentation~");
        CreateDirectoryLink(documentationRoot, outsideRoot);
        try
        {
            var service = fixture.CreateService();

            Assert.Throws<UnauthorizedAccessException>(() => service.GetIndex());
        }
        finally
        {
            DeleteDirectoryLink(documentationRoot);
        }
    }

    /// <summary>
    /// 验证受控根内部的目录链接不能把单文档读取重定向到外部目录。
    /// </summary>
    [Fact]
    public void ReadDocumentRejectsNestedReparsePoint()
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WritePackageDocument("Guides/Local.md", "# local");
        var outsideRoot = Path.Combine(fixture.PackageRoot, "private-documents");
        Directory.CreateDirectory(outsideRoot);
        File.WriteAllText(Path.Combine(outsideRoot, "secret.md"), "# secret");
        var linkPath = Path.Combine(fixture.PackageRoot, "Documentation~", "Guides", "Linked");
        CreateDirectoryLink(linkPath, outsideRoot);
        try
        {
            var service = fixture.CreateService();

            Assert.Throws<UnauthorizedAccessException>(
                () => service.ReadDocument("Documentation~/Guides/Linked/secret.md"));
        }
        finally
        {
            DeleteDirectoryLink(linkPath);
        }
    }

    /// <summary>
    /// 验证合法 C# 标题保留井号，而由空白分隔的尾部井号仍作为 closing hash 移除。
    /// </summary>
    [Fact]
    public void ReadDocumentPreservesCSharpHeadingAndRemovesClosingHashes()
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WritePackageDocument("Guides/CSharp.md", "# C#\n\n## 快速开始 ###\n");
        var service = fixture.CreateService();

        var document = service.ReadDocument("Documentation~/Guides/CSharp.md");

        Assert.Equal("C#", document.Title);
        Assert.Collection(
            document.Headings,
            heading => Assert.Equal((1, "C#"), (heading.Level, heading.Title)),
            heading => Assert.Equal((2, "快速开始"), (heading.Level, heading.Title)));
    }

    /// <summary>
    /// 验证无效 JSON 与非字符串 version 都转换为带 package.json 证据的 InvalidDataException。
    /// </summary>
    [Theory]
    [InlineData("{ invalid json")]
    [InlineData("{\"name\":\"com.hinatayoki.yokiframe\",\"version\":42}")]
    public void GetIndexNormalizesInvalidPackageMetadata(string packageJson)
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WritePackageJson(packageJson);
        var service = fixture.CreateService();

        var exception = Assert.Throws<InvalidDataException>(() => service.GetIndex());

        Assert.Contains("package.json", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证首次索引建立不可变搜索快照，只有显式 RefreshIndex 才重新读取文档。
    /// </summary>
    [Fact]
    public void SearchUsesCachedSnapshotUntilRefreshIndex()
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WritePackageDocument("Api/02-Core/Cache.md", "# Cache\n\nAlphaToken\n");
        var service = fixture.CreateService();
        service.GetIndex();
        fixture.WritePackageDocument("Api/02-Core/Cache.md", "# Cache\n\nBetaToken\n");

        Assert.Single(service.Search("AlphaToken"));
        Assert.Empty(service.Search("BetaToken"));

        service.RefreshIndex();

        Assert.Single(service.Search("BetaToken"));
        Assert.Empty(service.Search("AlphaToken"));
    }

    /// <summary>
    /// 验证文档关键词搜索能够识别 Kit、类型、方法和错误码语义。
    /// </summary>
    [Theory]
    [InlineData("FsmKit", DocumentationKeywordKind.Kit)]
    [InlineData("FSM", DocumentationKeywordKind.Type)]
    [InlineData("Change", DocumentationKeywordKind.Method)]
    [InlineData("UnknownKit", DocumentationKeywordKind.ErrorCode)]
    public void SearchClassifiesDocumentationKeywords(string keyword, DocumentationKeywordKind expectedKind)
    {
        using var fixture = DocumentationFixture.Create();
        fixture.WritePackageDocument(
            "Api/02-Core/FsmKit.md",
            "# FsmKit 指南\n\n使用 `FSM<PlayerState>`，调用 `Change(State.Run)`。错误码 `UnknownKit` 表示 Kit 未注册。\n");
        var service = fixture.CreateService();

        var results = service.Search(keyword);

        var result = Assert.Single(results, static item => item.ItemKind == DocumentationSearchItemKind.Document);
        Assert.Contains(expectedKind, result.MatchedKeywordKinds);
        Assert.Contains(keyword, result.Snippet, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证可插拔 API 索引条目参与搜索，但服务不负责生成 XML 文档数据。
    /// </summary>
    [Fact]
    public void SearchIncludesEntriesFromApiIndexSource()
    {
        using var fixture = DocumentationFixture.Create();
        fixture.ApiEntries.Add(new DocumentationApiIndexEntry(
            "FSM.Start",
            DocumentationKeywordKind.Method,
            "启动指定状态。",
            "FSM<TEnum>",
            "api/YokiFrame.xml"));
        var service = fixture.CreateService();

        var results = service.Search("Start");

        var result = Assert.Single(results, static item => item.ItemKind == DocumentationSearchItemKind.ApiSymbol);
        Assert.Equal("FSM.Start", result.Title);
        Assert.Contains(DocumentationKeywordKind.Method, result.MatchedKeywordKinds);
    }

    /// <summary>
    /// 提供位于临时目录的最小 YokiFrame 包根及两个受控文档根。
    /// </summary>
    private sealed class DocumentationFixture : IDisposable
    {
        /// <summary>
        /// 创建测试包根并写入当前包版本。
        /// </summary>
        private DocumentationFixture()
        {
            PackageRoot = Path.Combine(
                Path.GetTempPath(),
                "yokiframe-documentation-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(PackageRoot);
            File.WriteAllText(
                Path.Combine(PackageRoot, "package.json"),
                "{\"name\":\"com.hinatayoki.yokiframe\",\"version\":\"2.3.4-preview\"}");
        }

        /// <summary>
        /// 获取测试使用的 YokiFrame 包根。
        /// </summary>
        internal string PackageRoot { get; }

        /// <summary>
        /// 获取可插拔 API 索引测试数据。
        /// </summary>
        internal List<DocumentationApiIndexEntry> ApiEntries { get; } = new();

        /// <summary>
        /// 创建新的隔离测试包根。
        /// </summary>
        /// <returns>测试现场。</returns>
        internal static DocumentationFixture Create()
        {
            return new DocumentationFixture();
        }

        /// <summary>
        /// 创建以当前包根为输入的离线文档服务。
        /// </summary>
        /// <returns>待验证服务。</returns>
        internal OfflineDocumentationService CreateService()
        {
            return new OfflineDocumentationService(
                PackageRoot,
                new StubApiIndexSource(ApiEntries));
        }

        /// <summary>
        /// 覆盖 package.json，供错误元数据契约测试使用。
        /// </summary>
        /// <param name="content">新的 package.json 文本。</param>
        internal void WritePackageJson(string content)
        {
            File.WriteAllText(Path.Combine(PackageRoot, "package.json"), content);
        }

        /// <summary>
        /// 写入包级 Documentation~ 文档。
        /// </summary>
        /// <param name="relativePath">相对 Documentation~ 的路径。</param>
        /// <param name="markdown">Markdown 正文。</param>
        internal void WritePackageDocument(string relativePath, string markdown)
        {
            WriteText(Path.Combine(PackageRoot, "Documentation~", Normalize(relativePath)), markdown);
        }

        /// <summary>
        /// 写入 Workbench 源码侧 docs 文档。
        /// </summary>
        /// <param name="relativePath">相对 Workbench docs 的路径。</param>
        /// <param name="markdown">Markdown 正文。</param>
        internal void WriteWorkbenchDocument(string relativePath, string markdown)
        {
            WriteText(Path.Combine(PackageRoot, "YokiFrameWorkbench~", "docs", Normalize(relativePath)), markdown);
        }

        /// <summary>
        /// 在受控根之外写入文件，并返回绝对路径。
        /// </summary>
        /// <param name="relativePath">相对包根的路径。</param>
        /// <param name="content">文件正文。</param>
        /// <returns>已写入文件的绝对路径。</returns>
        internal string WriteOutsideDocument(string relativePath, string content)
        {
            var path = Path.Combine(PackageRoot, Normalize(relativePath));
            WriteText(path, content);
            return path;
        }

        /// <summary>
        /// 删除 fixture 创建的全部临时文件。
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(PackageRoot))
            {
                Directory.Delete(PackageRoot, recursive: true);
            }
        }

        /// <summary>
        /// 把测试相对路径转换为当前平台路径。
        /// </summary>
        /// <param name="relativePath">使用斜杠表达的相对路径。</param>
        /// <returns>当前平台相对路径。</returns>
        private static string Normalize(string relativePath)
        {
            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// 写入文本并自动创建父目录。
        /// </summary>
        /// <param name="path">目标路径。</param>
        /// <param name="content">正文。</param>
        private static void WriteText(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }

    /// <summary>
    /// 以测试内存条目证明 API 索引来源可替换，避免在本切片实现 XML 生成器。
    /// </summary>
    private sealed class StubApiIndexSource : IDocumentationApiIndexSource
    {
        private readonly IReadOnlyList<DocumentationApiIndexEntry> mEntries;

        /// <summary>
        /// 保存 API 索引条目快照。
        /// </summary>
        /// <param name="entries">待返回条目。</param>
        internal StubApiIndexSource(IReadOnlyList<DocumentationApiIndexEntry> entries)
        {
            mEntries = entries.ToArray();
        }

        /// <summary>
        /// 返回测试 API 索引条目。
        /// </summary>
        /// <returns>API 条目快照。</returns>
        public IReadOnlyList<DocumentationApiIndexEntry> ReadEntries()
        {
            return mEntries;
        }
    }

    /// <summary>
    /// 创建目录重解析点；Windows 符号链接无权限时回落到无需提权的 Junction。
    /// </summary>
    /// <param name="linkPath">待创建链接路径。</param>
    /// <param name="targetPath">链接目标目录。</param>
    private static void CreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (exception is PlatformNotSupportedException
                                          or UnauthorizedAccessException
                                          or IOException)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new InvalidOperationException("当前平台无法创建目录符号链接。", exception);
            }

            CreateWindowsJunction(linkPath, targetPath, exception);
        }
    }

    /// <summary>
    /// 通过 Windows 内置 mklink 创建 Junction，确保普通权限测试进程也能覆盖重解析点。
    /// </summary>
    /// <param name="linkPath">待创建 Junction 路径。</param>
    /// <param name="targetPath">Junction 目标目录。</param>
    /// <param name="symlinkException">符号链接创建失败的原始异常。</param>
    private static void CreateWindowsJunction(
        string linkPath,
        string targetPath,
        Exception symlinkException)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = System.Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(linkPath);
        startInfo.ArgumentList.Add(targetPath);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 mklink 创建测试 Junction。", symlinkException);
        process.WaitForExit();
        if (process.ExitCode != 0 || !Directory.Exists(linkPath))
        {
            throw new InvalidOperationException("无法创建测试 Junction。", symlinkException);
        }
    }

    /// <summary>
    /// 删除测试创建的目录链接，不递归触碰链接目标。
    /// </summary>
    /// <param name="linkPath">目录链接路径。</param>
    private static void DeleteDirectoryLink(string linkPath)
    {
        if (Directory.Exists(linkPath))
        {
            Directory.Delete(linkPath);
        }
    }
}
