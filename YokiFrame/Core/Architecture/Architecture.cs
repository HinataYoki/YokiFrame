using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace YokiFrame
{
    #region 定义
    /// <summary>
    /// 架构
    /// </summary>
    public interface IArchitecture : ICanDispose
    {
        static IArchitecture Interface { get; }
        void Register<T>(T service) where T : class, IService, new();
        T GetService<T>(bool force = false) where T : class, IService, new();
        IEnumerable<T> GetServicesByType<T>() where T : class, IService, new();
    }
    /// <summary>
    /// 服务
    /// </summary>
    public interface IService : ICanDispose
    {
        IArchitecture Architecture { get; }
        void SetArchitecture(IArchitecture architecture);
    }
    /// <summary>
    /// 数据服务
    /// </summary>
    public interface IModel : IService, ISerializable { }

    public interface ICanInit
    {
        abstract bool Initialized { get; }
        abstract void OnInit();
    }

    public interface ICanDispose : ICanInit
    {
        virtual void Dispose() { }
    }
    #endregion

    #region 抽象实现
    public abstract class Architecture<T> : IArchitecture where T : Architecture<T>, new()
    {
        private static readonly Dictionary<Type, IService> mServices = new();

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
                    //初始化架构,用户自己的服务在这里面写入
                    mArchitecture.OnInit();
                    mArchitecture.mInited = true;
                }
                return mArchitecture;
            }
        }

        public abstract void OnInit();

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

        public IEnumerable<K> GetServicesByType<K>() where K : class, IService, new()
            => mServices.Values.OfType<K>();

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
            service.OnInit();
            service.SetArchitecture(mArchitecture);
        }
    }

    public abstract class AbstractService : IService
    {
        private IArchitecture mArchitecture;
        public IArchitecture Architecture => mArchitecture;

        private bool mInitialized = false;
        public bool Initialized => mInitialized;

        public abstract void OnInit();

        void IService.SetArchitecture(IArchitecture architecture)
        {
            mArchitecture = architecture;
            mInitialized = true;
        }
    }

    public abstract class AbstractModel : AbstractService, IModel
    {
        public abstract void GetObjectData(SerializationInfo info, StreamingContext context);
    }
    #endregion
}