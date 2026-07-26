#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// SingletonKit 运行时注册表，供诊断页面、命令桥和测试读取生命周期快照。
    /// </summary>
    public static class SingletonRegistry
    {
        private static readonly object sLock = new();
        private static readonly Dictionary<Type, SingletonDebugInfo> sInfos = new();
        private static long sDiagnosticVersion;

        /// <summary>
        /// 获取诊断版本号；单例登记、释放或清理时递增。
        /// </summary>
        public static long DiagnosticVersion
        {
            get { return Interlocked.Read(ref sDiagnosticVersion); }
        }

        /// <summary>
        /// 获取当前注册表记录数量，包含已释放但仍保留诊断记录的单例。
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
        /// 登记或刷新一个单例实例的诊断信息。
        /// </summary>
        /// <param name="type">单例类型。</param>
        /// <param name="instance">单例实例。</param>
        /// <param name="backend">后端名称。</param>
        /// <param name="source">创建来源。</param>
        public static void Register(Type type, object instance, string backend, string source)
        {
            if (type == null)
            {
                return;
            }

            lock (sLock)
            {
                sInfos[type] = CreateInfo(type, instance, backend, source);
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 把指定类型的诊断记录标记为非存活；记录本身保留给工作台查看释放状态。
        /// </summary>
        /// <param name="type">要释放的单例类型。</param>
        public static void Unregister(Type type)
        {
            if (type == null)
            {
                return;
            }

            lock (sLock)
            {
                SingletonDebugInfo info;
                if (!sInfos.TryGetValue(type, out info))
                {
                    return;
                }

                info.IsAlive = false;
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 复制当前全部单例诊断记录，避免调用方修改注册表内部对象。
        /// </summary>
        /// <param name="result">用于接收诊断记录的列表；方法会先清空该列表。</param>
        public static void GetAll(List<SingletonDebugInfo> result)
        {
            if (result == null)
            {
                return;
            }

            result.Clear();
            lock (sLock)
            {
                foreach (KeyValuePair<Type, SingletonDebugInfo> pair in sInfos)
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
        /// 创建单例诊断快照。
        /// </summary>
        /// <param name="type">单例类型。</param>
        /// <param name="instance">单例实例。</param>
        /// <param name="backend">后端名称。</param>
        /// <param name="source">创建来源。</param>
        /// <returns>诊断快照。</returns>
        private static SingletonDebugInfo CreateInfo(Type type, object instance, string backend, string source)
        {
            return new SingletonDebugInfo
            {
                TypeName = type.Name,
                FullName = type.FullName ?? type.Name,
                Backend = string.IsNullOrEmpty(backend) ? "Base" : backend,
                Source = string.IsNullOrEmpty(source) ? "SingletonKit" : source,
                CreatedAtUtc = DateTime.UtcNow.ToString("O"),
                InstanceHash = instance != null ? RuntimeHelpers.GetHashCode(instance) : 0,
                IsAlive = instance != null
            };
        }

        /// <summary>
        /// 克隆一条诊断记录。
        /// </summary>
        /// <param name="info">原始记录。</param>
        /// <returns>克隆后的记录。</returns>
        private static SingletonDebugInfo CloneInfo(SingletonDebugInfo info)
        {
            return new SingletonDebugInfo
            {
                TypeName = info.TypeName,
                FullName = info.FullName,
                Backend = info.Backend,
                Source = info.Source,
                CreatedAtUtc = info.CreatedAtUtc,
                InstanceHash = info.InstanceHash,
                IsAlive = info.IsAlive
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
