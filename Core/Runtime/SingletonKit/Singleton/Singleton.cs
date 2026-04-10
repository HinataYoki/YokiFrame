namespace YokiFrame
{
    /// <summary>
    /// Base class for plain C# singletons managed by <see cref="SingletonKit{T}"/>.
    /// </summary>
    public abstract class Singleton<T> : ISingleton where T : Singleton<T>
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
        public virtual void OnSingletonInit()
        {
        }
    }
}
