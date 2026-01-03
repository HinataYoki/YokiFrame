using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// MonoBehaviour 单例基类
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
        /// 应用程序退出：释放当前对象并销毁相关GameObject
        /// </summary>
        protected virtual void OnApplicationQuit()
        {
            var instance = SingletonKit<T>.Instance;
            if (instance == null) return;
            Destroy(instance.gameObject);
            SingletonKit<T>.Dispose();
        }

        /// <summary>
        /// 释放当前对象
        /// </summary>
        protected virtual void OnDestroy()
        {
            SingletonKit<T>.Dispose();
        }
    }
}