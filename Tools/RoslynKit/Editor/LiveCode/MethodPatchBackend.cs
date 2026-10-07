#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;
using System.Reflection;

namespace YokiFrame
{
    public sealed class MethodPatchBackend
    {
        private readonly string mDirectory;
        private readonly Func<string, Assembly> mLoadAssembly;
        private MethodInfo mApply;
        private MethodInfo mRemove;
        private bool mResolverInstalled;

        /// <summary>记录补丁包目录。不在构造时加载程序集。</summary>
        /// <param name="directory">包含补丁包和依赖 DLL 的目录。</param>
        public MethodPatchBackend(string directory) : this(directory, null) { }

        /// <summary>记录补丁包目录和宿主加载器。不在构造时加载程序集。</summary>
        /// <param name="directory">包含补丁包和依赖 DLL 的目录。</param>
        /// <param name="loadAssembly">程序集加载委托；null 时使用 <see cref="Assembly.LoadFrom"/>。</param>
        public MethodPatchBackend(string directory, Func<string, Assembly> loadAssembly)
        {
            mDirectory = directory;
            mLoadAssembly = loadAssembly;
        }

        /// <summary>检查补丁包是否已随包放置。只看文件存在，不在这里加载程序集。</summary>
        public bool Installed => File.Exists(Path.Combine(mDirectory, "YokiFrame.RoslynKit.Patching.dll"));

        /// <summary>按模式把静态补丁挂到目标方法。首次调用才加载补丁包。</summary>
        /// <param name="owner">补丁所有者标识。</param>
        /// <param name="original">被补丁方法。</param>
        /// <param name="patch">静态补丁方法。</param>
        /// <param name="mode">prefix、postfix 或 replace。</param>
        public void Apply(string owner, MethodInfo original, MethodInfo patch, string mode)
        {
            EnsureLoaded();
            Invoke(mApply, new object[] { owner, original, patch, mode });
        }

        /// <summary>卸下先前挂上的补丁。目标异常从调用包装中重新抛出。</summary>
        /// <param name="owner">补丁所有者标识。</param>
        /// <param name="original">被补丁方法。</param>
        /// <param name="patch">静态补丁方法。</param>
        public void Remove(string owner, MethodInfo original, MethodInfo patch)
        {
            EnsureLoaded();
            Invoke(mRemove, new object[] { owner, original, patch });
        }

        /// <summary>加载补丁包并缓存 Apply 与 Remove。只安装一次程序集解析回调。</summary>
        private void EnsureLoaded()
        {
            if (mApply != null) return;
            if (!Installed) throw new InvalidOperationException("Patch bundle missing: " + mDirectory);
            if (!mResolverInstalled)
            {
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                mResolverInstalled = true;
            }
            Type type = LoadBundle(Path.Combine(mDirectory, "YokiFrame.RoslynKit.Patching.dll"))
                .GetType("YokiFrame.RoslynKit.Patching.MethodPatcher", true);
            mApply = type.GetMethod("Apply");
            mRemove = type.GetMethod("Remove");
            if (mApply == null || mRemove == null)
                throw new InvalidOperationException("Patch bundle API is incompatible.");
        }

        /// <summary>只为补丁目录中的 Harmony 与 Cecil 依赖提供精确全名匹配。其他程序集返回 null。</summary>
        /// <param name="sender">触发解析的应用程序域，不使用。</param>
        /// <param name="args">请求的程序集身份。</param>
        /// <returns>匹配的已加载程序集；不负责时为 null。</returns>
        private Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            if (name.Name != "0Harmony" && name.Name != "Mono.Cecil"
                && !name.Name.StartsWith("Mono.Cecil.", StringComparison.Ordinal)
                && !name.Name.StartsWith("MonoMod.", StringComparison.Ordinal)) return null;
            string path = Path.Combine(mDirectory, name.Name + ".dll");
            if (!File.Exists(path)) return null;
            if (AssemblyName.GetAssemblyName(path).FullName != name.FullName) return null;
            // Unity 6.5 默认 LoadFrom 进入可回收上下文，Harmony 依赖随后返回 0x80131515。
            return LoadBundle(path);
        }

        /// <summary>按宿主委托加载补丁程序集；未提供委托时保留路径加载。</summary>
        /// <param name="path">程序集绝对路径。</param>
        /// <returns>已加载程序集。</returns>
        private Assembly LoadBundle(string path)
        {
            return mLoadAssembly == null ? Assembly.LoadFrom(path) : mLoadAssembly(path);
        }

        /// <summary>调用静态补丁 API。目标异常保留原栈重新抛出。</summary>
        /// <param name="method">Apply 或 Remove。</param>
        /// <param name="args">按补丁 API 排列的参数。</param>
        private static void Invoke(MethodInfo method, object[] args)
        {
            try { method.Invoke(null, args); }
            catch (TargetInvocationException exception)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(exception.InnerException ?? exception).Throw();
                throw;
            }
        }
    }
}
#endif
