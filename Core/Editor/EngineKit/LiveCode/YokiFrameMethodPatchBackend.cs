#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;
using System.Reflection;

namespace YokiFrame
{
    public sealed class YokiFrameMethodPatchBackend
    {
        private readonly string mDirectory;
        private MethodInfo mApply;
        private MethodInfo mRemove;
        private bool mResolverInstalled;

        public YokiFrameMethodPatchBackend(string directory) { mDirectory = directory; }
        public bool Installed => File.Exists(Path.Combine(mDirectory, "YokiFrame.EngineKit.Patching.dll"));

        public void Apply(string owner, MethodInfo original, MethodInfo patch, string mode)
        {
            EnsureLoaded();
            Invoke(mApply, new object[] { owner, original, patch, mode });
        }

        public void Remove(string owner, MethodInfo original, MethodInfo patch)
        {
            EnsureLoaded();
            Invoke(mRemove, new object[] { owner, original, patch });
        }

        private void EnsureLoaded()
        {
            if (mApply != null) return;
            if (!Installed) throw new InvalidOperationException("Patch bundle missing: " + mDirectory);
            if (!mResolverInstalled)
            {
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                mResolverInstalled = true;
            }
            Type type = Assembly.LoadFrom(Path.Combine(mDirectory, "YokiFrame.EngineKit.Patching.dll"))
                .GetType("YokiFrame.EngineKit.Patching.MethodPatcher", true);
            mApply = type.GetMethod("Apply");
            mRemove = type.GetMethod("Remove");
            if (mApply == null || mRemove == null)
                throw new InvalidOperationException("Patch bundle API is incompatible.");
        }

        private Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            if (name.Name != "0Harmony" && name.Name != "Mono.Cecil"
                && !name.Name.StartsWith("Mono.Cecil.", StringComparison.Ordinal)
                && !name.Name.StartsWith("MonoMod.", StringComparison.Ordinal)) return null;
            string path = Path.Combine(mDirectory, name.Name + ".dll");
            if (!File.Exists(path)) return null;
            if (AssemblyName.GetAssemblyName(path).FullName != name.FullName) return null;
            return Assembly.LoadFrom(path);
        }

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
