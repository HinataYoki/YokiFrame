using System;
using System.Reflection;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 纯 C# 单例的中心管理器，负责实例创建、缓存和生命周期登记。
    /// </summary>
    /// <typeparam name="T">单例类型。</typeparam>
    public static class SingletonKit<T> where T : class, ISingleton
    {
        private static readonly object sLock = new();
        private static T sInstance;
        private static T sConstructing;

        /// <summary>
        /// 使用线程安全懒初始化获取单例实例；实例仅在初始化完成后对锁外线程可见。
        /// </summary>
        public static T Instance
        {
            get
            {
                T fast = Volatile.Read(ref sInstance);
                if (fast != null)
                {
                    return fast;
                }

                lock (sLock)
                {
                    if (sInstance != null)
                    {
                        return sInstance;
                    }

                    if (sConstructing != null)
                    {
                        return sConstructing;
                    }

                    T instance = CreateInstance();
                    sConstructing = instance;
                    try
                    {
                        InitializeInstance(instance);
                        Volatile.Write(ref sInstance, instance);
                    }
                    finally
                    {
                        sConstructing = null;
                    }

                    return instance;
                }
            }
        }

        /// <summary>
        /// 获取当前是否已经创建过单例实例。
        /// </summary>
        public static bool HasInstance
        {
            get
            {
                lock (sLock)
                {
                    return sInstance != null;
                }
            }
        }

        /// <summary>
        /// 尝试获取当前实例，不触发自动创建。
        /// </summary>
        /// <param name="instance">当前实例；尚未创建时为 null。</param>
        /// <returns>已存在实例时返回 true。</returns>
        public static bool TryGetInstance(out T instance)
        {
            lock (sLock)
            {
                instance = sInstance;
                return instance != null;
            }
        }

        /// <summary>
        /// 清除缓存的实例引用，并把诊断注册表中的实例标记为非存活。
        /// </summary>
        public static void Dispose()
        {
            lock (sLock)
            {
#if UNITY_EDITOR || (GODOT && TOOLS)
                SingletonRegistry.Unregister(typeof(T));
#endif
                sInstance = null;
            }
        }

        /// <summary>
        /// 通过私有或公开无参构造函数创建纯 C# 单例实例；初始化在实例创建后单独执行。
        /// </summary>
        /// <returns>尚未执行初始化回调的单例实例。</returns>
        private static T CreateInstance()
        {
            Type type = typeof(T);
            T instance;

            try
            {
                instance = Activator.CreateInstance(type, true) as T;
            }
            catch (MissingMethodException exception)
            {
                throw CreateMissingConstructorException(type, exception);
            }
            catch (TargetInvocationException exception)
            {
                throw CreateConstructorInvocationException(type, exception);
            }

            if (instance == null)
            {
                throw new InvalidOperationException("[SingletonKit] Failed to create singleton instance for type " + type.FullName + ".");
            }

            return instance;
        }

        /// <summary>
        /// 在实例可经构造中引用（同线程重入）被访问后执行一次初始化，并登记诊断存活状态；成功后实例才发布到单例缓存。
        /// </summary>
        /// <param name="instance">尚未发布到单例缓存的构造中实例。</param>
        private static void InitializeInstance(T instance)
        {
            instance.OnSingletonInit();
#if UNITY_EDITOR || (GODOT && TOOLS)
            SingletonRegistry.Register(typeof(T), instance, "Base", "SingletonKit");
#endif
        }

        /// <summary>
        /// 构造无无参构造函数时的统一错误。
        /// </summary>
        /// <param name="type">创建失败的单例类型。</param>
        /// <param name="exception">原始反射异常。</param>
        /// <returns>包含类型名和修复建议的异常。</returns>
        private static InvalidOperationException CreateMissingConstructorException(Type type, Exception exception)
        {
            return new InvalidOperationException(
                "[SingletonKit] Type " + type.FullName + " must have a parameterless constructor.",
                exception);
        }

        /// <summary>
        /// 构造构造函数内部异常的统一错误，保留原始异常作为 InnerException。
        /// </summary>
        /// <param name="type">创建失败的单例类型。</param>
        /// <param name="exception">反射包装异常。</param>
        /// <returns>包含类型名和原始异常的异常。</returns>
        private static InvalidOperationException CreateConstructorInvocationException(Type type, TargetInvocationException exception)
        {
            Exception inner = exception.InnerException ?? exception;
            return new InvalidOperationException(
                "[SingletonKit] Constructor of " + type.FullName + " threw an exception.",
                inner);
        }
    }
}
