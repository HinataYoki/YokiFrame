using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// Base class for <see cref="MonoBehaviour"/> singletons managed by <see cref="SingletonKit{T}"/>.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="Singleton{T}"/> when Unity lifecycle callbacks are not required. Use
    /// <see cref="MonoSingleton{T}"/> only for components that must participate in scene or GameObject lifecycle.
    /// </remarks>
    public abstract class MonoSingleton<T> : MonoBehaviour, ISingleton where T : MonoSingleton<T>
    {
        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static T Instance => SingletonKit<T>.Instance;

        /// <summary>
        /// Clears the current singleton instance reference.
        /// </summary>
        public static void Dispose() => SingletonKit<T>.Dispose();

        /// <summary>
        /// Called after the singleton instance has been created.
        /// </summary>
        public virtual void OnSingletonInit() { }

        /// <summary>
        /// Clears the singleton reference when the component is destroyed.
        /// </summary>
        protected virtual void OnDestroy()
        {
            SingletonKit<T>.Dispose();
        }
    }
}
