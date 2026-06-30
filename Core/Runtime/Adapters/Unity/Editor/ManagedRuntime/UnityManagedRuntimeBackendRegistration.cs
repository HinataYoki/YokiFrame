#if !GODOT
using UnityEditor;

namespace YokiFrame.Unity
{
    [InitializeOnLoad]
    public static class UnityManagedRuntimeBackendRegistration
    {
        static UnityManagedRuntimeBackendRegistration()
        {
            EnsureRegistered();
        }

        public static void EnsureRegistered()
        {
            ManagedRuntimeKit.RegisterBackend(new UnityLeanClrManagedRuntimeBackend());
        }
    }
}
#endif
