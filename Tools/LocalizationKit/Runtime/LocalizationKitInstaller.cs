namespace YokiFrame
{
    /// <summary>
    /// LocalizationKit 默认运行时安装器。没有项目 Provider 时提供一个空内存 Provider 作为安全默认值。
    /// </summary>
    [YokiFrameKitDiscoverableInstaller(YokiFrameEngine.Unity)]
    [YokiFrameKitDiscoverableInstaller(YokiFrameEngine.Godot)]
    public sealed class LocalizationKitInstaller : IYokiFrameKitInstaller
    {
        public string KitName
        {
            get { return "LocalizationKit"; }
        }

        public void Install(YokiFrameEngineContext context)
        {
            if (LocalizationKit.GetProvider() == null)
                LocalizationKit.SetProvider(new MemoryLocalizationProvider());

            if (LocalizationKit.GetFormatter() == null)
                LocalizationKit.SetFormatter(new DefaultTextFormatter());
        }

        public bool Tick(float deltaSeconds)
        {
            return true;
        }

        public void Shutdown()
        {
        }
    }
}
