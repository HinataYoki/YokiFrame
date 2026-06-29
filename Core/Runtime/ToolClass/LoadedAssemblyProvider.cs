using System;
using System.Reflection;

namespace YokiFrame
{
    public static class LoadedAssemblyProvider
    {
#if UNITY_6000_5_OR_NEWER && !GODOT
        private static readonly Func<Assembly[]> sUnityGetLoadedAssemblies = CreateUnityGetLoadedAssemblies();
#endif

        public static Assembly[] GetLoadedAssemblies()
        {
#if UNITY_6000_5_OR_NEWER && !GODOT
            return sUnityGetLoadedAssemblies();
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }

#if UNITY_6000_5_OR_NEWER && !GODOT
        private static Func<Assembly[]> CreateUnityGetLoadedAssemblies()
        {
            var currentAssembliesType = Type.GetType("UnityEngine.Assemblies.CurrentAssemblies, UnityEngine.CoreModule");
            var getLoadedAssembliesMethod = currentAssembliesType != null
                ? currentAssembliesType.GetMethod("GetLoadedAssemblies", BindingFlags.Public | BindingFlags.Static)
                : null;

            if (getLoadedAssembliesMethod == null)
                throw new InvalidOperationException("UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies is unavailable.");

            return (Func<Assembly[]>)Delegate.CreateDelegate(typeof(Func<Assembly[]>), getLoadedAssembliesMethod);
        }
#endif
    }
}
