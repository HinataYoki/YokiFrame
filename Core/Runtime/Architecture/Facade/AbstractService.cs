namespace YokiFrame
{
    /// <summary>
    /// Architecture 服务基类，提供架构注入、初始化保护和服务查询能力。
    /// </summary>
    public abstract class AbstractService : IService
    {
        private IArchitecture mArchitecture;
        private bool mInitialized;
        private bool mDisposed;

        /// <summary>
        /// 获取当前服务所属的架构容器。
        /// </summary>
        public IArchitecture Architecture
        {
            get { return mArchitecture; }
        }

        /// <summary>
        /// 获取当前服务是否已经完成初始化。
        /// </summary>
        public bool Initialized
        {
            get { return mInitialized; }
        }

        /// <summary>
        /// 注入当前服务所属架构；仅由 Architecture 注册流程调用。
        /// </summary>
        /// <param name="architecture">服务所属架构。</param>
        void IService.SetArchitecture(IArchitecture architecture)
        {
            mArchitecture = architecture;
            mDisposed = false;
        }

        /// <summary>
        /// 执行服务初始化；重复调用不会重复触发 OnInit。
        /// </summary>
        void ICanInit.Init()
        {
            if (mInitialized)
            {
                return;
            }

            OnInit();
            mInitialized = true;
        }

        /// <summary>
        /// 释放服务并清理架构引用；重复释放不会重复触发 OnDispose。
        /// </summary>
        void System.IDisposable.Dispose()
        {
            if (mDisposed)
            {
                return;
            }

            mDisposed = true;
            OnDispose();
            mInitialized = false;
            mArchitecture = null;
        }

        /// <summary>
        /// 从所属架构中获取其它服务；未注入架构时返回 null。
        /// </summary>
        /// <typeparam name="T">服务类型。</typeparam>
        /// <returns>服务实例；未注册或未注入架构时返回 null。</returns>
        public T GetService<T>() where T : class, IService, new()
        {
            if (mArchitecture == null)
            {
                return null;
            }

            return mArchitecture.GetService<T>();
        }

        /// <summary>
        /// 子类实现具体初始化逻辑；调用时服务已经拿到架构引用。
        /// </summary>
        protected abstract void OnInit();

        /// <summary>
        /// 子类按需实现释放逻辑；默认不做额外处理。
        /// </summary>
        protected virtual void OnDispose()
        {
        }
    }
}
