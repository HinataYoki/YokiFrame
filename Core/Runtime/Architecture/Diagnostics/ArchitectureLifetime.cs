using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 记录当前进程里仍存活的架构实例，供会话换代统一释放。
    /// 诊断注册表只在 Editor/Tools 编译，不能承担 Player 的生命周期。
    /// </summary>
    internal static class ArchitectureLifetime
    {
        private static readonly object sLock = new object();
        private static readonly List<IArchitecture> sInstances = new List<IArchitecture>();

        /// <summary>
        /// 登记一个已完成初始化的架构。重复登记同一引用保持幂等。
        /// </summary>
        /// <param name="architecture">要跟踪的架构实例。</param>
        internal static void Register(IArchitecture architecture)
        {
            if (architecture == null)
            {
                return;
            }

            lock (sLock)
            {
                if (Contains(architecture))
                {
                    return;
                }

                sInstances.Add(architecture);
            }
        }

        /// <summary>
        /// 停止跟踪一个已经释放或被替换的架构。
        /// </summary>
        /// <param name="architecture">要移除的架构实例。</param>
        internal static void Unregister(IArchitecture architecture)
        {
            if (architecture == null)
            {
                return;
            }

            lock (sLock)
            {
                for (int index = 0; index < sInstances.Count; index++)
                {
                    if (!ReferenceEquals(sInstances[index], architecture))
                    {
                        continue;
                    }

                    sInstances.RemoveAt(index);
                    return;
                }
            }
        }

        /// <summary>
        /// 释放当前已跟踪的全部架构。释放过程中新创建的实例留到下一次会话换代。
        /// </summary>
        internal static void DisposeAll()
        {
            IArchitecture[] snapshot = TakeSnapshot();
            Exception firstException = null;
            for (int index = 0; index < snapshot.Length; index++)
            {
                try
                {
                    snapshot[index].Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                    {
                        firstException = exception;
                    }
                }
            }

            if (firstException != null)
            {
                throw firstException;
            }
        }

        /// <summary>
        /// 取出并清空当前跟踪表，避免在释放期间持有锁。
        /// </summary>
        /// <returns>本次要释放的架构。</returns>
        private static IArchitecture[] TakeSnapshot()
        {
            lock (sLock)
            {
                IArchitecture[] snapshot = sInstances.ToArray();
                sInstances.Clear();
                return snapshot;
            }
        }

        /// <summary>
        /// 按引用判断架构是否已经跟踪。调用方必须持有锁。
        /// </summary>
        /// <param name="architecture">目标架构。</param>
        /// <returns>已跟踪时返回 true。</returns>
        private static bool Contains(IArchitecture architecture)
        {
            for (int index = 0; index < sInstances.Count; index++)
            {
                if (ReferenceEquals(sInstances[index], architecture))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
