#if UNITY_EDITOR
using System;
using System.Reflection;

#if UNITY_6000_5_OR_NEWER && !GODOT
using UnityEngine.Assemblies;
#endif

namespace YokiFrame.Unity
{
    internal static class TableKitLoadedAssemblyProvider
    {
        public static Assembly[] GetLoadedAssemblies()
        {
#if UNITY_6000_5_OR_NEWER && !GODOT
            return CurrentAssemblies.GetLoadedAssemblies();
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }
    }
}
#endif
