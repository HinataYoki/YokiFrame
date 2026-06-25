#if GODOT
namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot AudioKit 默认后端安装器。
    /// </summary>
    [YokiFrameKitDiscoverableInstaller(YokiFrameEngine.Godot)]
    public sealed class GodotAudioKitInstaller : IYokiFrameKitInstaller
    {
        private GodotAudioKitBackend mBackend;

        public string KitName
        {
            get { return "Godot.AudioKit"; }
        }

        public void Install(YokiFrameEngineContext context)
        {
            if (context.Engine != YokiFrameEngine.Godot)
                return;

            mBackend = new GodotAudioKitBackend();
            AudioKit.SetBackend(mBackend);

            var resourceProvider = context.GetService<IResourceProvider>();
            if (resourceProvider != null)
                AudioKit.SetResourceLoader(new ResourceProviderAudioResourceLoader(resourceProvider));
        }

        public bool Tick(float deltaSeconds)
        {
            AudioKit.Update(deltaSeconds);
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
