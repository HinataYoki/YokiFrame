#if !GODOT
namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity AudioKit 默认后端安装器。
    /// </summary>
    [YokiFrameKitDiscoverableInstaller(YokiFrameEngine.Unity)]
    public sealed class UnityAudioKitInstaller : IYokiFrameKitInstaller
    {
        private UnityAudioKitBackend mBackend;

        public string KitName
        {
            get { return "Unity.AudioKit"; }
        }

        public void Install(YokiFrameEngineContext context)
        {
            if (context.Engine != YokiFrameEngine.Unity)
                return;

            mBackend = new UnityAudioKitBackend();
            AudioKit.SetBackend(mBackend);

            var resourceProvider = context.GetService<IResourceProvider>();
            if (resourceProvider != null)
                AudioKit.SetResourceLoader(new ResourceProviderAudioResourceLoader(resourceProvider));
        }

        public bool Tick(float deltaSeconds)
        {
            return true;
        }

        public void Shutdown()
        {
            if (mBackend != null)
            {
                mBackend.Dispose();
                mBackend = null;
            }

            AudioKit.ClearBackend();
        }
    }
}
#endif
