namespace YokiFrame
{
    /// <summary>
    /// 普通 C# 单例基类
    /// </summary>
    public abstract class Singleton<T> : ISingleton where T : Singleton<T>
    {
        /// <summary>
        /// 获取单例实例（委托给 SingletonKit 管理）
        /// </summary>
        public static T Instance => SingletonKit<T>.Instance;

        /// <summary>
        /// 释放单例实例
        /// </summary>
        public static void Dispose() => SingletonKit<T>.Dispose();

        /// <summary>
        /// 单例初始化方法
        /// </summary>
        public virtual void OnSingletonInit()
        {
        }
    }
}