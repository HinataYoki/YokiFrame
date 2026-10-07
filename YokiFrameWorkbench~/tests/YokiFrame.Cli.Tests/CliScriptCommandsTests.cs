using System.Diagnostics;

namespace YokiFrame.Cli.Tests;

public sealed class CliScriptCommandsTests
{
    [Theory]
    [InlineData("", "engine.ConsoleLog(\"x\");", "ScriptConfirmationRequired")]
    [InlineData("--confirm-execution", "", "ScriptInputLimit")]
    [InlineData("--confirm-execution --file task.csx --stdin", "x", "ScriptInputInvalid")]
    public async Task Invalid_input_is_rejected_without_a_host(string options, string input, string code)
    {
        var result = await Execute(input, options.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(1, result.ExitCode);
        Assert.Contains(code, result.Error);
    }

    [Fact]
    public async Task Oversized_input_is_bounded_before_dispatch()
    {
        var result = await Execute(new string('x', 128 * 1024 + 1), "--confirm-execution");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ScriptInputLimit", result.Error);
    }

    [Fact]
    public async Task Utf8_byte_budget_is_not_a_character_budget()
    {
        var result = await Execute(new string('\u4e00', 50000), "--confirm-execution");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ScriptInputLimit", result.Error);
    }

    private static async Task<(int ExitCode, string Error)> Execute(string input, params string[] options)
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
            await output;
            return (process.ExitCode, await error);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
