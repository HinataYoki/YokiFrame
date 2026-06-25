using System;

namespace YokiFrame
{
    /// <summary>
    /// 纯 C# 单例的中心管理器，不依赖 MonoBehaviour。
    /// 负责实例创建、缓存和生命周期控制。
    /// MonoBehaviour 单例由 Unity Adapter 的 MonoSingleton 提供支持。
    /// </summary>
    public static class SingletonKit<T> where T : class, ISingleton
    {
        /// <summary>
        /// 缓存的单例实例。
        /// </summary>
        private static T sInstance;

        /// <summary>
        /// 懒初始化期间使用的同步锁。
        /// </summary>
        private static readonly object sLock = new();

        /// <summary>
        /// 使用线程安全的懒初始化获取单例实例。
        /// </summary>
        public static T Instance
        {
            get
            {
                if (sInstance is null)
                {
                    lock (sLock)
                    {
                        sInstance ??= CreateInstance();
                    }
                }
                return sInstance;
            }
        }

        /// <summary>
        /// 清除缓存的实例引用。
        /// </summary>
        public static void Dispose()
        {
            SingletonRegistry.Unregister(typeof(T));
            sInstance = null;
        }

        /// <summary>
        /// 通过反射创建纯 C# 单例实例。
        /// </summary>
        private static T CreateInstance()
        {
            var type = typeof(T);

            T instance;
            try
            {
                instance = Activator.CreateInstance(type, true) as T;
            }
            catch (MissingMethodException)
            {
                throw new InvalidOperationException(
                    $"[SingletonKit] Type {type.Name} must have a parameterless constructor. " +
                    "For IL2CPP builds, ensure the type is explicitly referenced or marked with [Preserve].");
            }

            if (instance == null)
                throw new InvalidOperationException("[SingletonKit] Failed to create singleton instance for type " + type.Name + ".");

            instance.OnSingletonInit();
            SingletonRegistry.Register(type, instance, "Base", "SingletonKit");
            return instance;
        }
    }
}
