#if !GODOT
using YokiFrame;
#if YOKIFRAME_INPUTSYSTEM_SUPPORT
using UnityEngine.InputSystem;
#endif
using InputKitApi = YokiFrame.InputKit;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 将 Unity 输入后端注入 InputKit，保持跨引擎静态入口一致。
    /// </summary>
    public static class UnityInputKitInstaller
    {
        private static UnityInputBackend sBackend;

        public static void Install(IResourceProvider provider)
        {
            sBackend = new UnityInputBackend();
            InputKitApi.SetBackend(sBackend);
        }

        public static bool Tick(float deltaSeconds)
        {
            InputKitApi.Update(UnityEngine.Time.unscaledTime);
            return true;
        }

        public static void Dispose()
        {
            if (sBackend != null)
            {
                var disposable = sBackend as System.IDisposable;
                if (disposable != null)
                    disposable.Dispose();
                sBackend = null;
            }

            InputKitApi.ClearBackend();
        }

        public static UnityInputBackend GetBackend()
        {
            return sBackend;
        }

#if YOKIFRAME_INPUTSYSTEM_SUPPORT
        public static void Register<T>() where T : class, IInputActionCollection2, new()
        {
            EnsureBackend().Register<T>();
        }

        public static void Register<T>(T instance) where T : class, IInputActionCollection2
        {
            EnsureBackend().Register(instance);
        }

        public static T Get<T>() where T : class, IInputActionCollection2
        {
            return EnsureBackend().Get<T>();
        }

        public static void SetActionAsset(InputActionAsset actionAsset)
        {
            EnsureBackend().SetActionAsset(actionAsset);
        }
#endif

        private static UnityInputBackend EnsureBackend()
        {
            if (sBackend == null)
            {
                sBackend = new UnityInputBackend();
                InputKitApi.SetBackend(sBackend);
            }

            return sBackend;
        }
    }
}
#endif
