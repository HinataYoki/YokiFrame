#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// 按候选目录顺序加载编译器 bundle：包内 <c>Dependencies~</c> 优先，其次是项目本地缓存。
    /// 只使用调用方给出的候选路径，不下载也不调用外部工具。
    /// </summary>
    public sealed class YokiFrameRoslynCompilerLoader : IDisposable
    {
        /// <summary>随包分发的编译器 bundle 相对包根的路径，装完即可用。</summary>
        public const string PACKAGE_RELATIVE_PATH =
            "Core/Adapters/Unity/Editor/EngineKit/Dependencies~/roslyn-4.8.0";

        /// <summary>项目本地编译器 bundle 相对工程根的路径；保留给自定义或离线场景。</summary>
        public const string RELATIVE_PATH = ".yokiframe/automation/compiler/roslyn-4.8.0";

        private const string COMPILER_ASSEMBLY_NAME = "YokiFrame.EngineKit.Roslyn.dll";

        private readonly IReadOnlyList<string> mCandidateDirectories;
        private readonly string mReferenceDirectory;
        private readonly Func<byte[], byte[], Assembly> mLoadAssembly;
        private MethodInfo mCompile;
        private volatile string mResolvedDirectory;
        private readonly object mLoadLock = new object();
        private bool mResolverInstalled;
        private bool mDisposed;
        private const int MAX_CACHED_SCRIPTS = 128;
        private readonly Dictionary<string, CompiledScript> mScripts = new Dictionary<string, CompiledScript>(StringComparer.Ordinal);
        private readonly Queue<string> mScriptOrder = new Queue<string>();
        private long mScriptCacheHits;
        private long mScriptCacheMisses;

        internal sealed class CompiledScript
        {
            internal Func<YokiFrameAutomationContext, YokiFrameAutomationAssertions, CancellationToken, Task> Run;
            internal string Diagnostics;
        }

        public int CachedScripts { get { lock (mLoadLock) return mScripts.Count; } }
        public int MaxCachedScripts => MAX_CACHED_SCRIPTS;
        public long ScriptCacheHits { get { lock (mLoadLock) return mScriptCacheHits; } }
        public long ScriptCacheMisses { get { lock (mLoadLock) return mScriptCacheMisses; } }

        internal string ScriptKey(string source, string[] references)
        {
            // Preserve reference order: it can affect compiler binding. Metadata changes invalidate reuse.
            var key = new StringBuilder(YokiFrameLiveCodeManager.Hash(source));
            foreach (string path in references)
            {
                var file = new FileInfo(path);
                key.Append('|').Append(file.FullName.Length).Append(':').Append(file.FullName)
                    .Append(':').Append(file.Length).Append(':').Append(file.LastWriteTimeUtc.Ticks);
            }
            return YokiFrameLiveCodeManager.Hash(key.ToString());
        }

        internal bool TryGetScript(string key, out CompiledScript script)
        {
            lock (mLoadLock)
            {
                if (mDisposed) throw new ObjectDisposedException(nameof(YokiFrameRoslynCompilerLoader));
                if (mScripts.TryGetValue(key, out script)) { mScriptCacheHits++; return true; }
                mScriptCacheMisses++;
                return false;
            }
        }

        internal CompiledScript LoadScript(string key, byte[] pe, byte[] symbols, string diagnostics, YokiFrameRoslynLoadBudget budget)
        {
            lock (mLoadLock)
            {
                if (mDisposed) throw new ObjectDisposedException(nameof(YokiFrameRoslynCompilerLoader));
                // Concurrent first submissions may compile together; only the first loads an assembly.
                if (mScripts.TryGetValue(key, out var cached)) { mScriptCacheHits++; return cached; }
                budget.Reserve(pe.LongLength + symbols.LongLength);
                Assembly assembly = LoadAssembly(pe, symbols);
                MethodInfo method = assembly.GetType("YokiFrame.Automation.Script", true)
                    .GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                var script = new CompiledScript
                {
                    Run = (Func<YokiFrameAutomationContext, YokiFrameAutomationAssertions, CancellationToken, Task>)
                        Delegate.CreateDelegate(typeof(Func<YokiFrameAutomationContext, YokiFrameAutomationAssertions, CancellationToken, Task>), method),
                    Diagnostics = diagnostics
                };
                if (mScripts.Count == MAX_CACHED_SCRIPTS) mScripts.Remove(mScriptOrder.Dequeue());
                mScripts.Add(key, script);
                mScriptOrder.Enqueue(key);
                return script;
            }
        }
        private static readonly string[] sDependencies =
        {
            "Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp", "System.Collections.Immutable",
            "System.Reflection.Metadata", "System.Memory", "System.Runtime.CompilerServices.Unsafe",
            "System.Buffers", "System.Threading.Tasks.Extensions", "System.Text.Encoding.CodePages",
            "System.Numerics.Vectors"
        };

        public YokiFrameRoslynCompilerLoader(string projectRoot, string referenceDirectory = null,
            Func<byte[], byte[], Assembly> loadAssembly = null)
            : this(projectRoot, Array.Empty<string>(), referenceDirectory, loadAssembly)
        {
        }

        /// <summary>
        /// 建立编译器加载器；候选目录按顺序尝试，第一个含编译器入口的目录生效。
        /// </summary>
        /// <param name="projectRoot">工程根；<see cref="RELATIVE_PATH"/> 以它为基准。</param>
        /// <param name="compilerDirectories">优先尝试的编译器目录（通常为包内 bundle）。</param>
        /// <param name="referenceDirectory">编译引用程序集目录；null 时由调用方另行提供。</param>
        /// <param name="loadAssembly">PE/PDB 加载委托；null 时使用 <see cref="Assembly.Load(byte[], byte[])"/>。</param>
        public YokiFrameRoslynCompilerLoader(string projectRoot, IReadOnlyList<string> compilerDirectories,
            string referenceDirectory = null,
            Func<byte[], byte[], Assembly> loadAssembly = null)
        {
            mCandidateDirectories = BuildCandidates(projectRoot, compilerDirectories);
            mReferenceDirectory = referenceDirectory;
            mLoadAssembly = loadAssembly;
        }

        /// <summary>
        /// 按顺序拼装候选目录：调用方显式给出的目录在前，项目本地缓存兜底。
        /// </summary>
        /// <param name="projectRoot">工程根。</param>
        /// <param name="compilerDirectories">调用方给出的优先目录；可为 null。</param>
        /// <returns>去重后的候选目录数组。</returns>
        private static string[] BuildCandidates(string projectRoot, IReadOnlyList<string> compilerDirectories)
        {
            var candidates = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (compilerDirectories != null)
            {
                for (var index = 0; index < compilerDirectories.Count; index++)
                {
                    AddCandidate(candidates, seen, compilerDirectories[index]);
                }
            }

            AddCandidate(candidates, seen, Path.Combine(projectRoot,
                RELATIVE_PATH.Replace('/', Path.DirectorySeparatorChar)));
            return candidates.ToArray();
        }

        /// <summary>
        /// 归一化并追加一个候选目录；空白、无效与重复项被忽略。
        /// </summary>
        /// <param name="candidates">候选列表。</param>
        /// <param name="seen">已见目录集合。</param>
        /// <param name="directory">待追加目录。</param>
        private static void AddCandidate(List<string> candidates, HashSet<string> seen, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return;
            string full;
            try
            {
                full = Path.GetFullPath(directory);
            }
            catch (ArgumentException)
            {
                return;
            }
            catch (NotSupportedException)
            {
                return;
            }
            catch (PathTooLongException)
            {
                return;
            }

            if (seen.Add(full)) candidates.Add(full);
        }

        public string[] CaptureReferences() => YokiFrameEngineScriptOperations.CaptureReferences(mReferenceDirectory);
        public Assembly LoadAssembly(byte[] pe, byte[] symbols) =>
            mLoadAssembly == null ? Assembly.Load(pe, symbols) : mLoadAssembly(pe, symbols);

        /// <summary>
        /// 获取是否至少有一个候选目录带有编译器入口程序集。
        /// </summary>
        public bool Installed
        {
            get { return ResolveDirectory() != null; }
        }

        /// <summary>
        /// 按候选顺序解析首个含编译器入口的目录并缓存；失败时缓存为空以允许后续重试。
        /// </summary>
        /// <returns>生效的编译器目录；候选均不可用时返回 null。</returns>
        private string ResolveDirectory()
        {
            string cached = mResolvedDirectory;
            if (cached != null) return cached;
            lock (mLoadLock)
            {
                if (mDisposed) return null;
                if (mResolvedDirectory != null) return mResolvedDirectory;
                for (var index = 0; index < mCandidateDirectories.Count; index++)
                {
                    string candidate = mCandidateDirectories[index];
                    if (File.Exists(Path.Combine(candidate, COMPILER_ASSEMBLY_NAME)))
                    {
                        mResolvedDirectory = candidate;
                        return candidate;
                    }
                }

                return null;
            }
        }

        public byte[] Compile(string source, string[] references, CancellationToken token,
            out byte[] symbols, out string[][] diagnostics)
        {
            MethodInfo compile;
            lock (mLoadLock)
            {
                if (mDisposed) throw new ObjectDisposedException(nameof(YokiFrameRoslynCompilerLoader));
                EnsureLoaded();
                compile = mCompile;
            }
            object[] args = { source, references, token, null, null };
            byte[] result;
            try { result = (byte[])compile.Invoke(null, args); }
            catch (TargetInvocationException exception)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException ?? exception).Throw();
                throw;
            }
            symbols = (byte[])args[3];
            diagnostics = (string[][])args[4];
            return result;
        }

        private void EnsureLoaded()
        {
            lock (mLoadLock)
            {
                if (mCompile != null) return;
                string directory = ResolveDirectory();
                if (directory == null)
                {
                    throw new InvalidOperationException(
                        "Roslyn compiler bundle is missing. Checked: " + string.Join(", ", mCandidateDirectories));
                }

                if (!mResolverInstalled)
                {
                    AppDomain.CurrentDomain.AssemblyResolve += ResolveDependency;
                    mResolverInstalled = true;
                }
                Assembly compiler = Assembly.LoadFrom(Path.Combine(directory, COMPILER_ASSEMBLY_NAME));
                mCompile = compiler.GetType("YokiFrame.EngineKit.Roslyn.MemoryCompiler", true).GetMethod("Compile");
                if (mCompile == null) throw new InvalidOperationException("Compiler bundle has an incompatible API.");
            }
        }

        private Assembly ResolveDependency(object sender, ResolveEventArgs args)
        {
            var requested = new AssemblyName(args.Name);
            if (Array.IndexOf(sDependencies, requested.Name) < 0) return null;

            // 只在加载编译器期间按候选目录解析，避免把解析委托长期暴露给无关程序集。
            string resolved = mResolvedDirectory;
            if (resolved == null) return null;
            string path = Path.Combine(resolved, requested.Name + ".dll");
            if (!File.Exists(path)) return null;
            AssemblyName bundled = AssemblyName.GetAssemblyName(path);
            if (bundled.FullName != requested.FullName) return null;
            return Assembly.LoadFrom(path);
        }

        public void Dispose()
        {
            lock (mLoadLock)
            {
                mDisposed = true;
                if (mResolverInstalled) AppDomain.CurrentDomain.AssemblyResolve -= ResolveDependency;
                mResolverInstalled = false;
                mCompile = null;
                mScripts.Clear();
                mScriptOrder.Clear();
            }
        }
    }
}
#endif
