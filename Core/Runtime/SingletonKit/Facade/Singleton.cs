namespace YokiFrame
{
    /// <summary>
    /// 由 SingletonKit 管理的纯 C# 单例基类。
    /// </summary>
    /// <typeparam name="T">具体单例类型。</typeparam>
    public abstract class Singleton<T> : ISingleton where T : Singleton<T>
    {
        /// <summary>
        /// 获取当前类型的单例实例。
        /// </summary>
        public static T Instance
        {
            get { return SingletonKit<T>.Instance; }
        }

        /// <summary>
        /// 清除当前类型的单例实例引用。
        /// </summary>
        public static void Dispose()
        {
            SingletonKit<T>.Dispose();
        }

        /// <summary>
        /// 单例实例创建后调用；子类可重写完成自身初始化。
        /// </summary>
        public virtual void OnSingletonInit()
        {
        }
    }
}
