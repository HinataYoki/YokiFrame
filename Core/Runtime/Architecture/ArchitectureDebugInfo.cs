using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Architecture 服务注册快照，只在架构初始化和服务注册这些低频点更新。
    /// </summary>
    public sealed class ArchitectureServiceDebugInfo
    {
        public string TypeName;
        public string FullName;
        public string ImplementationTypeName;
        public string ImplementationFullName;
        public bool Initialized;
        public int InstanceHash;
    }

    /// <summary>
    /// Architecture 实例快照，供命令桥和编辑器页面读取。
    /// </summary>
    public sealed class ArchitectureDebugInfo
    {
        public string TypeName;
        public string FullName;
        public string CreatedAtUtc;
        public int InstanceHash;
        public bool IsAlive;
        public bool Initialized;
        public int ServiceCount;
        public List<ArchitectureServiceDebugInfo> Services = new();
    }

    /// <summary>
    /// Architecture 运行时注册表。Base 层只登记低频生命周期状态，不做反射扫描。
    /// </summary>
    public static class ArchitectureRegistry
    {
        private static readonly object sLock = new object();
        private static readonly Dictionary<Type, ArchitectureDebugInfo> sInfos = new();

        public static int Count
        {
            get
            {
                lock (sLock)
                    return sInfos.Count;
            }
        }

        public static void Register(
            Type architectureType,
            IArchitecture architecture,
            bool initialized,
            IEnumerable<KeyValuePair<Type, IService>> services)
        {
            if (architectureType == null)
                return;

            lock (sLock)
            {
                if (!sInfos.TryGetValue(architectureType, out var info))
                {
                    info = new ArchitectureDebugInfo
                    {
                        TypeName = architectureType.Name,
                        FullName = architectureType.FullName ?? architectureType.Name,
                        CreatedAtUtc = DateTime.UtcNow.ToString("O")
                    };
                    sInfos.Add(architectureType, info);
                }

                info.InstanceHash = architecture != null ? architecture.GetHashCode() : 0;
                info.IsAlive = architecture != null;
                info.Initialized = initialized;
                UpdateServices(info, services);
            }
        }

        public static void Unregister(Type architectureType, IArchitecture architecture)
        {
            if (architectureType == null)
                return;

            lock (sLock)
            {
                if (!sInfos.TryGetValue(architectureType, out var info))
                    return;

                info.IsAlive = false;
                if (architecture != null)
                    info.InstanceHash = architecture.GetHashCode();
            }
        }

        public static void GetAll(List<ArchitectureDebugInfo> result)
        {
            if (result == null)
                return;

            result.Clear();
            lock (sLock)
            {
                foreach (var kvp in sInfos)
                    result.Add(Copy(kvp.Value));
            }
        }

        public static void Clear()
        {
            lock (sLock)
                sInfos.Clear();
        }

        private static void UpdateServices(
            ArchitectureDebugInfo info,
            IEnumerable<KeyValuePair<Type, IService>> services)
        {
            info.Services.Clear();
            if (services != null)
            {
                foreach (var pair in services)
                {
                    var contractType = pair.Key;
                    var service = pair.Value;
                    if (contractType == null || service == null)
                        continue;

                    var implementationType = service.GetType();
                    info.Services.Add(new ArchitectureServiceDebugInfo
                    {
                        TypeName = contractType.Name,
                        FullName = contractType.FullName ?? contractType.Name,
                        ImplementationTypeName = implementationType.Name,
                        ImplementationFullName = implementationType.FullName ?? implementationType.Name,
                        Initialized = service.Initialized,
                        InstanceHash = service.GetHashCode()
                    });
                }

                info.Services.Sort(CompareServices);
            }

            info.ServiceCount = info.Services.Count;
        }

        private static int CompareServices(ArchitectureServiceDebugInfo a, ArchitectureServiceDebugInfo b)
        {
            return string.CompareOrdinal(a != null ? a.FullName : string.Empty, b != null ? b.FullName : string.Empty);
        }

        private static ArchitectureDebugInfo Copy(ArchitectureDebugInfo source)
        {
            var copy = new ArchitectureDebugInfo
            {
                TypeName = source.TypeName,
                FullName = source.FullName,
                CreatedAtUtc = source.CreatedAtUtc,
                InstanceHash = source.InstanceHash,
                IsAlive = source.IsAlive,
                Initialized = source.Initialized,
                ServiceCount = source.ServiceCount
            };

            for (var i = 0; i < source.Services.Count; i++)
            {
                var service = source.Services[i];
                copy.Services.Add(new ArchitectureServiceDebugInfo
                {
                    TypeName = service.TypeName,
                    FullName = service.FullName,
                    ImplementationTypeName = service.ImplementationTypeName,
                    ImplementationFullName = service.ImplementationFullName,
                    Initialized = service.Initialized,
                    InstanceHash = service.InstanceHash
                });
            }

            return copy;
        }
    }
}
