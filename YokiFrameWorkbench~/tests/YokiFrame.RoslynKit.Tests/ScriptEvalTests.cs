using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 脚本类 eval（Godot GDScript 路径）：同步编译 + 立即调用，结论落在记录里。
/// </summary>
public sealed class ScriptEvalTests : IDisposable
{
    private readonly string mStoreRoot = Path.Combine(
        Path.GetTempPath(),
        "yokiframe-script-eval-tests",
        Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(mStoreRoot))
        {
            Directory.Delete(mStoreRoot, recursive: true);
        }
    }

    [Fact]
    public void Submit_compiles_and_invokes_synchronously()
    {
        var host = new FakeScriptHost { ResultJson = "{\"ok\":true}" };
        RoslynScriptEvalService service = CreateService(host);

        RoslynEvalRecord record = service.Submit("smoke", "func run():\n\treturn 1", out string code, out string message);

        Assert.Equal(string.Empty, code);
        Assert.Equal(string.Empty, message);
        Assert.NotNull(record);
        Assert.Equal(RoslynEvalStates.READY, record.State);
        Assert.Equal("{\"ok\":true}", record.Note);
        Assert.Equal("memory://script", record.SourcePath);
        Assert.Empty(record.EntryName);
        Assert.Single(host.Compiled);
        Assert.Single(host.Invoked);
        Assert.Equal(record.Token, host.Invoked[0].Token);

        // 记录必须落盘：换一个 store 实例仍能读到，CLI 的 eval_result 才能跨进程读
        var reopened = new RoslynEvalStore(mStoreRoot);
        Assert.True(reopened.TryRead("smoke", out RoslynEvalRecord persisted));
        Assert.Equal(RoslynEvalStates.READY, persisted.State);
    }

    [Fact]
    public void Compile_failure_is_recorded_as_state_not_error_code()
    {
        var host = new FakeScriptHost { CompileError = "Parse Error: line 2" };
        RoslynScriptEvalService service = CreateService(host);

        RoslynEvalRecord record = service.Submit("broken", "func run(:", out string code, out _);

        Assert.Equal(string.Empty, code);
        Assert.Equal(RoslynEvalStates.COMPILE_FAILED, record.State);
        Assert.Contains("Parse Error", record.CompilerErrors, StringComparison.Ordinal);
        Assert.Empty(host.Invoked);
    }

    [Fact]
    public void Invoke_failure_keeps_ready_with_error_note()
    {
        var host = new FakeScriptHost { InvokeError = "boom" };
        RoslynScriptEvalService service = CreateService(host);

        RoslynEvalRecord record = service.Submit("throwing", "func run():\n\tassert(false)", out _, out _);

        Assert.Equal(RoslynEvalStates.READY, record.State);
        Assert.StartsWith("invoke failed:", record.Note, StringComparison.Ordinal);
        Assert.Contains("boom", record.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_host_and_invalid_request_are_reported_separately()
    {
        var service = new RoslynScriptEvalService(null, new RoslynEvalStore(mStoreRoot));
        service.Submit("x", "func run():\n\tpass", out string unavailable, out _);
        Assert.Equal(RoslynErrorCodes.UNAVAILABLE, unavailable);

        RoslynScriptEvalService wired = CreateService(new FakeScriptHost());
        wired.Submit(string.Empty, "func run():\n\tpass", out string invalidId, out _);
        Assert.Equal(RoslynErrorCodes.INVALID_PAYLOAD, invalidId);
        wired.Submit("ok", "   ", out string invalidCode, out _);
        Assert.Equal(RoslynErrorCodes.INVALID_PAYLOAD, invalidCode);
    }

    [Fact]
    public void Prune_unloads_scripts_and_removes_records()
    {
        var host = new FakeScriptHost();
        RoslynScriptEvalService service = CreateService(host);
        service.Submit("a", "func run():\n\tpass", out _, out _);
        service.Submit("b", "func run():\n\tpass", out _, out _);

        int removed = service.Prune(includeNonTerminal: false);

        Assert.Equal(2, removed);
        Assert.Equal(new[] { "a", "b" }, host.Unloaded);
        Assert.False(service.TryRead("a", out _));
    }

    [Fact]
    public void Language_comes_from_the_host()
    {
        var host = new FakeScriptHost { Language = "gdscript" };
        Assert.Equal("gdscript", CreateService(host).Language);
        Assert.Equal(string.Empty, new RoslynScriptEvalService(null, null).Language);
    }

    private RoslynScriptEvalService CreateService(FakeScriptHost host)
    {
        return new RoslynScriptEvalService(host, new RoslynEvalStore(mStoreRoot));
    }

    /// <summary>假脚本宿主：把编译/调用结果做成开关，并记录调用顺序。</summary>
    private sealed class FakeScriptHost : IRoslynScriptEvalHost
    {
        public string Language { get; set; } = "gdscript";

        public string CompileError { get; set; } = string.Empty;

        public string InvokeError { get; set; } = string.Empty;

        public string ResultJson { get; set; } = "{}";

        public List<(string Id, string Token)> Compiled { get; } = new();

        public List<(string Id, string Token)> Invoked { get; } = new();

        public List<string> Unloaded { get; } = new();

        public bool TryCompile(string id, string token, string code, out string error)
        {
            error = CompileError;
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            Compiled.Add((id, token));
            return true;
        }

        public bool TryInvoke(string id, string token, out string resultJson, out string error)
        {
            resultJson = string.Empty;
            error = InvokeError;
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            Invoked.Add((id, token));
            resultJson = ResultJson;
            return true;
        }

        public void Unload(string id)
        {
            Unloaded.Add(id);
        }
    }
}
