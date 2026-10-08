using System.Diagnostics;
using System.Text.Json.Nodes;

namespace YokiFrame.Cli.Tests;

/// <summary>覆盖 script 在接触宿主前的输入拒绝契约。</summary>
public sealed class CliScriptCommandsTests
{
    /// <summary>验证未确认、空输入或同时指定 file 与 stdin 时，CLI 以对应错误码拒绝且不依赖宿主。</summary>
    /// <param name="options">附加 CLI 选项。</param>
    /// <param name="input">写入 stdin 的脚本正文。</param>
    /// <param name="code">预期错误码。</param>
    [Theory]
    [InlineData("", "engine.ConsoleLog(\"x\");", "ScriptConfirmationRequired")]
    [InlineData("--confirm-execution", "", "ScriptInputLimit")]
    [InlineData("--confirm-execution --file task.csx --stdin", "x", "ScriptInputInvalid")]
    public async Task Invalid_input_is_rejected_without_a_host(string options, string input, string code)
    {
        var result = await Execute(input, options.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        AssertRejected(result, code);
    }

    /// <summary>验证超过 128 KiB 的输入在派发前被拒绝。</summary>
    [Fact]
    public async Task Oversized_input_is_bounded_before_dispatch()
    {
        var result = await Execute(new string('x', 128 * 1024 + 1), "--confirm-execution");
        AssertRejected(result, "ScriptInputLimit");
    }

    /// <summary>验证预算按 UTF-8 字节计算，多字节字符会先于等长字符数触发上限。</summary>
    [Fact]
    public async Task Utf8_byte_budget_is_not_a_character_budget()
    {
        var result = await Execute(new string('\u4e00', 50000), "--confirm-execution");
        AssertRejected(result, "ScriptInputLimit");
    }

    /// <summary>断言进程以失败退出，且 stderr 的错误码字段等于预期值。</summary>
    /// <param name="result">CLI 进程结果。</param>
    /// <param name="code">预期错误码。</param>
    private static void AssertRejected((int ExitCode, string Output, string Error) result, string code)
    {
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Output.Trim());
        var json = JsonNode.Parse(result.Error)
            ?? throw new InvalidOperationException("CLI stderr is not JSON.");
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal(code, json["error"]!["code"]!.GetValue<string>());
    }

    /// <summary>启动 script 命令并把 stdin 交给 CLI。</summary>
    /// <param name="input">脚本正文。</param>
    /// <param name="options">确认与输入来源选项。</param>
    /// <returns>退出码、标准输出和标准错误。</returns>
    private static async Task<(int ExitCode, string Output, string Error)> Execute(string input, params string[] options)
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-script-cli", Guid.NewGuid().ToString("N"));
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add(CliTestHelpers.GetCliAssemblyPath());
        foreach (string argument in new[] { "script", "--target", "editor", "--project", root })
            info.ArgumentList.Add(argument);
        foreach (string argument in options) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
            await process.WaitForExitAsync();
            return (process.ExitCode, await output, await error);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
