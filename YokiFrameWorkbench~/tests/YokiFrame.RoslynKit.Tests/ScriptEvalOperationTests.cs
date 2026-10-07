using System;
using System.Collections.Generic;
using System.IO;
using YokiFrame.Json;
using Xunit;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// eval 的脚本语言分支：语言选择、错误码与 eval_result / eval_prune 的共用记录。
/// </summary>
public sealed class ScriptEvalOperationTests : IDisposable
{
    private readonly string mStoreRoot = Path.Combine(
        Path.GetTempPath(),
        "yokiframe-script-eval-op-tests",
        Guid.NewGuid().ToString("N"));

    /// <summary>删除本测试的 eval 存储目录。目录不存在时不做任何事。</summary>
    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(mStoreRoot))
        {
            Directory.Delete(mStoreRoot, recursive: true);
        }
    }

    [Fact]
    /// <summary>显式 gdscript 走脚本子系统。结果为 Ready，且不带 entryName。</summary>
    public void Eval_with_gdscript_language_uses_the_script_subsystem()
    {
        var host = new FakeScriptHost { ResultJson = "{\"answer\":42}" };
        RoslynKitProvider provider = CreateProvider(host, scriptOnly: true, out _);

        YokiFrameCommandResult result = provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval",
            "{\"confirmed\":true,\"language\":\"gdscript\",\"id\":\"gd1\",\"code\":\"func run():\\n\\treturn 1\"}"));

        Assert.True(result.IsSuccess);
        JsonElement root = JsonDocument.Parse(result.ResultJson).RootElement;
        Assert.Equal("gdscript", root.GetProperty("language").GetString());
        Assert.Equal("gd1", root.GetProperty("evalId").GetString());
        Assert.Equal(RoslynEvalStates.READY, root.GetProperty("state").GetString());
        Assert.False(root.TryGetProperty("entryName", out _));
    }

    [Fact]
    /// <summary>未知语言返回 UNSUPPORTED。不落入 C# 编译路径。</summary>
    public void Eval_with_unknown_language_is_unsupported()
    {
        RoslynKitProvider provider = CreateProvider(new FakeScriptHost(), scriptOnly: true, out _);
        YokiFrameCommandResult result = provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval",
            "{\"confirmed\":true,\"language\":\"lua\",\"id\":\"x\",\"code\":\"print(1)\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.UNSUPPORTED, result.ErrorCode);
    }

    [Fact]
    /// <summary>没有显式 language 时返回 INVALID_PAYLOAD。不按默认语言执行。</summary>
    public void Eval_without_explicit_language_is_rejected()
    {
        RoslynKitProvider provider = CreateProvider(new FakeScriptHost(), scriptOnly: true, out _);
        YokiFrameCommandResult result = provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval",
            "{\"confirmed\":true,\"id\":\"x\",\"code\":\"return 1;\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
    }

    [Fact]
    /// <summary>脚本 eval 拒绝 codeFile。错误码为 INVALID_PAYLOAD，且错误信息非空。</summary>
    public void Eval_script_rejects_code_file()
    {
        RoslynKitProvider provider = CreateProvider(new FakeScriptHost(), scriptOnly: true, out _);
        YokiFrameCommandResult result = provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval",
            "{\"confirmed\":true,\"language\":\"script\",\"id\":\"x\",\"codeFile\":\"Assets/toast.gd\"}"));

        Assert.False(result.IsSuccess);
        // 两条拒绝路径都合法：解析器先按"项目内文件不存在"拒绝，脚本分支自己按"只支持内联代码"拒绝。
        Assert.Equal(RoslynErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    /// <summary>eval_result 读到脚本记录，源路径为 memory://script。不重新编译。</summary>
    public void Eval_result_reads_script_records()
    {
        RoslynKitProvider provider = CreateProvider(new FakeScriptHost(), scriptOnly: true, out _);
        provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval",
            "{\"confirmed\":true,\"language\":\"gdscript\",\"id\":\"gd2\",\"code\":\"func run():\\n\\tpass\"}"));

        YokiFrameCommandResult result = provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval_result",
            "{\"id\":\"gd2\"}"));

        Assert.True(result.IsSuccess);
        JsonElement root = JsonDocument.Parse(result.ResultJson).RootElement;
        Assert.Equal("gd2", root.GetProperty("evalId").GetString());
        Assert.Equal("memory://script", root.GetProperty("sourcePath").GetString());
    }

    [Fact]
    /// <summary>eval_prune 报告删除了一条脚本，并让宿主卸载对应标识。</summary>
    public void Eval_prune_reports_removed_scripts()
    {
        var host = new FakeScriptHost();
        RoslynKitProvider provider = CreateProvider(host, scriptOnly: true, out _);
        provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval",
            "{\"confirmed\":true,\"language\":\"gdscript\",\"id\":\"gd3\",\"code\":\"func run():\\n\\tpass\"}"));

        YokiFrameCommandResult result = provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval_prune",
            "{}"));

        Assert.True(result.IsSuccess);
        Assert.True(JsonDocument.Parse(result.ResultJson).RootElement.GetProperty("removedScripts").TryGetInt32(out int removedScripts));
        Assert.Equal(1, removedScripts);
        Assert.Contains("gd3", host.Unloaded);
    }

    /// <summary>装配 RoslynKit Provider；scriptOnly 为 true 时只接脚本 eval（模拟 Godot）。</summary>
    private RoslynKitProvider CreateProvider(
        FakeScriptHost host,
        bool scriptOnly,
        out StubEngineOperationProvider engineProvider)
    {
        var settingsSource = new StubEngineSettingsSource(RoslynSettingsSnapshot.Enabled());
        engineProvider = new StubEngineOperationProvider("Godot", RoslynExecutionTarget.Editor);
        if (scriptOnly)
        {
            engineProvider.ScriptEvalService = new RoslynScriptEvalService(
                host,
                new RoslynEvalStore(mStoreRoot));
        }

        return new RoslynKitProvider(
            RoslynGate.CreateDefault(settingsSource),
            engineProvider,
            settingsSource);
    }

    private sealed class FakeScriptHost : IRoslynScriptEvalHost
    {
        public string Language { get; set; } = "gdscript";

        public string ResultJson { get; set; } = "{}";

        public List<string> Unloaded { get; } = new();

        /// <summary>总是编译成功。不记录源码，错误信息为空。</summary>
        /// <param name="id">未使用的脚本标识。</param>
        /// <param name="token">未使用的令牌。</param>
        /// <param name="code">未使用的源码。</param>
        /// <param name="error">始终为空的错误。</param>
        /// <returns>始终为 true。</returns>
        public bool TryCompile(string id, string token, string code, out string error)
        {
            error = string.Empty;
            return true;
        }

        /// <summary>总是调用成功并返回预设 JSON。不记录调用。</summary>
        /// <param name="id">未使用的脚本标识。</param>
        /// <param name="token">未使用的令牌。</param>
        /// <param name="resultJson">预设结果 JSON。</param>
        /// <param name="error">始终为空的错误。</param>
        /// <returns>始终为 true。</returns>
        public bool TryInvoke(string id, string token, out string resultJson, out string error)
        {
            error = string.Empty;
            resultJson = ResultJson;
            return true;
        }

        /// <summary>记录被卸载的脚本标识。不删除存储。</summary>
        /// <param name="id">脚本标识。</param>
        public void Unload(string id)
        {
            Unloaded.Add(id);
        }
    }
}
