using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 统一重置一次宿主运行会话中的可变静态状态。
    /// 进程级只读数据和 Editor 注册表不进入这里。
    /// </summary>
    public static class YokiFrameSession
    {
        /// <summary>退出标记等宿主旗标，先于业务状态清理。</summary>
        public const int PREPARE_HOST_ORDER = 50;

        /// <summary>停止帧监听和仍在推进的运行时工作。</summary>
        public const int STOP_RUNTIME_ORDER = 100;

        /// <summary>释放场景、音频、存档、资源和宿主对象。</summary>
        public const int RELEASE_HOSTS_ORDER = 200;

        /// <summary>清空跨模块事件订阅。</summary>
        public const int CLEAR_BUSES_ORDER = 300;

        /// <summary>释放架构和纯 C# 单例。</summary>
        public const int RELEASE_SERVICES_ORDER = 400;

        /// <summary>清空只在 Editor/Tools 中存活的运行诊断。</summary>
        public const int CLEAR_DIAGNOSTICS_ORDER = 450;

        /// <summary>清空日志和运行时设置缓存。工厂恢复必须晚于这一层。</summary>
        public const int CLEAR_FRAMEWORK_ORDER = 500;

        /// <summary>重新登记被清掉的宿主默认工厂。</summary>
        public const int RESTORE_FACTORIES_ORDER = 900;

        private struct Participant
        {
            public int Order;
            public string Id;
            public Action Reset;
        }

        private static readonly object sLock = new object();
        private static readonly List<Participant> sParticipants = new List<Participant>();
        private static bool sBeginning;
        private static long sGeneration;

        /// <summary>
        /// 登记 Core 自己拥有的会话状态。Tool 在各自静态构造或宿主安装器里登记。
        /// </summary>
        static YokiFrameSession()
        {
            RegisterCoreParticipants();
        }

        /// <summary>
        /// 获取已经完成的会话换代次数。进程启动时为 0，每次 <see cref="Begin"/> 成功进入后加一。
        /// </summary>
        public static long Generation
        {
            get { return System.Threading.Interlocked.Read(ref sGeneration); }
        }

        /// <summary>
        /// 登记一个会话重置回调。相同 <paramref name="participantId"/> 会替换顺序和回调，重复登记不会叠多次。
        /// 回调在本次 <see cref="Begin"/> 已经开始后登记时，只参与下一次换代。
        /// </summary>
        /// <param name="order">执行顺序，数值小的先执行。</param>
        /// <param name="participantId">稳定参与者标识，不能为空。</param>
        /// <param name="reset">关闭上一代会话状态的回调，不能为空。</param>
        public static void Register(int order, string participantId, Action reset)
        {
            if (string.IsNullOrWhiteSpace(participantId))
            {
                throw new ArgumentException("Session participant id is required.", nameof(participantId));
            }

            if (reset == null)
            {
                throw new ArgumentNullException(nameof(reset));
            }

            lock (sLock)
            {
                int index = FindIndex(participantId);
                Participant participant = new Participant
                {
                    Order = order,
                    Id = participantId,
                    Reset = reset
                };
                if (index >= 0)
                {
                    sParticipants[index] = participant;
                    return;
                }

                sParticipants.Add(participant);
            }
        }

        /// <summary>
        /// 按固定顺序关闭上一代会话。单个回调失败不会阻止其余参与者，失败留到全部结束后再记录。
        /// 换代过程中再次调用会直接返回，避免重置回调反向重入。
        /// </summary>
        public static void Begin()
        {
            Participant[] participants = BeginGeneration();
            if (participants == null)
            {
                return;
            }

            try
            {
                InvokeParticipants(participants);
            }
            finally
            {
                EndGeneration();
            }
        }

        /// <summary>
        /// 登记不依赖 Tool 程序集的 Core 会话参与者。
        /// </summary>
        private static void RegisterCoreParticipants()
        {
            Register(STOP_RUNTIME_ORDER, "frame", YokiFrameUpdateDispatcher.ResetListeners);
            Register(RELEASE_HOSTS_ORDER, "reskit", ResKit.ResetRuntimeDefaults);
            Register(RELEASE_HOSTS_ORDER, "poolkit-shared", PoolKit.Shared.Clear);
            Register(CLEAR_BUSES_ORDER, "events", EventKit.Clear);
            Register(RELEASE_SERVICES_ORDER, "architecture", ArchitectureLifetime.DisposeAll);
            Register(RELEASE_SERVICES_ORDER + 10, "singleton", SingletonLifetime.DisposeAll);
#if UNITY_EDITOR || (GODOT && TOOLS)
            Register(CLEAR_DIAGNOSTICS_ORDER, "fsm", FsmKitRegistry.ClearAll);
#endif
            Register(CLEAR_FRAMEWORK_ORDER, "log", LogKit.Reset);
            Register(CLEAR_FRAMEWORK_ORDER + 10, "settings", KitSettings.Reset);
        }

        /// <summary>
        /// 进入新一代并复制当前参与者。重入时返回 null，调用方不得继续执行回调。
        /// </summary>
        /// <returns>按顺序排列的参与者副本；重入时返回 null。</returns>
        private static Participant[] BeginGeneration()
        {
            lock (sLock)
            {
                if (sBeginning)
                {
                    return null;
                }

                sBeginning = true;
                System.Threading.Interlocked.Increment(ref sGeneration);
                return CopySorted();
            }
        }

        /// <summary>
        /// 标记换代结束，允许下一次 <see cref="Begin"/>。
        /// </summary>
        private static void EndGeneration()
        {
            lock (sLock)
            {
                sBeginning = false;
            }
        }

        /// <summary>
        /// 复制并按顺序、标识排序。排序在锁内完成，回调在锁外执行。
        /// </summary>
        /// <returns>本次换代要执行的参与者。</returns>
        private static Participant[] CopySorted()
        {
            Participant[] participants = new Participant[sParticipants.Count];
            for (int index = 0; index < participants.Length; index++)
            {
                participants[index] = sParticipants[index];
            }

            Sort(participants);
            return participants;
        }

        /// <summary>
        /// 按顺序执行重置，并在工厂恢复之后记录失败。
        /// </summary>
        /// <param name="participants">本次换代的参与者。</param>
        private static void InvokeParticipants(Participant[] participants)
        {
            List<Exception> errors = null;
            for (int index = 0; index < participants.Length; index++)
            {
                try
                {
                    participants[index].Reset();
                }
                catch (Exception exception)
                {
                    if (errors == null)
                    {
                        errors = new List<Exception>();
                    }

                    errors.Add(exception);
                }
            }

            ReportFailures(errors);
        }

        /// <summary>
        /// 查找已登记的参与者。调用方必须持有 <see cref="sLock"/>。
        /// </summary>
        /// <param name="participantId">参与者标识。</param>
        /// <returns>匹配下标；不存在时返回 -1。</returns>
        private static int FindIndex(string participantId)
        {
            for (int index = 0; index < sParticipants.Count; index++)
            {
                if (string.Equals(sParticipants[index].Id, participantId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// 对少量参与者做稳定插入排序，避免为会话换代分配比较器或 LINQ 缓冲。
        /// </summary>
        /// <param name="participants">待排序的参与者。</param>
        private static void Sort(Participant[] participants)
        {
            for (int index = 1; index < participants.Length; index++)
            {
                Participant current = participants[index];
                int cursor = index;
                while (cursor > 0 && Compare(participants[cursor - 1], current) > 0)
                {
                    participants[cursor] = participants[cursor - 1];
                    cursor--;
                }

                participants[cursor] = current;
            }
        }

        /// <summary>
        /// 先比顺序，再比标识，保证同层回调的顺序稳定。
        /// </summary>
        /// <param name="left">左侧参与者。</param>
        /// <param name="right">右侧参与者。</param>
        /// <returns>左侧应先执行时返回负数。</returns>
        private static int Compare(Participant left, Participant right)
        {
            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
            {
                return order;
            }

            return string.CompareOrdinal(left.Id, right.Id);
        }

        /// <summary>
        /// 在全部回调结束后记录失败。日志工厂此时已经按恢复阶段重新登记。
        /// </summary>
        /// <param name="errors">回调期间捕获的异常；没有失败时为 null。</param>
        private static void ReportFailures(List<Exception> errors)
        {
            if (errors == null)
            {
                return;
            }

            LogKit.Error("[Session] Failed to reset " + errors.Count + " participant(s): " + errors[0]);
        }
    }
}
