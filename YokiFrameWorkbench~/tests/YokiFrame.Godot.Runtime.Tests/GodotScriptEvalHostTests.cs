using System.Collections.Generic;
using Xunit;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// Godot 脚本 eval 宿主：包装源码、令牌校验、重复提交与卸载——全部在注入的假编译器下验证。
/// </summary>
public sealed class GodotScriptEvalHostTests
{
    [Fact]
    public void Compile_wraps_code_with_token_and_entry()
    {
        var compiler = new FakeCompiler();
        var host = new GodotScriptEvalHost(compiler);

        Assert.True(host.TryCompile("a", "tok-1", "return 1\nreturn 2", out string error));
        Assert.Equal(string.Empty, error);
        Assert.Contains("# yokiframe-eval-token: tok-1", compiler.LastSource, System.StringComparison.Ordinal);
        Assert.Contains("extends RefCounted", compiler.LastSource, System.StringComparison.Ordinal);
        Assert.Contains("func run():", compiler.LastSource, System.StringComparison.Ordinal);
        // 函数体必须被制表符缩进，否则 GDScript 解析失败
        Assert.Contains("\treturn 1\n\treturn 2", compiler.LastSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Invoke_requires_the_matching_token()
    {
        var host = new GodotScriptEvalHost(new FakeCompiler());
        host.TryCompile("a", "tok-1", "pass", out _);

        Assert.False(host.TryInvoke("a", "tok-2", out _, out string error));
        Assert.Contains("token mismatch", error, System.StringComparison.Ordinal);

        Assert.True(host.TryInvoke("a", "tok-1", out string resultJson, out _));
        Assert.Equal("{\"ok\":true}", resultJson);
    }

    [Fact]
    public void Recompiling_the_same_id_frees_the_previous_handle()
    {
        var compiler = new FakeCompiler();
        var host = new GodotScriptEvalHost(compiler);
        host.TryCompile("a", "tok-1", "pass", out _);
        host.TryCompile("a", "tok-2", "pass", out _);

        Assert.Single(compiler.Freed);

        // 旧令牌必须失效（同名残留不会被误调用）
        Assert.False(host.TryInvoke("a", "tok-1", out _, out _));
        Assert.True(host.TryInvoke("a", "tok-2", out _, out _));
    }

    [Fact]
    public void Compile_failure_is_reported_and_keeps_no_handle()
    {
        var compiler = new FakeCompiler { CompileError = "Parse Error: 3:1" };
        var host = new GodotScriptEvalHost(compiler);

        Assert.False(host.TryCompile("a", "tok", "func", out string error));
        Assert.Contains("Parse Error", error, System.StringComparison.Ordinal);
        Assert.False(host.TryInvoke("a", "tok", out _, out string invokeError));
        Assert.Contains("has not been compiled", invokeError, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Unload_drops_the_handle()
    {
        var compiler = new FakeCompiler();
        var host = new GodotScriptEvalHost(compiler);
        host.TryCompile("a", "tok", "pass", out _);

        host.Unload("a");

        Assert.Single(compiler.Freed);
        Assert.False(host.TryInvoke("a", "tok", out _, out _));
        Assert.Equal("gdscript", host.Language);
    }

    [Fact]
    public void Call_failure_is_propagated()
    {
        var compiler = new FakeCompiler { CallError = "Invalid call. Nonexistent function 'run'." };
        var host = new GodotScriptEvalHost(compiler);
        host.TryCompile("a", "tok", "pass", out _);

        Assert.False(host.TryInvoke("a", "tok", out _, out string error));
        Assert.Contains("Nonexistent function", error, System.StringComparison.Ordinal);
    }

    private sealed class FakeCompiler : IGodotScriptCompiler
    {
        public string LastSource { get; private set; } = string.Empty;

        public string CompileError { get; set; } = string.Empty;

        public string CallError { get; set; } = string.Empty;

        public List<object> Freed { get; } = new();

        public bool TryCompile(string source, out object handle, out string error)
        {
            LastSource = source;
            error = CompileError;
            handle = null;
            if (CompileError.Length > 0)
            {
                return false;
            }

            handle = new object();
            return true;
        }

        public bool TryCall(object handle, string method, out string resultJson, out string error)
        {
            resultJson = string.Empty;
            error = CallError;
            if (CallError.Length > 0)
            {
                return false;
            }

            resultJson = "{\"ok\":true}";
            return true;
        }

        public void Free(object handle)
        {
            Freed.Add(handle);
        }
    }
}
