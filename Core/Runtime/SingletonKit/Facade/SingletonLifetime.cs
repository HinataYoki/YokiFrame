using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 收集各个封闭单例类型的释放回调。泛型静态字段没有统一入口，只能由每个类型在初始化时登记。
    /// </summary>
    internal static class SingletonLifetime
    {
        private static readonly object sLock = new object();
        private static readonly List<Action> sDisposers = new List<Action>();

        /// <summary>
        /// 登记一个单例类型的释放回调。相同委托重复登记保持幂等。
        /// </summary>
        /// <param name="dispose">释放该封闭类型静态实例的回调。</param>
        internal static void Register(Action dispose)
        {
            if (dispose == null)
            {
                throw new ArgumentNullException(nameof(dispose));
            }

            lock (sLock)
            {
                if (Contains(dispose))
                {
                    return;
                }

                sDisposers.Add(dispose);
            }
        }

        /// <summary>
        /// 调用已登记的全部释放回调。某个类型失败时仍继续释放其余类型。
        /// </summary>
        internal static void DisposeAll()
        {
            Action[] disposers = Copy();
            Exception firstException = null;
            for (int index = 0; index < disposers.Length; index++)
            {
                try
                {
                    disposers[index]();
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
        /// 复制当前释放回调，避免释放过程修改登记表时影响本次遍历。
        /// </summary>
        /// <returns>本次要调用的释放回调。</returns>
        private static Action[] Copy()
        {
            lock (sLock)
            {
                Action[] disposers = new Action[sDisposers.Count];
                for (int index = 0; index < disposers.Length; index++)
                {
                    disposers[index] = sDisposers[index];
                }

                return disposers;
            }
        }

        /// <summary>
        /// 按委托目标和方法判断是否已经登记。调用方必须持有锁。
        /// </summary>
        /// <param name="dispose">待查找的释放回调。</param>
        /// <returns>已登记时返回 true。</returns>
        private static bool Contains(Action dispose)
        {
            for (int index = 0; index < sDisposers.Count; index++)
            {
                Action existing = sDisposers[index];
                if (existing.Target == dispose.Target && existing.Method == dispose.Method)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
