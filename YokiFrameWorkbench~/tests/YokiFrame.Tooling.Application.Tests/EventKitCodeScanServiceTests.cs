using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Tooling.Application.Services.EventKit;

namespace YokiFrame.Tooling.Application.Tests;

public sealed class EventKitCodeScanServiceTests : IDisposable
{
    private readonly string mProjectRoot = Path.Combine(
        Path.GetTempPath(),
        "yokiframe-eventkit-scan-" + Guid.NewGuid().ToString("N"));

    /// <summary>创建隔离的 Unity Assets 目录。</summary>
    public EventKitCodeScanServiceTests()
    {
        Directory.CreateDirectory(Path.Combine(mProjectRoot, "Assets", "Scripts"));
    }

    /// <summary>验证 Roslyn 能识别跨行 Type/Enum/String 调用并忽略注释和字符串内容。</summary>
    [Fact]
    public async Task ScanAsyncParsesCallsAndIgnoresCommentsAndStrings()
    {
        WriteSource("Assets/Scripts/Demo.cs", """
            namespace Demo;
            public enum Signal { Ready }
            public sealed class DamageEvent { }
            public sealed class Handler
            {
                void Setup()
                {
                    // EventKit.Type.Send(new FakeCommentEvent());
                    string text = "EventKit.Type.Send(new FakeStringEvent())";
                    EventKit.Type.Register<DamageEvent>(OnDamage);
                    EventKit.Type.Send(
                        new DamageEvent());
                    EventKit.Enum.Register<Signal, int>(Signal.Ready, OnReady);
                    EventKit.Enum.Send(Signal.Ready, 42);
                    EventKit.String.Send("legacy.message");
                    EventKit.Type.UnRegister<DamageEvent>(OnDamage);
                }
                void OnDamage(DamageEvent value) { }
                void OnReady(int value) { }
            }
            """);

        WorkbenchEventKitCodeScan scan = await CreateService().ScanAsync(true, CancellationToken.None);

        Assert.Equal(3, scan.Relations.Count);
        WorkbenchEventKitCodeRelation type = Assert.Single(
            scan.Relations,
            static relation => relation.Channel == "Type");
        Assert.Equal("Demo.DamageEvent", type.EventKey);
        Assert.Single(type.Senders);
        Assert.Single(type.Receivers);
        Assert.Single(type.Unregisters);
        Assert.DoesNotContain(scan.Relations, static relation => relation.EventKey.Contains("Fake", StringComparison.Ordinal));
    }

    /// <summary>验证同名命名空间类型和闭合泛型实参不会被错误合并。</summary>
    [Fact]
    public async Task ScanAsyncPreservesNamespaceAndClosedGenericIdentity()
    {
        WriteSource("Assets/Scripts/Generic.cs", """
            namespace Combat { public sealed class DamageEvent { } }
            namespace UI { public sealed class DamageEvent { } }
            public sealed class Envelope<T> { }
            public sealed class Demo
            {
                void Run()
                {
                    EventKit.Type.Send(new Combat.DamageEvent());
                    EventKit.Type.Send(new UI.DamageEvent());
                    EventKit.Type.Send(new Envelope<Combat.DamageEvent>());
                    EventKit.Type.Send(new Envelope<UI.DamageEvent>());
                }
            }
            """);

        WorkbenchEventKitCodeScan scan = await CreateService().ScanAsync(true, CancellationToken.None);
        string[] identities = scan.Relations.Select(static relation => relation.Identity).ToArray();

        Assert.Equal(4, identities.Length);
        Assert.Contains(identities, static identity => identity.Contains("Combat.DamageEvent", StringComparison.Ordinal));
        Assert.Contains(identities, static identity => identity.Contains("UI.DamageEvent", StringComparison.Ordinal));
        Assert.Contains(identities, static identity => identity.Contains("Envelope<Combat.DamageEvent>", StringComparison.Ordinal));
        Assert.Contains(identities, static identity => identity.Contains("Envelope<UI.DamageEvent>", StringComparison.Ordinal));
    }

    /// <summary>验证全局命名空间不会泄漏 Roslyn 占位文本，包内泛型基类属性能还原枚举类型。</summary>
    [Fact]
    public async Task ScanAsyncResolvesGlobalAndInheritedGenericEnumTypes()
    {
        WriteYokiFrameAssembly();
        WriteSource("Assets/Scripts/GameStateTemplate.cs", """
            using YokiFrame;
            public enum GameState { Boot, Ready }
            public enum ApplyAwait { Start, End }
            public sealed class GameStateTemplate : AbstractState<GameState, object>
            {
                void Run()
                {
                    EventKit.Enum.Register<ApplyAwait, string>(ApplyAwait.Start, OnApply);
                    EventKit.Enum.Send(mFSM.CurEnum);
                    EventKit.Enum.Send<GameState, string>(mFSM.CurEnum, "ready");
                }
                void OnApply(string value) { }
            }
            """);

        WorkbenchEventKitCodeScan scan = await CreateService().ScanAsync(true, CancellationToken.None);

        Assert.DoesNotContain(scan.Relations, static relation =>
            relation.EventKey.Contains("global namespace", StringComparison.Ordinal));
        Assert.Contains(scan.Relations, static relation => relation.EventKey == "ApplyAwait.Start");
        Assert.Contains(scan.Relations, static relation =>
            relation.EventKey == "GameState" && relation.PayloadType.Length == 0);
        Assert.Contains(scan.Relations, static relation =>
            relation.EventKey == "GameState" && relation.PayloadType == "System.String");
    }

    /// <summary>验证 Unity Editor 预处理块中的 EventKit 调用会被静态扫描识别。</summary>
    [Fact]
    public async Task ScanAsyncEnablesUnityEditorPreprocessorSymbols()
    {
        WriteSource("Assets/Scripts/EditorSmoke.cs", """
            #if UNITY_EDITOR
            using YokiFrame;
            public sealed class EditorSmoke
            {
                void Run() => EventKit.Type.Send(new EditorEvent());
            }
            #endif
            """);

        WorkbenchEventKitCodeScan scan = await CreateService().ScanAsync(true, CancellationToken.None);

        WorkbenchEventKitCodeRelation relation = Assert.Single(scan.Relations);
        Assert.Equal("EditorEvent", relation.EventKey);
    }

    /// <summary>验证能够解析为其它命名空间同名类型的伪 EventKit 不进入结果。</summary>
    [Fact]
    public async Task ScanAsyncRejectsShadowEventKitType()
    {
        WriteSource("Assets/Scripts/Fake.cs", """
            namespace Fake
            {
                public static class EventKit
                {
                    public static Bus Type { get; } = new Bus();
                }
                public sealed class Bus { public void Send<T>(T value) { } }
            }
            public sealed class Demo
            {
                void Run() => Fake.EventKit.Type.Send(new object());
            }
            """);

        WorkbenchEventKitCodeScan scan = await CreateService().ScanAsync(true, CancellationToken.None);

        Assert.Empty(scan.Relations);
    }

    /// <summary>验证排除 Editor 只剪枝目录，不误伤文件名包含 Editor 的 Runtime 源码。</summary>
    [Fact]
    public async Task ScanAsyncExcludesEditorDirectoriesOnlyWhenRequested()
    {
        WriteSource("Assets/Editor/EditorEmitter.cs", "class EditorEmitter { void Run() => EventKit.Type.Send(new EditorOnlyEvent()); }");
        WriteSource("Assets/Scripts/RuntimeEditorState.cs", "class RuntimeEditorState { void Run() => EventKit.Type.Send(new RuntimeEvent()); }");

        WorkbenchEventKitCodeScan excluded = await CreateService().ScanAsync(true, CancellationToken.None);
        WorkbenchEventKitCodeScan included = await CreateService().ScanAsync(false, CancellationToken.None);

        Assert.Single(excluded.Relations);
        Assert.Equal("RuntimeEvent", excluded.Relations[0].EventKey);
        Assert.Equal(2, included.Relations.Count);
    }

    /// <summary>验证预取消请求不会返回半份扫描结果。</summary>
    [Fact]
    public async Task ScanAsyncHonorsPreCanceledToken()
    {
        WriteSource("Assets/Scripts/Demo.cs", "class Demo { void Run() => EventKit.Type.Send(new DemoEvent()); }");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().ScanAsync(true, cancellation.Token));
    }

    /// <summary>创建绑定当前临时项目的扫描服务。</summary>
    private EventKitCodeScanService CreateService()
    {
        return new EventKitCodeScanService(mProjectRoot);
    }

    /// <summary>在临时项目内写入指定相对路径的 C# 源码。</summary>
    private void WriteSource(string relativePath, string source)
    {
        string fullPath = Path.Combine(mProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, source);
    }

    /// <summary>生成只含 FSM 泛型继承链的目标项目程序集，模拟 Unity 包源码位于 Assets 之外。</summary>
    private void WriteYokiFrameAssembly()
    {
        const string source = """
            namespace YokiFrame
            {
                public sealed class FSM<TEnum>
                {
                    public TEnum CurEnum { get; }
                }
                public abstract class AbstractState<TEnum, TBlack>
                {
                    protected FSM<TEnum> mFSM;
                }
            }
            """;
        string outputPath = Path.Combine(mProjectRoot, "Library", "ScriptAssemblies", "YokiFrame.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "YokiFrame",
            new[] { CSharpSyntaxTree.ParseText(source) },
            CreatePlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var result = compilation.Emit(outputPath);
        Assert.True(result.Success, string.Join(System.Environment.NewLine, result.Diagnostics));
    }

    /// <summary>返回测试 Runtime 的可信平台引用，使伪程序集可独立编译。</summary>
    private static IEnumerable<MetadataReference> CreatePlatformReferences()
    {
        string trustedAssemblies = Assert.IsType<string>(
            AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"));
        return trustedAssemblies
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path));
    }

    /// <summary>删除本测试创建的临时项目。</summary>
    public void Dispose()
    {
        if (Directory.Exists(mProjectRoot))
        {
            Directory.Delete(mProjectRoot, true);
        }
    }
}
