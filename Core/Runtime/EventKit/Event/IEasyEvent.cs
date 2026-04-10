using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Common runtime contract for all EasyEvent containers.
    /// </summary>
    public interface IEasyEvent
    {
        /// <summary>
        /// Removes every registered listener from the event container.
        /// </summary>
        void UnRegisterAll();

        /// <summary>
        /// Current number of registered listeners.
        /// </summary>
        int ListenerCount { get; }

        /// <summary>
        /// Enumerates all registered delegates for diagnostics or editor inspection.
        /// </summary>
        IEnumerable<Delegate> GetListeners();
    }
}
