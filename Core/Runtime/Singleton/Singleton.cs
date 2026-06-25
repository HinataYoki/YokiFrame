namespace YokiFrame
{
    /// <summary>
    /// 由 SingletonKit&lt;T&gt; 管理的纯 C# 单例基类。
    /// </summary>
    public abstract class Singleton<T> : ISingleton where T : Singleton<T>
    {
        /// <summary>
        /// 获取单例实例。
        /// </summary>
        public static T Instance => SingletonKit<T>.Instance;

        /// <summary>
        /// 清除当前单例实例引用。
        /// </summary>
        public static void Dispose() => SingletonKit<T>.Dispose();

        /// <summary>
        /// 单例实例创建后调用。
        /// </summary>
        public virtual void OnSingletonInit()
        {
        }
    }
}
