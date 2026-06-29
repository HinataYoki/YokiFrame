using System;
using System.Collections.Generic;
using System.Reflection;

namespace YokiFrame
{
    /// <summary>
    /// YokiFrame 统一初始化入口。宿主只声明当前引擎，具体 Kit 默认后端由 Kit installer 自己接入。
    /// </summary>
    public static class YokiFrameKit
    {
        private static readonly object sLock = new object();
        private static readonly List<IYokiFrameKitInstaller> sInstallers = new List<IYokiFrameKitInstaller>();
        private static readonly HashSet<string> sDiscoveredInstallerKeys = new HashSet<string>(StringComparer.Ordinal);
        private static YokiFrameRuntime sRuntime;

        public static bool IsInitialized
        {
            get
            {
                lock (sLock)
                    return sRuntime != null;
            }
        }

        public static YokiFrameEngine CurrentEngine
        {
            get
            {
                lock (sLock)
                    return sRuntime != null ? sRuntime.Context.Engine : YokiFrameEngine.Unknown;
            }
        }

        public static IYokiFrameRuntime CurrentRuntime
        {
            get
            {
                lock (sLock)
                    return sRuntime;
            }
        }

        public static IYokiFrameRuntime Initialize(YokiFrameEngine engine)
        {
            EnsureEngineDefaultInstallers(engine);
            return Initialize(new YokiFrameEngineContext(engine));
        }

        public static IYokiFrameRuntime Initialize(YokiFrameEngineContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            YokiFrameRuntime previousRuntime;
            lock (sLock)
            {
                previousRuntime = sRuntime;
                sRuntime = null;
            }

            if (previousRuntime != null)
                previousRuntime.Shutdown();

            IYokiFrameKitInstaller[] installers;
            lock (sLock)
                installers = sInstallers.ToArray();

            var installedInstallers = new List<IYokiFrameKitInstaller>(installers.Length);
            for (var i = 0; i < installers.Length; i++)
            {
                var installer = installers[i];
                if (installer == null)
                    continue;

                installer.Install(context);
                installedInstallers.Add(installer);
            }

            var runtime = new YokiFrameRuntime(context, installedInstallers);
            lock (sLock)
                sRuntime = runtime;

            return runtime;
        }

        public static void RegisterInstaller(IYokiFrameKitInstaller installer)
        {
            if (installer == null)
                return;

            lock (sLock)
            {
                for (var i = 0; i < sInstallers.Count; i++)
                {
                    if (ReferenceEquals(sInstallers[i], installer))
                        return;

                    if (string.Equals(sInstallers[i].KitName, installer.KitName, StringComparison.Ordinal))
                        return;
                }

                sInstallers.Add(installer);
            }
        }

        public static void Tick(float deltaSeconds)
        {
            IYokiFrameRuntime runtime;
            lock (sLock)
                runtime = sRuntime;

            if (runtime != null)
                runtime.Tick(deltaSeconds);
        }

        public static void Shutdown()
        {
            YokiFrameRuntime runtime;
            lock (sLock)
            {
                runtime = sRuntime;
                sRuntime = null;
            }

            if (runtime != null)
                runtime.Shutdown();
        }

        internal static void ClearInstallersForTests()
        {
            lock (sLock)
            {
                sInstallers.Clear();
                sDiscoveredInstallerKeys.Clear();
            }
        }

        private static void EnsureEngineDefaultInstallers(YokiFrameEngine engine)
        {
            DiscoverInstallers(engine);
        }

        private static void DiscoverInstallers(YokiFrameEngine engine)
        {
            var assemblies = LoadedAssemblyProvider.GetLoadedAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                if (types == null)
                    continue;

                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var type = types[typeIndex];
                    if (type == null || type.IsAbstract || !typeof(IYokiFrameKitInstaller).IsAssignableFrom(type))
                        continue;

                    var attributes = type.GetCustomAttributes(typeof(YokiFrameKitDiscoverableInstallerAttribute), false);
                    if (attributes == null || attributes.Length == 0)
                        continue;

                    if (!SupportsEngine(attributes, engine))
                        continue;

                    RegisterDiscoveredInstaller(type);
                }
            }
        }

        private static bool SupportsEngine(object[] attributes, YokiFrameEngine engine)
        {
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i] as YokiFrameKitDiscoverableInstallerAttribute;
                if (attribute != null && attribute.Engine == engine)
                    return true;
            }

            return false;
        }

        private static void RegisterDiscoveredInstaller(Type type)
        {
            var key = type.AssemblyQualifiedName;
            lock (sLock)
            {
                if (!sDiscoveredInstallerKeys.Add(key))
                    return;
            }

            var installer = Activator.CreateInstance(type, true) as IYokiFrameKitInstaller;
            RegisterInstaller(installer);
        }

        private sealed class YokiFrameRuntime : IYokiFrameRuntime
        {
            private readonly List<IYokiFrameKitInstaller> mInstallers;
            private bool mShutdown;

            public YokiFrameRuntime(YokiFrameEngineContext context, List<IYokiFrameKitInstaller> installers)
            {
                Context = context;
                mInstallers = installers;
            }

            public YokiFrameEngineContext Context { get; private set; }

            public void Tick(float deltaSeconds)
            {
                if (mShutdown)
                    return;

                for (var i = 0; i < mInstallers.Count; i++)
                    mInstallers[i].Tick(deltaSeconds);
            }

            public void Shutdown()
            {
                if (mShutdown)
                    return;

                mShutdown = true;
                for (var i = mInstallers.Count - 1; i >= 0; i--)
                    mInstallers[i].Shutdown();

                lock (sLock)
                {
                    if (ReferenceEquals(sRuntime, this))
                        sRuntime = null;
                }
            }
        }
    }
}
