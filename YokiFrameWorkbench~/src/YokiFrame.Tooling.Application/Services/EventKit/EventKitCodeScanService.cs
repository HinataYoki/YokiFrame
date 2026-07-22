using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;

namespace YokiFrame.Tooling.Application.Services.EventKit;

/// <summary>在项目 Assets 范围内执行可取消的 EventKit C# 静态关系扫描。</summary>
public sealed class EventKitCodeScanService
{
    private static readonly string[] sPreprocessorSymbols = { "UNITY_EDITOR" };
    private static readonly HashSet<string> sSkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "Library", "Temp", "obj", "bin", "target", "Build", "Builds"
    };
    private readonly string mProjectRoot;
    private readonly string mScanRoot;

    /// <summary>创建绑定到单个规范化项目根的扫描服务。</summary>
    public EventKitCodeScanService(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("Project root is required.", nameof(projectRoot));
        }

        mProjectRoot = Path.GetFullPath(projectRoot);
        string assetsRoot = Path.Combine(mProjectRoot, "Assets");
        if (!Directory.Exists(assetsRoot))
        {
            throw new DirectoryNotFoundException("EventKit code scan requires a project Assets directory.");
        }

        mScanRoot = assetsRoot;
    }

    /// <summary>异步扫描全部允许的 C# 文件，并返回强类型聚合关系。</summary>
    public Task<WorkbenchEventKitCodeScan> ScanAsync(
        bool excludeEditor,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => ScanCoreAsync(excludeEditor, cancellationToken),
            cancellationToken);
    }

    /// <summary>在后台线程完成目录枚举、文件读取与 Roslyn 解析，避免阻塞 Workbench UI。</summary>
    private async Task<WorkbenchEventKitCodeScan> ScanCoreAsync(
        bool excludeEditor,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<string> paths = EnumerateSourceFiles(excludeEditor, cancellationToken);
        List<EventKitCodeSourceFile> files = new(paths.Count);
        for (var index = 0; index < paths.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = paths[index];
            string source = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            string relativePath = Path.GetRelativePath(mProjectRoot, fullPath).Replace('\\', '/');
            var syntaxTree = (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(
                source,
                new CSharpParseOptions(
                    LanguageVersion.CSharp9,
                    preprocessorSymbols: sPreprocessorSymbols),
                relativePath,
                cancellationToken: cancellationToken);
            files.Add(new EventKitCodeSourceFile(relativePath, syntaxTree));
        }

        HashSet<string> matchedFiles = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<MetadataReference> projectReferences = CreateProjectReferences(cancellationToken);
        IReadOnlyList<WorkbenchEventKitCodeRelation> relations =
            EventKitCSharpUsageParser.Parse(files, projectReferences, matchedFiles, cancellationToken);
        stopwatch.Stop();
        return new WorkbenchEventKitCodeScan(
            mProjectRoot,
            excludeEditor,
            files.Count,
            matchedFiles.Count,
            stopwatch.Elapsed,
            relations);
    }

    /// <summary>以显式目录栈枚举源码，使 Editor 与缓存目录在进入前即可剪枝。</summary>
    private List<string> EnumerateSourceFiles(bool excludeEditor, CancellationToken cancellationToken)
    {
        List<string> files = new();
        Stack<string> pending = new();
        pending.Push(mScanRoot);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string current = pending.Pop();
            foreach (string directory in Directory.EnumerateDirectories(current))
            {
                if (!ShouldSkipDirectory(directory, excludeEditor))
                {
                    pending.Push(directory);
                }
            }

            foreach (string file in Directory.EnumerateFiles(current, "*.cs", SearchOption.TopDirectoryOnly))
            {
                files.Add(Path.GetFullPath(file));
            }
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    /// <summary>判断目录是否属于缓存、构建产物或用户要求排除的 Editor 范围。</summary>
    private static bool ShouldSkipDirectory(string directory, bool excludeEditor)
    {
        string name = Path.GetFileName(directory);
        return sSkippedDirectories.Contains(name)
            || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0
            || (excludeEditor && string.Equals(name, "Editor", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>加载目标 Unity 项目已编译的 YokiFrame 程序集，使包内继承成员可参与语义推断。</summary>
    private IReadOnlyList<MetadataReference> CreateProjectReferences(CancellationToken cancellationToken)
    {
        if (File.Exists(Path.Combine(mScanRoot, "YokiFrame", "package.json")))
        {
            return Array.Empty<MetadataReference>();
        }

        string assembliesRoot = Path.Combine(mProjectRoot, "Library", "ScriptAssemblies");
        if (!Directory.Exists(assembliesRoot))
        {
            return Array.Empty<MetadataReference>();
        }

        List<MetadataReference> references = new();
        foreach (string path in Directory.EnumerateFiles(assembliesRoot, "YokiFrame*.dll"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryCreateMetadataReference(path, out MetadataReference reference))
            {
                references.Add(reference);
            }
        }

        return references;
    }

    /// <summary>尝试读取可选编译上下文；Unity 正在替换程序集时跳过该引用并保持扫描可用。</summary>
    private static bool TryCreateMetadataReference(string path, out MetadataReference reference)
    {
        try
        {
            reference = MetadataReference.CreateFromFile(path);
            return true;
        }
        catch (IOException)
        {
            reference = null!;
            return false;
        }
        catch (BadImageFormatException)
        {
            reference = null!;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reference = null!;
            return false;
        }
    }
}
