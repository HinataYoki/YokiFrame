using YokiFrame.Tooling.Application.Models.Luban;
using YokiFrame.Tooling.Application.Services.Luban;

namespace YokiFrame.Tooling.Application.Tests.Luban;

/// <summary>验证跨 Kit Luban 自动发现对标准分发包和多安装目录的处理。</summary>
public sealed class LubanProjectDiscoveryServiceTests
{
    /// <summary>标准 Luban 目录同时包含 DLL 与 EXE 时，应视为同一份工具并稳定优先 DLL。</summary>
    [Fact]
    public void DiscoverPrefersDllForStandardPairedDistribution()
    {
        using TemporaryLubanProject project = TemporaryLubanProject.Create();
        project.AddStandardToolPair();

        LubanToolDiscoveryResult result = new LubanProjectDiscoveryService().Discover(project.Root);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.Equal(Path.Combine(project.Root, "Luban", "Tools", "Luban", "Luban.dll"), result.Options?.LubanExecutablePath);
        Assert.Equal(string.Empty, result.Options?.LubanAgentExecutablePath);
        Assert.Equal(string.Empty, result.Options?.LubanMcpExecutablePath);
        Assert.Equal(string.Empty, result.Options?.LubanSkillsPath);
        Assert.Equal(string.Empty, result.Options?.LubanDocumentationPath);
    }

    /// <summary>新版伴随工具存在时，应附加到发现结果但不改变主工具入口。</summary>
    [Fact]
    public void DiscoverFindsOptionalAiCompanionTools()
    {
        using TemporaryLubanProject project = TemporaryLubanProject.Create();
        project.AddStandardToolPair();
        project.AddCompanionTool("Luban.Agent", "Luban.Agent.dll");
        project.AddCompanionTool("Luban.Mcp", "Luban.Mcp.dll");
        project.AddDocumentationDirectory();
        project.AddSkillsDirectory();

        LubanToolDiscoveryResult result = new LubanProjectDiscoveryService().Discover(project.Root);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.Equal(
            Path.Combine(project.Root, "Luban", "Tools", "Luban.Agent", "Luban.Agent.dll"),
            result.Options?.LubanAgentExecutablePath);
        Assert.Equal(
            Path.Combine(project.Root, "Luban", "Tools", "Luban.Mcp", "Luban.Mcp.dll"),
            result.Options?.LubanMcpExecutablePath);
        Assert.Equal(
            Path.Combine(project.Root, "Luban", "Tools", "Luban.Skill", "skills"),
            result.Options?.LubanSkillsPath);
        Assert.Equal(
            Path.Combine(project.Root, "Luban", "Tools", "Luban.Skill", "skills"),
            LubanProjectDiscoveryService.ResolveOfficialSkillsRoot(
                Path.Combine(project.Root, "Luban", "Tools", "Luban.Skill")));
        Assert.Equal(
            Path.Combine(project.Root, "luban-doc", "docs"),
            result.Options?.LubanDocumentationPath);
    }

    /// <summary>可选工具存在多个候选时不得静默猜测，主 Luban 仍必须保持可用。</summary>
    [Fact]
    public void DiscoverIgnoresAmbiguousOptionalCompanionTools()
    {
        using TemporaryLubanProject project = TemporaryLubanProject.Create();
        project.AddStandardToolPair();
        project.AddCompanionTool("Luban.Agent", "Luban.Agent.dll");
        project.AddProjectCompanionTool("Luban.Agent", "Luban.Agent.dll");

        LubanToolDiscoveryResult result = new LubanProjectDiscoveryService().Discover(project.Root);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.Equal(Path.Combine(project.Root, "Luban", "Tools", "Luban", "Luban.dll"), result.Options?.LubanExecutablePath);
        Assert.Equal(string.Empty, result.Options?.LubanAgentExecutablePath);
    }

    /// <summary>不同目录中的多份 Luban 工具仍必须拒绝自动猜测，避免静默使用错误版本。</summary>
    [Fact]
    public void DiscoverRejectsSeparateToolInstallations()
    {
        using TemporaryLubanProject project = TemporaryLubanProject.Create();
        project.AddTool("Luban", "Luban.dll");
        project.AddProjectTool("Luban.dll");

        LubanToolDiscoveryResult result = new LubanProjectDiscoveryService().Discover(project.Root);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Contains("多个 Luban 工具", StringComparison.Ordinal));
    }

    /// <summary>显式工作目录应覆盖自动扫描中的其它配置文件，并使用该目录内的 Luban 入口。</summary>
    [Fact]
    public void DiscoverUsesConfiguredWorkDirectory()
    {
        using TemporaryLubanProject project = TemporaryLubanProject.Create();
        string workDirectory = Path.Combine(project.Root, "Luban", "CustomTemplate");
        Directory.CreateDirectory(workDirectory);
        File.WriteAllText(
            Path.Combine(workDirectory, "luban.conf"),
            "{\"dataDir\":\"Datas\",\"targets\":[{\"name\":\"client\"}]}" );
        File.WriteAllText(Path.Combine(workDirectory, "Luban.dll"), string.Empty);

        LubanToolDiscoveryResult result = new LubanProjectDiscoveryService()
            .Discover(project.Root, Path.Combine("Luban", "CustomTemplate"));

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.Equal(Path.Combine(workDirectory, "luban.conf"), result.Options?.LubanConfigPath);
        Assert.Equal(workDirectory, result.Options?.LubanWorkDir);
        Assert.Equal(Path.Combine(workDirectory, "Luban.dll"), result.Options?.LubanExecutablePath);
    }

    /// <summary>创建包含单一配置文件的临时 Luban 项目，避免测试依赖当前工作区文件。</summary>
    private sealed class TemporaryLubanProject : IDisposable
    {
        /// <summary>创建绑定指定根目录的临时项目实例。</summary>
        /// <param name="root">临时项目绝对路径。</param>
        private TemporaryLubanProject(string root) => Root = root;

        /// <summary>临时项目根目录。</summary>
        public string Root { get; }

        /// <summary>创建带最小 luban.conf 的临时项目。</summary>
        /// <returns>可由自动发现服务扫描的项目。</returns>
        public static TemporaryLubanProject Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "yokiframe-luban-discovery-" + Guid.NewGuid().ToString("N"));
            string workDirectory = Path.Combine(root, "Luban", "MiniTemplate");
            Directory.CreateDirectory(workDirectory);
            File.WriteAllText(
                Path.Combine(workDirectory, "luban.conf"),
                "{\"dataDir\":\"Datas\",\"targets\":[{\"name\":\"client\"}]}" );
            return new TemporaryLubanProject(root);
        }

        /// <summary>在标准工具目录创建 DLL 和 EXE 配对入口。</summary>
        public void AddStandardToolPair()
        {
            AddTool("Luban", "Luban.dll");
            AddTool("Luban", "Luban.exe");
        }

        /// <summary>在测试项目的 Luban 工具根创建一个空入口文件。</summary>
        /// <param name="directoryName">工具目录名。</param>
        /// <param name="fileName">入口文件名。</param>
        public void AddTool(string directoryName, string fileName)
        {
            string directory = Path.Combine(Root, "Luban", "Tools", directoryName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), string.Empty);
        }

        /// <summary>在标准 Luban Tools 目录创建一个可选伴随工具入口。</summary>
        /// <param name="directoryName">伴随工具目录名。</param>
        /// <param name="fileName">工具入口文件名。</param>
        public void AddCompanionTool(string directoryName, string fileName)
        {
            AddTool(directoryName, fileName);
        }

        /// <summary>在项目根级 Tools 目录创建一个可选伴随工具入口，用于构造发现歧义。</summary>
        /// <param name="directoryName">伴随工具目录名。</param>
        /// <param name="fileName">工具入口文件名。</param>
        public void AddProjectCompanionTool(string directoryName, string fileName)
        {
            string directory = Path.Combine(Root, "Tools", directoryName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), string.Empty);
        }

        /// <summary>创建项目根级 Luban 文档目录。</summary>
        public void AddDocumentationDirectory()
        {
            Directory.CreateDirectory(Path.Combine(Root, "luban-doc", "docs"));
        }

        /// <summary>创建项目根级 Luban 官方 Skills 源目录。</summary>
        public void AddSkillsDirectory()
        {
            string skillsRoot = Path.Combine(Root, "Luban", "Tools", "Luban.Skill", "skills");
            string skillDirectory = Path.Combine(skillsRoot, "luban-validator");
            Directory.CreateDirectory(skillDirectory);
            File.WriteAllText(Path.Combine(skillDirectory, "SKILL.md"), "---\nname: luban-validator\n---\n");
        }

        /// <summary>在项目根级候选工具目录创建一个空入口文件。</summary>
        /// <param name="fileName">入口文件名。</param>
        public void AddProjectTool(string fileName)
        {
            string directory = Path.Combine(Root, "Tools", "Luban");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), string.Empty);
        }

        /// <summary>删除临时项目及其测试产生的所有文件。</summary>
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
    }
}
