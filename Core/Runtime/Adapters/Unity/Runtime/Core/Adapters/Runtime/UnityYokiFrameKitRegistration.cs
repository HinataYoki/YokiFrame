#if !GODOT
namespace YokiFrame.Unity
{
    internal static class UnityYokiFrameKitRegistration
    {
        private static UnityCoreKitInstaller sCoreInstaller;

        public static void EnsureRegistered(UnityLogKitOptions logKitOptions, IEngineLogger logger)
        {
            sCoreInstaller = new UnityCoreKitInstaller(logKitOptions, logger);
            YokiFrameKit.RegisterInstaller(sCoreInstaller);
        }
    }
}
#endif
