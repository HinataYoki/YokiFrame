using YokiFrame;
using YokiFrame.Json;

namespace YokiFrame.EngineKit.Tests;

public sealed class RoslynOperationTests
{
    [Theory]
    [InlineData(YokiFrameEngineExecutionTarget.Editor, "editor")]
    [InlineData(YokiFrameEngineExecutionTarget.Runtime, "runtime")]
    public void Godot_host_target_and_permission_name_are_explicit(YokiFrameEngineExecutionTarget target, string wireTarget)
    {
        using var bed = RunTestBed.Create();
        const string setting = "yokiframe/engine/trusted_csharp";
        var operations = new YokiFrameEngineScriptOperations(bed.Scheduler, new YokiFrameRoslynCompilerLoader(bed.Root),
            () => new YokiFrameEngineDomainState(), () => false,
            allowed => new YokiFrameAutomationContext(() => 0, allowed, _ => { }),
            targets: target, trustedSetting: setting).CreateOperations();
        Assert.Equal(target, operations[0].Descriptor.Targets);
        Assert.False(operations[0].Descriptor.Supports(YokiFrameEngineExecutionTarget.Play));
        var rejected = operations[0].Execute(EnginePipeline.CreateRequest("cli", "script_run",
            "{\"confirmed\":true,\"target\":\"" + wireTarget + "\",\"code\":\"test.Equal(1,1);\"}"));
        Assert.Equal("ScriptExecutionNotPermitted", rejected.ErrorCode);
        Assert.Contains(setting, rejected.ErrorMessage);
        using var status = JsonDocument.Parse(operations[1].Execute(EnginePipeline.CreateRequest("cli", "script_status")).ResultJson);
        Assert.Equal(setting, status.RootElement.GetProperty("trustedSetting").GetString());
    }

    [Fact]
    public void Compiler_uses_host_load_context_and_cannot_compile_after_disposal()
    {
        using var bed = RunTestBed.Create();
        var pe = new byte[] { 1 };
        var pdb = new byte[] { 2 };
        int calls = 0;
        var compiler = new YokiFrameRoslynCompilerLoader(bed.Root, loadAssembly: (image, symbols) =>
        {
            Assert.Same(pe, image);
            Assert.Same(pdb, symbols);
            calls++;
            return typeof(RoslynOperationTests).Assembly;
        });
        Assert.Same(typeof(RoslynOperationTests).Assembly, compiler.LoadAssembly(pe, pdb));
        Assert.Equal(1, calls);
        compiler.Dispose();
        Assert.Throws<ObjectDisposedException>(() => compiler.Compile("", Array.Empty<string>(), default, out _, out _));
    }

    [Fact]
    public void Trusted_permission_controls_gate_capabilities_and_snapshot_version()
    {
        using var bed = RunTestBed.Create();
        bool permitted = false;
        var operations = Create(bed, () => permitted);
        var settings = new StubEngineSettingsSource(YokiFrameEngineSettingsSnapshot.Enabled());
        var gate = YokiFrameEngineGate.CreateDefault(settings);
        var provider = new YokiFrameEngineKitProvider(gate,
            new StubEngineOperationProvider("Unity", YokiFrameEngineExecutionTarget.Editor, operations), settings);
        var request = EnginePipeline.CreateRequest("cli", "script_run",
            """{"target":"editor","confirmed":true,"code":"test.Equal(1,1);"}""");
        Assert.False(gate.Evaluate(request, operations[0].Descriptor).IsAllowed);
        long version = provider.StateVersion;
        Assert.Equal("DisabledBySettings", ReadAvailability(provider));
        permitted = true;
        Assert.True(gate.Evaluate(request, operations[0].Descriptor).IsAllowed);
        Assert.True(provider.StateVersion > version);
        Assert.Equal("Enabled", ReadAvailability(provider));
    }

    [Fact]
    public void Compiler_missing_never_queues_fallback_work()
    {
        using var bed = RunTestBed.Create();
        var operations = Create(bed, () => true);
        var response = operations[0].Execute(EnginePipeline.CreateRequest("cli", "script_run",
            """{"target":"editor","confirmed":true,"code":"test.Equal(1,1);"}"""));
        Assert.False(response.IsSuccess);
        Assert.Equal("ScriptCompilerUnavailable", response.ErrorCode);
        Assert.Empty(bed.Scheduler.ReadRecentRuns(10));
    }

    [Fact]
    public void Diagnostics_and_cancellation_remain_available_with_all_settings_disabled()
    {
        using var bed = RunTestBed.Create();
        var operations = Create(bed, () => false);
        var gate = YokiFrameEngineGate.CreateDefault(
            new StubEngineSettingsSource(YokiFrameEngineSettingsSnapshot.InvalidConfig("broken")));
        foreach (var operation in operations.Skip(1))
            Assert.True(gate.Evaluate(EnginePipeline.CreateRequest("cli", operation.Descriptor.Action),
                operation.Descriptor).IsAllowed);
        var status = operations[1].Execute(EnginePipeline.CreateRequest("cli", "script_status"));
        Assert.True(status.IsSuccess);
        using var document = JsonDocument.Parse(status.ResultJson);
        Assert.False(document.RootElement.GetProperty("executionPermitted").GetBoolean());
        Assert.True(document.RootElement.GetProperty("maxAssemblies").TryGetInt32(out int maxAssemblies));
        Assert.Equal(YokiFrameRoslynLoadBudget.MaxAssemblies, maxAssemblies);
        Assert.True(document.RootElement.GetProperty("maxLoadedBytes").TryGetInt64(out long maxBytes));
        Assert.Equal(YokiFrameRoslynLoadBudget.MaxLoadedBytes, maxBytes);
        Assert.True(document.RootElement.GetProperty("remainingAssemblies").TryGetInt32(out int remaining));
        Assert.Equal(YokiFrameRoslynLoadBudget.MaxAssemblies, remaining);
        Assert.Equal("domain", document.RootElement.GetProperty("budgetScope").GetString());
    }

    private static IYokiFrameEngineOperation[] Create(RunTestBed bed, Func<bool> permitted)
        => new YokiFrameEngineScriptOperations(bed.Scheduler, new YokiFrameRoslynCompilerLoader(bed.Root),
            () => new YokiFrameEngineDomainState(), permitted,
            allowed => new YokiFrameAutomationContext(() => 0, allowed, _ => { })).CreateOperations();

    private static string ReadAvailability(YokiFrameEngineKitProvider provider)
    {
        var response = provider.Handle(EnginePipeline.CreateRequest("cli", "engine_capabilities"));
        using var document = JsonDocument.Parse(response.ResultJson);
        var operations = document.RootElement.GetProperty("operations");
        for (int index = 0; index < operations.GetArrayLength(); index++)
        {
            if (operations[index].GetProperty("action").GetString() == "script_run")
                return operations[index].GetProperty("targetAvailability")[0].GetProperty("availability").GetString()!;
        }
        throw new InvalidOperationException("script_run missing");
    }
}
