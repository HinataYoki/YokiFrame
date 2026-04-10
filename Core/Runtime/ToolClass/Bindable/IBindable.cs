using System;

namespace YokiFrame
{
    /// <summary>
    /// Contract for a value that supports change binding.
    /// </summary>
    public interface IBindable<T>
    {
        /// <summary>
        /// Current value.
        /// </summary>
        T Value { get; set; }

        /// <summary>
        /// Registers a value-changed callback.
        /// </summary>
        LinkUnRegister<T> Bind(Action<T> callback);

        /// <summary>
        /// Unregisters one value-changed callback.
        /// </summary>
        void UnBind(Action<T> value);

        /// <summary>
        /// Unregisters all callbacks.
        /// </summary>
        void UnBindAll();
    }

    /// <summary>
    /// Convenience helpers for bindable values.
    /// </summary>
    public static class BindableExtensions
    {
        /// <summary>
        /// Registers a callback and immediately invokes it with the current value.
        /// </summary>
        public static LinkUnRegister<T> BindWithCallback<T>(this IBindable<T> self, Action<T> callback)
        {
            var unregister = self.Bind(callback);
            callback?.Invoke(self.Value);
            return unregister;
        }
    }
}
