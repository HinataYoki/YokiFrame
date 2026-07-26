#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// Architecture 运行时诊断注册表，只在架构创建、服务注册和释放这些低频点更新。
    /// </summary>
    public static class ArchitectureRegistry
    {
        private static readonly object sLock = new();
        private static readonly Dictionary<Type, ArchitectureDebugInfo> sInfos = new();
        private static long sDiagnosticVersion;

        /// <summary>
        /// 获取诊断版本号；架构登记、释放或清理时递增。
        /// </summary>
        public static long DiagnosticVersion
        {
            get { return Interlocked.Read(ref sDiagnosticVersion); }
        }

        /// <summary>
        /// 获取当前注册表记录数量，包含已释放但仍保留诊断记录的架构。
        /// </summary>
        public static int Count
        {
            get
            {
                lock (sLock)
                {
                    return sInfos.Count;
                }
            }
        }

        /// <summary>
        /// 登记或刷新一个架构实例的诊断信息。
        /// </summary>
        /// <param name="architectureType">架构类型。</param>
        /// <param name="architecture">架构实例。</param>
        /// <param name="initialized">架构是否已完成初始化。</param>
        /// <param name="services">架构当前服务表。</param>
        public static void Register(
            Type architectureType,
            IArchitecture architecture,
            bool initialized,
            IEnumerable<KeyValuePair<Type, IService>> services)
        {
            if (architectureType == null)
            {
                return;
            }

            lock (sLock)
            {
                ArchitectureDebugInfo info = GetOrCreateInfo(architectureType);
                info.InstanceHash = architecture != null ? RuntimeHelpers.GetHashCode(architecture) : 0;
                info.IsAlive = architecture != null;
                info.Initialized = initialized;
                UpdateServices(info, services);
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 把指定架构记录标记为非存活；记录本身保留给工作台查看释放状态。
        /// 记录跟随最后一次登记的实例，过期实例的释放不回写记录。
        /// </summary>
        /// <param name="architectureType">架构类型。</param>
        /// <param name="architecture">被释放的架构实例。</param>
        public static void Unregister(Type architectureType, IArchitecture architecture)
        {
            if (architectureType == null)
            {
                return;
            }

            lock (sLock)
            {
                ArchitectureDebugInfo info;
                if (!sInfos.TryGetValue(architectureType, out info))
                {
                    return;
                }

                if (architecture != null && info.InstanceHash != RuntimeHelpers.GetHashCode(architecture))
                {
                    return;
                }

                info.IsAlive = false;
                if (architecture != null)
                {
                    info.InstanceHash = RuntimeHelpers.GetHashCode(architecture);
                }

                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 复制当前全部架构诊断记录，避免调用方修改注册表内部对象。
        /// </summary>
        /// <param name="result">用于接收诊断记录的列表；方法会先清空该列表。</param>
        public static void GetAll(List<ArchitectureDebugInfo> result)
        {
            if (result == null)
            {
                return;
            }

            result.Clear();
            lock (sLock)
            {
                foreach (KeyValuePair<Type, ArchitectureDebugInfo> pair in sInfos)
                {
                    result.Add(CloneInfo(pair.Value));
                }
            }
        }

        /// <summary>
        /// 清空注册表，通常只用于测试或域重载前的清理。
        /// </summary>
        public static void Clear()
        {
            lock (sLock)
            {
                sInfos.Clear();
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 只读探测指定架构的登记状态是否与给定实例状态一致；不修改记录也不递增版本号。
        /// </summary>
        /// <param name="architectureType">架构类型。</param>
        /// <param name="instanceHash">架构实例哈希。</param>
        /// <param name="initialized">架构是否已完成初始化。</param>
        /// <returns>记录存在、存活且实例哈希与初始化状态一致时返回 true。</returns>
        internal static bool IsCurrent(Type architectureType, int instanceHash, bool initialized)
        {
            if (architectureType == null)
            {
                return false;
            }

            lock (sLock)
            {
                ArchitectureDebugInfo info;
                if (!sInfos.TryGetValue(architectureType, out info))
                {
                    return false;
                }

                return info.IsAlive && info.InstanceHash == instanceHash && info.Initialized == initialized;
            }
        }

        /// <summary>
        /// 获取已有架构诊断记录；不存在时创建新记录。
        /// </summary>
        /// <param name="architectureType">架构类型。</param>
        /// <returns>诊断记录。</returns>
        private static ArchitectureDebugInfo GetOrCreateInfo(Type architectureType)
        {
            ArchitectureDebugInfo info;
            if (sInfos.TryGetValue(architectureType, out info))
            {
                return info;
            }

            info = new ArchitectureDebugInfo
            {
                TypeName = architectureType.Name,
                FullName = architectureType.FullName ?? architectureType.Name,
                CreatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            sInfos.Add(architectureType, info);
            return info;
        }

        /// <summary>
        /// 刷新架构服务诊断快照，并按完整类型名稳定排序。
        /// </summary>
        /// <param name="info">架构诊断记录。</param>
        /// <param name="services">架构服务表。</param>
        private static void UpdateServices(
            ArchitectureDebugInfo info,
            IEnumerable<KeyValuePair<Type, IService>> services)
        {
            info.Services.Clear();
            if (services != null)
            {
                foreach (KeyValuePair<Type, IService> pair in services)
                {
                    AddServiceInfo(info.Services, pair.Key, pair.Value);
                }

                info.Services.Sort(CompareServices);
            }

            info.ServiceCount = info.Services.Count;
        }

        /// <summary>
        /// 把单个服务转换为诊断快照。
        /// </summary>
        /// <param name="services">目标服务快照列表。</param>
        /// <param name="contractType">服务注册类型。</param>
        /// <param name="service">服务实例。</param>
        private static void AddServiceInfo(
            List<ArchitectureServiceDebugInfo> services,
            Type contractType,
            IService service)
        {
            if (contractType == null || service == null)
            {
                return;
            }

            Type implementationType = service.GetType();
            services.Add(new ArchitectureServiceDebugInfo
            {
                TypeName = contractType.Name,
                FullName = contractType.FullName ?? contractType.Name,
                ImplementationTypeName = implementationType.Name,
                ImplementationFullName = implementationType.FullName ?? implementationType.Name,
                Initialized = service.Initialized,
                InstanceHash = RuntimeHelpers.GetHashCode(service)
            });
        }

        /// <summary>
        /// 按服务完整类型名排序，保证工作台和测试输出稳定。
        /// </summary>
        /// <param name="left">左侧服务信息。</param>
        /// <param name="right">右侧服务信息。</param>
        /// <returns>排序比较结果。</returns>
        private static int CompareServices(ArchitectureServiceDebugInfo left, ArchitectureServiceDebugInfo right)
        {
            string leftName = left != null ? left.FullName : string.Empty;
            string rightName = right != null ? right.FullName : string.Empty;
            return string.CompareOrdinal(leftName, rightName);
        }

        /// <summary>
        /// 克隆架构诊断记录。
        /// </summary>
        /// <param name="source">原始记录。</param>
        /// <returns>克隆后的记录。</returns>
        private static ArchitectureDebugInfo CloneInfo(ArchitectureDebugInfo source)
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

            for (var index = 0; index < source.Services.Count; index++)
            {
                copy.Services.Add(CloneServiceInfo(source.Services[index]));
            }

            return copy;
        }

        /// <summary>
        /// 克隆服务诊断记录。
        /// </summary>
        /// <param name="source">原始服务记录。</param>
        /// <returns>克隆后的服务记录。</returns>
        private static ArchitectureServiceDebugInfo CloneServiceInfo(ArchitectureServiceDebugInfo source)
        {
            return new ArchitectureServiceDebugInfo
            {
                TypeName = source.TypeName,
                FullName = source.FullName,
                ImplementationTypeName = source.ImplementationTypeName,
                ImplementationFullName = source.ImplementationFullName,
                Initialized = source.Initialized,
                InstanceHash = source.InstanceHash
            };
        }

        /// <summary>
        /// 递增诊断版本号。
        /// </summary>
        private static void BumpDiagnosticVersion()
        {
            Interlocked.Increment(ref sDiagnosticVersion);
        }
    }
}
#endif
