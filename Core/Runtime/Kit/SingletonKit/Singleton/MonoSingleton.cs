using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// MonoBehaviour 单例基类
    /// 注意：仅在必须使用 MonoBehaviour 生命周期时使用，推荐优先使用纯 C# 的 Singleton&lt;T&gt;
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour, ISingleton where T : MonoSingleton<T>
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
        /// 实现接口的单例初始化
        /// </summary>
        public virtual void OnSingletonInit() { }

        /// <summary>
        /// 释放当前对象
        /// </summary>
        protected virtual void OnDestroy()
        {
            SingletonKit<T>.Dispose();
        }
    }
}