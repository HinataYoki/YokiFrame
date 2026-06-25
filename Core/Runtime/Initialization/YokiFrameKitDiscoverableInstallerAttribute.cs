using System;

namespace YokiFrame
{
    /// <summary>
    /// 标记可由 YokiFrameKit 在初始化时自动发现的 Kit installer。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class YokiFrameKitDiscoverableInstallerAttribute : Attribute
    {
        public YokiFrameKitDiscoverableInstallerAttribute(YokiFrameEngine engine)
        {
            Engine = engine;
        }

        public YokiFrameEngine Engine { get; private set; }
    }
}
