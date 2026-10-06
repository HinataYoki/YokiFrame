#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace YokiFrame
{
    /// <summary>Loads a project-local, bundled compiler. Never downloads or invokes external tools.</summary>
    public sealed class YokiFrameRoslynCompilerLoader : IDisposable
    {
        public const string RELATIVE_PATH = ".yokiframe/automation/compiler/roslyn-4.8.0";
        private readonly string mDirectory;
        private readonly string mReferenceDirectory;
        private readonly Func<byte[], byte[], Assembly> mLoadAssembly;
        private MethodInfo mCompile;
        private readonly object mLoadLock = new object();
        private bool mResolverInstalled;
        private bool mDisposed;
        private static readonly string[] sDependencies =
        {
            "Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp", "System.Collections.Immutable",
            "System.Reflection.Metadata", "System.Memory", "System.Runtime.CompilerServices.Unsafe",
            "System.Buffers", "System.Threading.Tasks.Extensions", "System.Text.Encoding.CodePages",
            "System.Numerics.Vectors"
        };

        public YokiFrameRoslynCompilerLoader(string projectRoot, string referenceDirectory = null,
            Func<byte[], byte[], Assembly> loadAssembly = null)
        {
            mDirectory = Path.Combine(projectRoot, RELATIVE_PATH.Replace('/', Path.DirectorySeparatorChar));
            mReferenceDirectory = referenceDirectory;
            mLoadAssembly = loadAssembly;
        }

        public string[] CaptureReferences() => YokiFrameEngineScriptOperations.CaptureReferences(mReferenceDirectory);
        public Assembly LoadAssembly(byte[] pe, byte[] symbols) =>
            mLoadAssembly == null ? Assembly.Load(pe, symbols) : mLoadAssembly(pe, symbols);

        public bool Installed
        {
            get { return File.Exists(Path.Combine(mDirectory, "YokiFrame.EngineKit.Roslyn.dll")); }
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
                if (!Installed) throw new InvalidOperationException("Roslyn compiler bundle is missing: " + mDirectory);
                if (!mResolverInstalled)
                {
                    AppDomain.CurrentDomain.AssemblyResolve += ResolveDependency;
                    mResolverInstalled = true;
                }
                Assembly compiler = Assembly.LoadFrom(Path.Combine(mDirectory, "YokiFrame.EngineKit.Roslyn.dll"));
                mCompile = compiler.GetType("YokiFrame.EngineKit.Roslyn.MemoryCompiler", true).GetMethod("Compile");
                if (mCompile == null) throw new InvalidOperationException("Compiler bundle has an incompatible API.");
            }
        }

        private Assembly ResolveDependency(object sender, ResolveEventArgs args)
        {
            var requested = new AssemblyName(args.Name);
            if (Array.IndexOf(sDependencies, requested.Name) < 0) return null;
            string path = Path.Combine(mDirectory, requested.Name + ".dll");
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
            }
        }
    }
}
#endif
