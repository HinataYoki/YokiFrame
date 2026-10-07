using System.Reflection;
using YokiFrame.RoslynKit.Compiler;

namespace YokiFrame.RoslynKit.Compiler.Tests;

public sealed class MemoryCompilerTests
{
    private static readonly string[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(LiveService).Assembly.Location).Distinct().ToArray();

    [Fact]
    public async Task Compiled_assemblies_call_the_same_live_service_without_replacing_it()
    {
        var service = new LiveService();
        var identities = new List<string>();
        foreach (int amount in new[] { 35, 7 })
        {
            string source = "public static class Script { public static async System.Threading.Tasks.Task Run("
                + "YokiFrame.RoslynKit.Compiler.Tests.LiveService service) { await System.Threading.Tasks.Task.Yield();"
                + "service.Value -= " + amount + "; } }";
            byte[] pe = MemoryCompiler.Compile(source, References, default, out byte[] pdb, out string[][] diagnostics);
            Assert.NotNull(pe);
            Assert.NotEmpty(pdb);
            Assert.DoesNotContain(diagnostics, d => d[1] == "Error");
            Assembly assembly = Assembly.Load(pe, pdb);
            Assert.Empty(assembly.Location);
            identities.Add(assembly.FullName!);
            await (Task)assembly.GetType("Script")!.GetMethod("Run")!.Invoke(null, new object[] { service })!;
        }
        Assert.Equal(78, service.Value);
        Assert.NotEqual(identities[0], identities[1]);
    }

    [Fact]
    public void Invalid_source_returns_diagnostic_without_an_assembly()
    {
        byte[] pe = MemoryCompiler.Compile(
            "public class Script {\n#line 1 \"input.csx\"\npublic void Run() { missing(); }\n}",
            References, default, out byte[] pdb, out string[][] diagnostics);
        Assert.Null(pe);
        Assert.Empty(pdb);
        Assert.Contains(diagnostics, d => d[0] == "CS0103" && d[3] == "input.csx" && d[4] == "1");
    }

    [Fact]
    public void Compilation_does_not_run_static_initializers()
    {
        LiveService.Executed = false;
        byte[] pe = MemoryCompiler.Compile(
            "public class Script { static Script() { YokiFrame.RoslynKit.Compiler.Tests.LiveService.Executed = true; } }",
            References, default, out _, out _);
        Assert.NotNull(pe);
        Assert.False(LiveService.Executed);
    }

    [Fact]
    public void Missing_reference_is_reported()
    {
        Assert.Null(MemoryCompiler.Compile("public class Script {}", new[] { "does-not-exist.dll" },
            default, out _, out string[][] diagnostics));
        Assert.Equal("ScriptReferenceInvalid", diagnostics[0][0]);
    }

    [Fact]
    public void Unsafe_code_is_rejected()
    {
        Assert.Null(MemoryCompiler.Compile("public unsafe class Script { int* pointer; }", References,
            default, out _, out string[][] diagnostics));
        Assert.Contains(diagnostics, d => d[0] == "CS0227");
    }

    [Fact]
    public void Oversized_source_is_rejected_before_parsing()
    {
        Assert.Null(MemoryCompiler.Compile(new string('x', MemoryCompiler.MaxSourceBytes + 1),
            References, default, out _, out string[][] diagnostics));
        Assert.Equal("ScriptInputLimit", diagnostics[0][0]);
    }

    [Fact]
    public void Cancelled_compilation_throws_cancellation()
    {
        Assert.Throws<OperationCanceledException>(() => MemoryCompiler.Compile(
            "public class Script {}", References, new CancellationToken(true), out _, out _));
    }
}

public sealed class LiveService
{
    public int Value = 120;
    public static bool Executed;
}
