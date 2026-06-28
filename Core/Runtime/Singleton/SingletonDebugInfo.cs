using System;
using System.Collections.Generic;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 单例监控快照，只在单例创建/销毁这些低频生命周期点更新。
    /// </summary>
    public sealed class SingletonDebugInfo
    {
        public string TypeName;
        public string FullName;
        public string Backend;
        public string Source;
        public string CreatedAtUtc;
        public int InstanceHash;
        public bool IsAlive;
    }

    /// <summary>
    /// SingletonKit 运行时注册表，供命令桥和编辑器页面读取。
    /// </summary>
    public static class SingletonRegistry
    {
        private static readonly object sLock = new();
        private static readonly Dictionary<Type, SingletonDebugInfo> sInfos = new();
        private static long sDiagnosticVersion;

        public static long DiagnosticVersion
        {
            get { return Interlocked.Read(ref sDiagnosticVersion); }
        }

        public static int Count
        {
            get
            {
                lock (sLock)
                    return sInfos.Count;
            }
        }

        public static void Register(Type type, object instance, string backend, string source)
        {
            if (type == null)
                return;

            lock (sLock)
            {
                sInfos[type] = new SingletonDebugInfo
                {
                    TypeName = type.Name,
                    FullName = type.FullName ?? type.Name,
                    Backend = string.IsNullOrEmpty(backend) ? "Base" : backend,
                    Source = string.IsNullOrEmpty(source) ? "SingletonKit" : source,
                    CreatedAtUtc = DateTime.UtcNow.ToString("O"),
                    InstanceHash = instance != null ? instance.GetHashCode() : 0,
                    IsAlive = instance != null
                };
                BumpDiagnosticVersion();
            }
        }

        public static void Unregister(Type type)
        {
            if (type == null)
                return;

            lock (sLock)
            {
                if (sInfos.TryGetValue(type, out var info))
                {
                    info.IsAlive = false;
                    BumpDiagnosticVersion();
                }
            }
        }

        public static void GetAll(List<SingletonDebugInfo> result)
        {
            if (result == null)
                return;

            result.Clear();
            lock (sLock)
            {
                foreach (var kvp in sInfos)
                    result.Add(kvp.Value);
            }
        }

        public static void Clear()
        {
            lock (sLock)
            {
                sInfos.Clear();
                BumpDiagnosticVersion();
            }
        }

        private static void BumpDiagnosticVersion()
        {
            Interlocked.Increment(ref sDiagnosticVersion);
        }
    }
}
