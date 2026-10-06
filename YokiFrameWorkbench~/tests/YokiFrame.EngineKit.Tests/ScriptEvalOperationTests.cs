using System;
using System.Collections.Generic;
using System.IO;
using YokiFrame.Json;
using Xunit;

namespace YokiFrame.EngineKit.Tests;

/// <summary>
/// eval 的脚本语言分支：语言选择、错误码与 eval_result / eval_prune 的共用记录。
/// </summary>
public sealed class ScriptEvalOperationTests : IDisposable
{
    private readonly string mStoreRoot = Path.Combine(
        Path.GetTempPath(),
        "yokiframe-script-eval-op-tests",
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
    public void Eval_with_gdscript_language_uses_the_script_subsystem()
    {
        var host = new FakeScriptHost { ResultJson = "{\"answer\":42}" };
        YokiFrameEngineKitProvider provider = CreateProvider(host, scriptOnly: true, out _);

        YokiFrameCommandResult result = provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval",
            "{\"confirmed\":true,\"language\":\"gdscript\",\"id\":\"gd1\",\"code\":\"func run():\\n\\treturn 1\"}"));

        Assert.True(result.IsSuccess);
        JsonElement root = JsonDocument.Parse(result.ResultJson).RootElement;
        Assert.Equal("gdscript", root.GetProperty("language").GetString());
        Assert.Equal("gd1", root.GetProperty("evalId").GetString());
        Assert.Equal(YokiFrameEngineEvalStates.READY, root.GetProperty("state").GetString());
        Assert.False(root.TryGetProperty("entryName", out _));
    }

    [Fact]
    public void Eval_with_unknown_language_is_unsupported()
    {
        YokiFrameEngineKitProvider provider = CreateProvider(new FakeScriptHost(), scriptOnly: true, out _);
        YokiFrameCommandResult result = provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval",
            "{\"confirmed\":true,\"language\":\"lua\",\"id\":\"x\",\"code\":\"print(1)\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.UNSUPPORTED, result.ErrorCode);
    }

    [Fact]
    public void Eval_without_explicit_language_is_rejected()
    {
        YokiFrameEngineKitProvider provider = CreateProvider(new FakeScriptHost(), scriptOnly: true, out _);
        YokiFrameCommandResult result = provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval",
            "{\"confirmed\":true,\"id\":\"x\",\"code\":\"return 1;\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
    }

    [Fact]
    public void Eval_script_rejects_code_file()
    {
        YokiFrameEngineKitProvider provider = CreateProvider(new FakeScriptHost(), scriptOnly: true, out _);
        YokiFrameCommandResult result = provider.Handle(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            "eval",
            "{\"confirmed\":true,\"language\":\"script\",\"id\":\"x\",\"codeFile\":\"Assets/toast.gd\"}"));

        Assert.False(result.IsSuccess);
        // 两条拒绝路径都合法：解析器先按"项目内文件不存在"拒绝，脚本分支自己按"只支持内联代码"拒绝。
        Assert.Equal(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void Eval_result_reads_script_records()
    {
        YokiFrameEngineKitProvider provider = CreateProvider(new FakeScriptHost(), scriptOnly: true, out _);
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
    public void Eval_prune_reports_removed_scripts()
    {
        var host = new FakeScriptHost();
        YokiFrameEngineKitProvider provider = CreateProvider(host, scriptOnly: true, out _);
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

    /// <summary>装配 Engine Kit Provider；scriptOnly 为 true 时只接脚本 eval（模拟 Godot）。</summary>
    private YokiFrameEngineKitProvider CreateProvider(
        FakeScriptHost host,
        bool scriptOnly,
        out StubEngineOperationProvider engineProvider)
    {
        var settingsSource = new StubEngineSettingsSource(YokiFrameEngineSettingsSnapshot.Enabled());
        engineProvider = new StubEngineOperationProvider("Godot", YokiFrameEngineExecutionTarget.Editor);
        if (scriptOnly)
        {
            engineProvider.ScriptEvalService = new YokiFrameEngineScriptEvalService(
                host,
                new YokiFrameEngineEvalStore(mStoreRoot));
        }

        return new YokiFrameEngineKitProvider(
            YokiFrameEngineGate.CreateDefault(settingsSource),
            engineProvider,
            settingsSource);
    }

    private sealed class FakeScriptHost : IYokiFrameEngineScriptEvalHost
    {
        public string Language { get; set; } = "gdscript";

        public string ResultJson { get; set; } = "{}";

        public List<string> Unloaded { get; } = new();

        public bool TryCompile(string id, string token, string code, out string error)
        {
            error = string.Empty;
            return true;
        }

        public bool TryInvoke(string id, string token, out string resultJson, out string error)
        {
            error = string.Empty;
            resultJson = ResultJson;
            return true;
        }

        public void Unload(string id)
        {
            Unloaded.Add(id);
        }
    }
}
