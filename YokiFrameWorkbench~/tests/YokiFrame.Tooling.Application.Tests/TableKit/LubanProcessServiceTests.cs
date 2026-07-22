using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.TableKit;

namespace YokiFrame.Tooling.Application.Tests.TableKit;

/// <summary>验证 TableKit 额外输出会生成完整且隔离的 Luban 参数。</summary>
public sealed class LubanProcessServiceTests
{
    /// <summary>主 C# 代码输出必须进入 TableKit/Luban，避免 Luban 清空用户扩展目录。</summary>
    [Fact]
    public void MainOutputUsesIsolatedLubanSubdirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-main-output-" + Guid.NewGuid().ToString("N"));
        TableKitOptions options = new()
        {
            ProjectRoot = root,
            LubanConfigPath = Path.Combine(root, "Luban", "luban.conf"),
            CodeTarget = "cs-bin",
            DataTarget = "bin"
        };
        TableKitContract contract = CreateContract(root);

        string[] arguments = LubanProcessService.BuildMainArguments(options, contract).ToArray();

        Assert.Contains(
            "cs-bin.outputCodeDir=" + Path.Combine(contract.OutputCodeDirectory, "Luban"),
            arguments);
        Assert.DoesNotContain(
            "cs-bin.outputCodeDir=" + contract.OutputCodeDirectory,
            arguments);
    }

    /// <summary>额外目标同时携带 target、代码 target、数据 target及各自输出目录。</summary>
    [Fact]
    public void ExtraOutputBuildsCodeAndDataArguments()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-args");
        TableKitOptions options = new()
        {
            ProjectRoot = root,
            LubanConfigPath = Path.Combine(root, "Luban", "luban.conf")
        };
        TableKitContract contract = CreateContract(root);
        TableKitExtraOutput extra = new()
        {
            TargetName = "server",
            CodeTarget = "java-json",
            DataTarget = "json",
            OutputDataDir = "Temp/Server/Data",
            OutputCodeDir = "Temp/Server/Code"
        };

        string[] arguments = LubanProcessService.BuildExtraArguments(options, contract, extra).ToArray();

        AssertArgumentsContain(arguments, "-t", "server");
        AssertArgumentsContain(arguments, "-d", "json");
        AssertArgumentsContain(arguments, "-c", "java-json");
        Assert.Contains("json.outputDataDir=" + Path.Combine(root, "Temp", "Server", "Data"), arguments);
        Assert.Contains("java-json.outputCodeDir=" + Path.Combine(root, "Temp", "Server", "Code"), arguments);
    }

    /// <summary>自定义 Luban 工作目录时仍传递绝对配置路径，避免只传文件名导致找不到 luban.conf。</summary>
    [Fact]
    public void MainOutputUsesAbsoluteConfigPath()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-config-path-" + Guid.NewGuid().ToString("N"));
        string configPath = Path.Combine(root, "Luban", "luban.conf");
        TableKitOptions options = new()
        {
            ProjectRoot = root,
            LubanConfigPath = configPath,
            LubanWorkDir = Path.Combine(root, "Tools", "Luban")
        };

        string[] arguments = LubanProcessService.BuildMainArguments(options, CreateContract(root)).ToArray();

        AssertArgumentsContain(arguments, "--conf", Path.GetFullPath(configPath));
    }

    /// <summary>创建满足参数构建所需的最小主契约。</summary>
    /// <param name="root">测试项目根。</param>
    /// <returns>主输出契约。</returns>
    private static TableKitContract CreateContract(string root)
    {
        return new TableKitContract
        {
            ConfigPath = Path.Combine(root, "Luban", "luban.conf"),
            TargetName = "client",
            TopModule = "Game.Config",
            Manager = "Tables",
            CodeTarget = "cs-bin",
            DataTarget = "bin",
            DataExtension = "bytes",
            OutputCodeDirectory = Path.Combine(root, "Assets", "Scripts", "TableKit"),
            OutputDataDirectory = Path.Combine(root, "Assets", "Resources", "Table")
        };
    }

    /// <summary>断言参数开关后紧跟指定值。</summary>
    /// <param name="arguments">完整参数列表。</param>
    /// <param name="option">待定位参数开关。</param>
    /// <param name="expectedValue">开关后的期望值。</param>
    private static void AssertArgumentsContain(IReadOnlyList<string> arguments, string option, string expectedValue)
    {
        int index = Array.IndexOf(arguments.ToArray(), option);
        Assert.True(index >= 0 && index + 1 < arguments.Count);
        Assert.Equal(expectedValue, arguments[index + 1]);
    }
}
