using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 类型级 Architecture 单例基类，负责服务注册、初始化和诊断登记。
    /// </summary>
    /// <typeparam name="T">具体架构类型。</typeparam>
    public abstract class Architecture<T> : IArchitecture where T : Architecture<T>, new()
    {
        private static readonly object sStaticLock = new();
        private static T sArchitecture;

        private readonly object mSyncRoot = new();
        private readonly Dictionary<Type, IService> mServices = new();
        private readonly Dictionary<Type, Lazy<IService>> mPendingServiceCreations = new();
        private bool mInitialized;
        private bool mDisposed;

        /// <summary>
        /// 获取当前架构的类型级单例；首次访问会创建实例、执行 OnInit 并初始化已注册服务。
        /// </summary>
        public static IArchitecture Interface
        {
            get { return GetOrCreate(); }
        }

        /// <summary>
        /// 获取当前架构是否已经完成初始化。
        /// </summary>
        public bool Initialized
        {
            get { return mInitialized; }
        }

        /// <summary>
        /// 注册一个服务实例；重复注册同一类型时会在容器锁外释放旧实例。
        /// 架构已释放时抛出 ObjectDisposedException，避免服务被提交进无人清理的容器。
        /// </summary>
        /// <typeparam name="K">服务类型。</typeparam>
        /// <param name="service">服务实例。</param>
        public void Register<K>(K service) where K : class, IService, new()
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            IService oldService;
            lock (mSyncRoot)
            {
                if (mDisposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                Type key = typeof(K);
                oldService = ReplaceService(key, service);
                service.SetArchitecture(this);
                if (mInitialized && !service.Initialized)
                {
                    service.Init();
                }

#if UNITY_EDITOR || (GODOT && TOOLS)
                RegisterSnapshot();
#endif
            }

            oldService?.Dispose();
        }

        /// <summary>
        /// 获取已注册服务；未注册且 force 为 true 时会创建、注册并初始化服务。
        /// 同一服务类型的并发强制请求共享一次创建，期间完成的显式注册优先。
        /// 当前线程已持有容器锁时绕过共享创建直接创建注册，避免与工厂线程互相等待造成死锁。
        /// 架构已释放时强制创建抛出 ObjectDisposedException。
        /// </summary>
        /// <typeparam name="K">服务类型。</typeparam>
        /// <param name="force">是否在缺失时强制创建服务。</param>
        /// <returns>已注册或新创建的服务；缺失且不强制创建时返回 null。</returns>
        public K GetService<K>(bool force = false) where K : class, IService, new()
        {
            Type key = typeof(K);
            bool lockHeld = Monitor.IsEntered(mSyncRoot);
            Lazy<IService> pendingCreation;
            lock (mSyncRoot)
            {
                IService service;
                if (mServices.TryGetValue(key, out service))
                {
                    return service as K;
                }

                if (!force)
                {
                    return null;
                }

                if (mDisposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                if (lockHeld)
                {
                    return CreateAndRegisterService<K>(key) as K;
                }

                if (!mPendingServiceCreations.TryGetValue(key, out pendingCreation))
                {
                    pendingCreation = new(
                        () => CreateAndRegisterService<K>(key),
                        LazyThreadSafetyMode.ExecutionAndPublication);
                    mPendingServiceCreations.Add(key, pendingCreation);
                }
            }

            try
            {
                return pendingCreation.Value as K;
            }
            finally
            {
                RemovePendingServiceCreation(key, pendingCreation);
            }
        }

        /// <summary>
        /// 获取当前架构中全部已注册服务的快照。
        /// </summary>
        /// <returns>服务快照集合；调用方修改集合不会影响架构内部状态。</returns>
        public IEnumerable<IService> GetAllServices()
        {
            var services = new List<IService>();
            lock (mSyncRoot)
            {
                foreach (KeyValuePair<Type, IService> pair in mServices)
                {
                    services.Add(pair.Value);
                }
            }

            return services;
        }

        /// <summary>
        /// 执行架构初始化；重复调用不会重复触发 OnInit。
        /// </summary>
        void ICanInit.Init()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// 释放架构实例、服务和诊断存活标记。
        /// </summary>
        void IDisposable.Dispose()
        {
            lock (sStaticLock)
            {
                if (ReferenceEquals(sArchitecture, this))
                {
                    sArchitecture = null;
                }
            }

            DisposeInstance();
        }

        /// <summary>
        /// 子类实现架构初始化，并在其中注册服务。
        /// </summary>
        protected abstract void OnInit();

        /// <summary>
        /// 子类按需实现架构释放逻辑；默认不做额外处理。
        /// </summary>
        protected virtual void OnDispose()
        {
        }

        /// <summary>
        /// 获取或创建当前类型级架构实例。
        /// </summary>
        /// <returns>已初始化的架构实例。</returns>
        private static T GetOrCreate()
        {
            lock (sStaticLock)
            {
                if (sArchitecture == null)
                {
                    sArchitecture = new T();
                }

                sArchitecture.EnsureInitialized();
                return sArchitecture;
            }
        }

        /// <summary>
        /// 确保架构只初始化一次，并在 OnInit 后统一初始化服务。
        /// </summary>
        private void EnsureInitialized()
        {
            lock (mSyncRoot)
            {
                if (mInitialized)
                {
#if UNITY_EDITOR || (GODOT && TOOLS)
                    if (!ArchitectureRegistry.IsCurrent(typeof(T), RuntimeHelpers.GetHashCode(this), mInitialized))
                    {
                        RegisterSnapshot();
                    }
#endif
                    return;
                }

                mDisposed = false;
#if UNITY_EDITOR || (GODOT && TOOLS)
                RegisterSnapshot();
#endif
                OnInit();
                InitializePendingServices();
                mInitialized = true;
#if UNITY_EDITOR || (GODOT && TOOLS)
                RegisterSnapshot();
#endif
            }
        }

        /// <summary>
        /// 初始化所有尚未初始化的服务；允许服务初始化期间继续注册新服务。
        /// </summary>
        private void InitializePendingServices()
        {
            while (true)
            {
                IService service = FindUninitializedService();
                if (service == null)
                {
                    return;
                }

                service.Init();
            }
        }

        /// <summary>
        /// 查找一个尚未初始化的服务。
        /// </summary>
        /// <returns>未初始化服务；没有时返回 null。</returns>
        private IService FindUninitializedService()
        {
            foreach (KeyValuePair<Type, IService> pair in mServices)
            {
                if (pair.Value != null && !pair.Value.Initialized)
                {
                    return pair.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// 在容器锁外创建服务，并在提交前再次检查显式注册结果。
        /// 并发显式注册已经成功时释放未采用的候选实例，避免覆盖调用方明确提供的服务。
        /// </summary>
        /// <typeparam name="K">待创建的服务类型。</typeparam>
        /// <param name="key">服务注册类型。</param>
        /// <returns>最终保存在架构中的服务实例。</returns>
        private IService CreateAndRegisterService<K>(Type key) where K : class, IService, new()
        {
            lock (mSyncRoot)
            {
                IService registeredService;
                if (mServices.TryGetValue(key, out registeredService))
                {
                    return registeredService;
                }
            }

            var newService = new K();
            IService selectedService;
            lock (mSyncRoot)
            {
                if (!mServices.TryGetValue(key, out selectedService))
                {
                    Register(newService);
                    return newService;
                }
            }

            newService.Dispose();
            return selectedService;
        }

        /// <summary>
        /// 移除已经完成或失败的服务创建预留；仅移除当前调用持有的同一预留，避免误删后续重试。
        /// </summary>
        /// <param name="key">服务注册类型。</param>
        /// <param name="pendingCreation">当前调用等待的创建预留。</param>
        private void RemovePendingServiceCreation(Type key, Lazy<IService> pendingCreation)
        {
            lock (mSyncRoot)
            {
                Lazy<IService> currentCreation;
                if (mPendingServiceCreations.TryGetValue(key, out currentCreation) &&
                    ReferenceEquals(currentCreation, pendingCreation))
                {
                    mPendingServiceCreations.Remove(key);
                }
            }
        }

        /// <summary>
        /// 替换指定服务类型的实例，并返回被替换的旧实例；旧实例由调用方在容器锁外释放。
        /// </summary>
        /// <param name="key">服务注册类型。</param>
        /// <param name="service">新服务实例。</param>
        /// <returns>被替换的旧服务实例；没有旧实例或与新实例相同时返回 null。</returns>
        private IService ReplaceService(Type key, IService service)
        {
            IService oldService;
            if (!mServices.TryGetValue(key, out oldService) || ReferenceEquals(oldService, service))
            {
                oldService = null;
            }

            mServices[key] = service;
            return oldService;
        }

        /// <summary>
        /// 释放当前架构实例；该方法不要求调用方已经持有静态锁。
        /// </summary>
        private void DisposeInstance()
        {
            List<IService> services;
            lock (mSyncRoot)
            {
                if (mDisposed)
                {
                    return;
                }

                mDisposed = true;
                services = CopyServices();
#if UNITY_EDITOR || (GODOT && TOOLS)
                ArchitectureRegistry.Unregister(typeof(T), this);
#endif
                mServices.Clear();
                mInitialized = false;
            }

            List<Exception> errors = new();
            DisposeServices(services, errors);
            try
            {
                OnDispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            ThrowDisposeErrors(errors);
        }

        /// <summary>
        /// 复制当前服务列表，用于在锁外执行释放回调。
        /// </summary>
        /// <returns>服务列表副本。</returns>
        private List<IService> CopyServices()
        {
            var services = new List<IService>(mServices.Count);
            foreach (KeyValuePair<Type, IService> pair in mServices)
            {
                services.Add(pair.Value);
            }

            return services;
        }

        /// <summary>
        /// 尝试释放服务列表中的每个服务实例，即使某个服务失败也继续清理其它服务。
        /// </summary>
        /// <param name="services">待释放服务列表。</param>
        /// <param name="errors">接收释放阶段的异常。</param>
        private static void DisposeServices(List<IService> services, List<Exception> errors)
        {
            for (var index = 0; index < services.Count; index++)
            {
                IService service = services[index];
                if (service != null)
                {
                    try
                    {
                        service.Dispose();
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }
        }

        /// <summary>
        /// 按异常数量重新抛出架构释放错误，保留单异常调用方的原有类型与原始堆栈。
        /// </summary>
        /// <param name="errors">释放期间收集的异常。</param>
        private static void ThrowDisposeErrors(List<Exception> errors)
        {
            if (errors.Count == 0)
            {
                return;
            }

            if (errors.Count == 1)
            {
                ExceptionDispatchInfo.Capture(errors[0]).Throw();
            }

            throw new AggregateException("One or more architecture cleanup operations failed.", errors);
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 把当前架构和服务状态写入诊断注册表。
        /// </summary>
        private void RegisterSnapshot()
        {
            ArchitectureRegistry.Register(typeof(T), this, mInitialized, mServices);
        }
#endif
    }
}
