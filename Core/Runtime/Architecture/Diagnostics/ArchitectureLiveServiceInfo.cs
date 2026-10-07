#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>Immutable registration metadata, without property values or strong references to services.</summary>
    public sealed class ArchitectureLiveServiceInfo
    {
        internal ArchitectureLiveServiceInfo(string id, Type architecture, Type implementation, Type[] contracts)
        {
            RegistrationId = id;
            ArchitectureType = architecture;
            ImplementationType = implementation;
            ContractTypes = Array.AsReadOnly(contracts);
        }

        public string RegistrationId { get; }
        public Type ArchitectureType { get; }
        public Type ImplementationType { get; }
        public IReadOnlyList<Type> ContractTypes { get; }
    }
}
#endif
