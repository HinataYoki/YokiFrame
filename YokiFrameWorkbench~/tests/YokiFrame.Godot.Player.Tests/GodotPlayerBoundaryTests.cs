using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection;
using System.Text;

namespace YokiFrame.Godot.Player.Tests;

/// <summary>
/// 验证 Godot 导出形态只包含业务 Runtime，不携带 Workbench、通信宿主或 Editor 配置。
/// </summary>
public sealed class GodotPlayerBoundaryTests
{
    private static readonly string[] sAssemblyFileNames =
    [
        "YokiFrame.dll",
        "YokiFrame.ActionKit.dll",
        "YokiFrame.AudioKit.dll",
        "YokiFrame.SaveKit.dll",
        "YokiFrame.SpatialKit.dll",
        "YokiFrame.Godot.Runtime.dll",
        "YokiFrame.AudioKit.Godot.dll",
        "YokiFrame.SaveKit.Godot.dll",
    ];

    private static readonly string[] sRequiredTypeNames =
    [
        "YokiFrame.EventKit",
        "YokiFrame.ActionKit",
        "YokiFrame.AudioKit",
        "YokiFrame.SaveKit",
        "YokiFrame.SpatialKit",
        "YokiFrame.Godot.GodotAudioKitBackend",
        "YokiFrame.Godot.GodotSaveKitRuntimeInstaller",
        "YokiFrame.GodotBootstrap"
    ];

    private static readonly string[] sForbiddenTypeNames =
    [
        "YokiFrame.ActionKitDiagnosticHistory",
        "YokiFrame.ActionKitInteractionProvider",
        "YokiFrame.AudioKitInteractionProvider",
        "YokiFrame.AudioVoiceSnapshot",
        "YokiFrame.AudioBusSnapshot",
        "YokiFrame.AudioHistoryEntry",
        "YokiFrame.ArchitectureRegistry",
        "YokiFrame.EventKitDiagnosticRegistry",
        "YokiFrame.FsmKitRegistry",
        "YokiFrame.GodotFastChannelListener",
        "YokiFrame.GodotFileBridgeHost",
        "YokiFrame.GodotSharedMemoryTelemetryWriter",
        "YokiFrame.IEngineLoggerWithStackTrace",
        "YokiFrame.LogKitHostEnvironment",
        "YokiFrame.LogKitWorkbenchSnapshot",
        "YokiFrame.PoolDebugger",
        "YokiFrame.ResKitInteractionProvider",
        "YokiFrame.SaveKitCommandHandler",
        "YokiFrame.SaveKitDiagnosticsMeta",
        "YokiFrame.SaveKitDiagnosticsSnapshot",
        "YokiFrame.SaveKitEditorInstaller",
        "YokiFrame.SaveKitInteractionProvider",
        "YokiFrame.SaveKitSnapshotWriter",
        "YokiFrame.ISpatialGizmoDiagnostics",
        "YokiFrame.SpatialGizmoNodeSnapshot",
        "YokiFrame.SpatialGizmoEntitySnapshot",
        "YokiFrame.SpatialGizmoIndexSnapshot",
        "YokiFrame.SpatialGizmoDiagnosticsFrame",
        "YokiFrame.SpatialKitDiagnosticsRegistry",
        "YokiFrame.SingletonRegistry",
        "YokiFrame.YokiFrameCommandDispatcher",
        "YokiFrame.YokiFrameKitInteractionRegistry",
        "YokiFrame.YokiFrameSharedMemoryTelemetryContract"
    ];

    private static readonly string[] sForbiddenQualifiedMembers =
    [
        "ActionController.MarkStackTraceRegistered",
        "ActionController.TryClearStackTraceRegistered",
        "ActionKitScheduler.FrameCount",
        "ActionKitScheduler.FinishedCount",
        "ActionKitScheduler.CancelledCount",
        "ActionKitScheduler.FaultedCount",
        "ActionKitScheduler.ExecutingCount",
        "ActionKitScheduler.DiagnosticVersion",
        "ActionKitScheduler.GetExecutingActions",
        "AudioKit.DiagnosticVersion",
        "AudioKit.HistoryTotalCount",
        "AudioKit.GetActiveVoices",
        "AudioKit.GetBuses",
        "AudioKit.GetHistory",
        "AudioKit.ClearHistory",
        "FSM.PublishStateAdded",
        "FSM.PublishStateChanged",
        "FSM.Name",
        "FSM.mName",
        "LogKit.DiagnosticVersion",
        "LogKit.GetHistory",
        "LogKit.LoggerName",
        "LogKitSettings.ApplyPayload",
        "LogKitSettings.BuildJson",
        "ResKit.CaptureDiagnosticSnapshot",
        "ResKit.CaptureLoadSource",
        "ResKit.DiagnosticVersion",
        "ResKit.GetUnloadHistory",
        "SpatialKit.CreateGizmoDiagnosticsFrame"
    ];

    private static readonly string[] sForbiddenParameterNames =
    [
        "monitorChannel",
        "sourceFile",
        "sourceLine"
    ];

    private static readonly string[] sForbiddenAssemblyReferences =
    [
        "Avalonia",
        "GodotSharpEditor",
        "UniTask",
        "UnityEngine",
        "YokiFrame.Editor",
        "YokiFrame.ActionKit.Editor",
        "YokiFrame.ActionKit.UniTask",
        "YokiFrame.ActionKit.Unity",
        "YokiFrame.AudioKit.Editor",
        "YokiFrame.Godot.Editor",
        "YokiFrame.SaveKit.Editor",
        "YokiFrame.Protocol",
        "YokiFrame.Tooling",
        "YokiFrame.Workbench"
    ];

    private static readonly string[] sForbiddenConfigurationTokens =
    [
        "editor-settings.json",
        "saveLogInEditor",
        "editorFileName",
        "yoki_editor.log"
    ];

    /// <summary>
    /// 扫描无 TOOLS 的真实 DLL，确认诊断类型、partial 成员、Editor 引用和配置文本均不存在。
    /// </summary>
    [Fact]
    public void NoToolsAssembliesExcludeWorkbenchAndEditorSurface()
    {
        string[] assemblyPaths = ResolveAssemblyPaths();
        HashSet<string> typeNames = new(StringComparer.Ordinal);
        HashSet<string> memberNames = new(StringComparer.Ordinal);
        HashSet<string> parameterNames = new(StringComparer.Ordinal);
        HashSet<string> referenceNames = new(StringComparer.Ordinal);
        for (var index = 0; index < assemblyPaths.Length; index++)
        {
            ReadMetadata(assemblyPaths[index], typeNames, memberNames, parameterNames, referenceNames);
        }

        AssertForbiddenValuesAbsent(typeNames, sForbiddenTypeNames, "Godot Player 类型");
        AssertForbiddenValuesAbsent(memberNames, sForbiddenQualifiedMembers, "Godot Player 成员");
        AssertForbiddenValuesAbsent(parameterNames, sForbiddenParameterNames, "Godot Player 参数");
        AssertRequiredValuesPresent(typeNames, sRequiredTypeNames);
        AssertFsmNameConstructor(assemblyPaths[0]);
        AssertForbiddenReferencesAbsent(referenceNames);
        AssertForbiddenConfigurationAbsent(assemblyPaths);
    }

    /// <summary>确认无 TOOLS Core 产物仍接受可选名称，但不要求任何诊断成员存在。</summary>
    /// <param name="assemblyPath">YokiFrame Core 程序集路径。</param>
    private static void AssertFsmNameConstructor(string assemblyPath)
    {
        Assembly assembly = Assembly.LoadFrom(assemblyPath);
        Type fsmType = assembly.GetType("YokiFrame.FSM`1", throwOnError: true)!;
        ConstructorInfo? constructor = fsmType.GetConstructor(new[] { typeof(string) });
        Assert.NotNull(constructor);
    }

    /// <summary>定位测试构建目标复制出的三份无 Tools 业务程序集。</summary>
    /// <returns>按固定程序集名排序的绝对路径。</returns>
    private static string[] ResolveAssemblyPaths()
    {
        string outputRoot = Path.Combine(AppContext.BaseDirectory, "player-boundary");
        string[] paths = new string[sAssemblyFileNames.Length];
        for (var index = 0; index < sAssemblyFileNames.Length; index++)
        {
            paths[index] = Path.Combine(outputRoot, sAssemblyFileNames[index]);
            Assert.True(File.Exists(paths[index]), "缺少 Godot Player 边界程序集: " + paths[index]);
        }
        return paths;
    }

    /// <summary>读取单个 ECMA-335 程序集的类型、成员和程序集引用。</summary>
    /// <param name="assemblyPath">待扫描程序集路径。</param>
    /// <param name="typeNames">接收完整类型名。</param>
    /// <param name="memberNames">接收 Type.Member 限定名称。</param>
    /// <param name="parameterNames">接收方法参数名。</param>
    /// <param name="referenceNames">接收程序集引用简单名。</param>
    private static void ReadMetadata(
        string assemblyPath,
        HashSet<string> typeNames,
        HashSet<string> memberNames,
        HashSet<string> parameterNames,
        HashSet<string> referenceNames)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader peReader = new(stream);
        MetadataReader reader = peReader.GetMetadataReader();
        foreach (var handle in reader.TypeDefinitions)
        {
            TypeDefinition definition = reader.GetTypeDefinition(handle);
            string typeName = reader.GetString(definition.Name);
            string typeNamespace = reader.GetString(definition.Namespace);
            typeNames.Add(string.IsNullOrEmpty(typeNamespace) ? typeName : typeNamespace + "." + typeName);
            AddMemberNames(reader, definition, NormalizeTypeName(typeName), memberNames, parameterNames);
        }
        foreach (var handle in reader.AssemblyReferences)
            referenceNames.Add(reader.GetString(reader.GetAssemblyReference(handle).Name));
    }

    /// <summary>把类型声明的方法、字段和属性追加为 Type.Member 限定名称。</summary>
    /// <param name="reader">当前程序集元数据读取器。</param>
    /// <param name="definition">当前类型定义。</param>
    /// <param name="typeName">已移除泛型 arity 的类型名。</param>
    /// <param name="memberNames">接收限定成员名。</param>
    /// <param name="parameterNames">接收方法参数名。</param>
    private static void AddMemberNames(
        MetadataReader reader,
        TypeDefinition definition,
        string typeName,
        HashSet<string> memberNames,
        HashSet<string> parameterNames)
    {
        foreach (var handle in definition.GetMethods())
        {
            MethodDefinition method = reader.GetMethodDefinition(handle);
            memberNames.Add(typeName + "." + reader.GetString(method.Name));
            foreach (var parameterHandle in method.GetParameters())
            {
                string parameterName = reader.GetString(reader.GetParameter(parameterHandle).Name);
                if (!string.IsNullOrEmpty(parameterName)) parameterNames.Add(parameterName);
            }
        }
        foreach (var handle in definition.GetFields())
            memberNames.Add(typeName + "." + reader.GetString(reader.GetFieldDefinition(handle).Name));
        foreach (var handle in definition.GetProperties())
            memberNames.Add(typeName + "." + reader.GetString(reader.GetPropertyDefinition(handle).Name));
    }

    /// <summary>断言一组禁止值均未出现在实际集合中。</summary>
    /// <param name="actual">实际元数据名称。</param>
    /// <param name="forbidden">禁止进入 Player 的名称。</param>
    /// <param name="scope">断言失败时的边界名称。</param>
    private static void AssertForbiddenValuesAbsent(
        HashSet<string> actual,
        IEnumerable<string> forbidden,
        string scope)
    {
        var hits = forbidden.Where(actual.Contains).Order(StringComparer.Ordinal).ToArray();
        Assert.True(hits.Length == 0, scope + "仍包含工具项:\n" + string.Join("\n", hits));
    }

    /// <summary>断言三个业务正向类型均真实存在，避免扫描空产物假通过。</summary>
    /// <param name="actual">实际完整类型名。</param>
    /// <param name="required">必须存在的业务类型名。</param>
    private static void AssertRequiredValuesPresent(HashSet<string> actual, IEnumerable<string> required)
    {
        var missing = required.Where(value => !actual.Contains(value)).ToArray();
        Assert.True(missing.Length == 0, "Godot Player 缺少业务 Runtime 类型:\n" + string.Join("\n", missing));
    }

    /// <summary>断言业务程序集没有引用 Editor、Workbench 或工具协议程序集。</summary>
    /// <param name="referenceNames">真实程序集引用简单名。</param>
    private static void AssertForbiddenReferencesAbsent(HashSet<string> referenceNames)
    {
        var hits = referenceNames
            .Where(reference => sForbiddenAssemblyReferences.Any(
                prefix => reference.StartsWith(prefix, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(hits.Length == 0, "Godot Player 仍引用工具程序集:\n" + string.Join("\n", hits));
    }

    /// <summary>按 UTF-8 与 UTF-16LE 扫描真实 DLL 中的 Editor 配置文本。</summary>
    /// <param name="assemblyPaths">待扫描业务程序集路径。</param>
    private static void AssertForbiddenConfigurationAbsent(IEnumerable<string> assemblyPaths)
    {
        List<string> hits = [];
        foreach (var assemblyPath in assemblyPaths)
        {
            byte[] bytes = File.ReadAllBytes(assemblyPath);
            foreach (var token in sForbiddenConfigurationTokens)
            {
                if (bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(token)) >= 0
                    || bytes.AsSpan().IndexOf(Encoding.Unicode.GetBytes(token)) >= 0)
                    hits.Add(Path.GetFileName(assemblyPath) + ":" + token);
            }
        }
        Assert.True(hits.Count == 0, "Godot Player 仍包含 Editor 配置:\n" + string.Join("\n", hits));
    }

    /// <summary>移除泛型 arity 后缀，使元数据类型名与源码类型名一致。</summary>
    /// <param name="name">元数据类型简单名。</param>
    /// <returns>不含反引号 arity 的类型名。</returns>
    private static string NormalizeTypeName(string name)
    {
        var arityIndex = name.IndexOf('`');
        return arityIndex < 0 ? name : name[..arityIndex];
    }
}
