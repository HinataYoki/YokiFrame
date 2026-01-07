using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace YokiFrame
{
    #region 定义
    /// <summary>
    /// 架构
    /// </summary>
    public interface IArchitecture : ICanInit
    {
        static IArchitecture Interface { get; }
        void Register<T>(T service) where T : class, IService, new();
        T GetService<T>(bool force = false) where T : class, IService, new();
    }
    /// <summary>
    /// 服务
    /// </summary>
    public interface IService : ICanInit
    {
        IArchitecture Architecture { get; }
        void SetArchitecture(IArchitecture architecture);
        T GetService<T>() where T : class, IService, new();
    }
    /// <summary>
    /// 数据服务
    /// </summary>
    public interface IModel : IService, ISerializable { }

    public interface ICanInit : IDisposable
    {
        abstract bool Initialized { get; }
        abstract void Init();
    }
    #endregion

    #region 抽象实现
    public abstract class Architecture<T> : IArchitecture where T : Architecture<T>, new()
    {
        private readonly Dictionary<Type, IService> mServices = new();

        private bool mInited = false;
        public bool Initialized => mInited;

        private static T mArchitecture;
        public static IArchitecture Interface
        {
            get
            {
                if (mArchitecture == null)
                {
                    mArchitecture ??= new T();
                    // 初始化架构,用户自己的服务在这里面写入
                    mArchitecture.OnInit();
                    // 服务在注册结束后统一初始化，确保在OnInit中服务互相引用不会拿空
                    foreach (var service in mArchitecture.mServices.Values)
                    {
                        service.Init();
                    }
                    mArchitecture.mInited = true;
                }
                return mArchitecture;
            }
        }


        void ICanInit.Init() => OnInit();
        void IDisposable.Dispose() => Dispose();

        protected abstract void OnInit();
        protected virtual void Dispose() { }

        public K GetService<K>(bool force = false) where K : class, IService, new()
        {
            var key = typeof(K);
            if (!mServices.TryGetValue(key, out var service))
            {
                //如果没有注册到架构会尝试注册到架构
                if (force)
                {
                    service = new K();
                    Register(service as K);
                }
            }
            return service as K;
        }

        public void Register<K>(K service) where K : class, IService, new()
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            var key = typeof(K);
            if (mServices.ContainsKey(key))
            {
                //如果有新的，释放先前的
                mServices[key].Dispose();
                mServices[key] = service;
            }
            else
            {
                mServices.Add(key, service);
            }
            service.SetArchitecture(mArchitecture);
        }
    }

    public abstract class AbstractService : IService
    {
        private IArchitecture mArchitecture;
        public IArchitecture Architecture => mArchitecture;

        private bool mInitialized = false;
        public bool Initialized => mInitialized;


        void IService.SetArchitecture(IArchitecture architecture)
        {
            mArchitecture = architecture;
            mInitialized = architecture != default;
        }

        void ICanInit.Init() => OnInit();
        void IDisposable.Dispose() => Dispose();

        protected abstract void OnInit();

        protected virtual void Dispose() { }


        public T GetService<T>() where T : class, IService, new()
        {
            if (!mInitialized) return default;
            return mArchitecture.GetService<T>();
        }
    }

    public abstract class AbstractModel : AbstractService, IModel
    {
        public abstract void GetObjectData(SerializationInfo info, StreamingContext context);
    }
    #endregion
}