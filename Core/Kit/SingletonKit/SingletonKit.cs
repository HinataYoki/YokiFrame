namespace YokiFrame
{
    public static class SingletonKit<T> where T : class, ISingleton
    {
        /// <summary>
        /// 静态实例
        /// </summary>
        private static T mInstance;
        /// <summary>
        /// 标签锁
        /// </summary>
        private static readonly object mLock = new();
        /// <summary>
        /// 静态属性
        /// </summary>
        public static T Instance
        {
            get
            {
                if (mInstance == null)
                {
                    lock (mLock)
                    {
                        mInstance ??= SingletonCreator.CreateSingleton<T>();
                    }
                }

                return mInstance;
            }
        }
        /// <summary>
        /// 资源释放
        /// </summary>
        public static void Dispose()
        {
            mInstance = null;
        }
    }
}