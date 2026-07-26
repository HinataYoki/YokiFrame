#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>
    /// 按 FSM 实例维护稳定诊断身份；同名实例保持独立，不依赖宿主或反射扫描。
    /// </summary>
    internal static class FsmKitRegistry
    {
        private const string INSTANCE_ID_PREFIX = "fsm-";
        private const string UNNAMED_FSM = "UnnamedFSM";

        private static readonly object sGate = new object();
        private static readonly List<FsmKitDiagnosticRecord> sRecords =
            new List<FsmKitDiagnosticRecord>();

        private static long sNextInstanceId;
        private static long sStateVersion;

        /// <summary>
        /// 获取诊断状态的单调版本；宿主据此只在领域事实变化后发布 Telemetry。
        /// </summary>
        internal static long StateVersion
        {
            get
            {
                lock (sGate)
                {
                    return sStateVersion;
                }
            }
        }

        /// <summary>
        /// 注册状态机；重复注册同一实例只更新名称，不改变 instanceId 或历史。
        /// </summary>
        /// <param name="fsm">要注册的状态机实例。</param>
        /// <param name="name">诊断名称；为空时回落状态机自身名称。</param>
        internal static void Register(IFSM fsm, string name)
        {
            EnsureFsm(fsm);
            string resolvedName = ResolveRegistrationName(fsm, name);
            lock (sGate)
            {
                FsmKitDiagnosticRecord record = FindByFsm(fsm);
                if (record != null)
                {
                    record.Rename(resolvedName);
                    MarkChanged();
                    return;
                }

                sRecords.Add(CreateRecord(fsm, resolvedName));
                MarkChanged();
            }
        }

        /// <summary>
        /// 重命名指定状态机；尚未注册时会以新名称完成首次注册。
        /// </summary>
        /// <param name="fsm">目标状态机实例。</param>
        /// <param name="name">新的非空诊断名称。</param>
        internal static void Rename(IFSM fsm, string name)
        {
            EnsureFsm(fsm);
            EnsureName(name);
            lock (sGate)
            {
                FsmKitDiagnosticRecord record = FindByFsm(fsm);
                if (record == null)
                {
                    sRecords.Add(CreateRecord(fsm, name));
                    MarkChanged();
                    return;
                }

                record.Rename(name);
                MarkChanged();
            }
        }

        /// <summary>
        /// 按实例注销状态机及其全部诊断记录。
        /// </summary>
        /// <param name="fsm">目标状态机实例。</param>
        internal static void Unregister(IFSM fsm)
        {
            if (fsm == null)
            {
                return;
            }

            lock (sGate)
            {
                int index = FindIndexByFsm(fsm);
                if (index >= 0)
                {
                    sRecords.RemoveAt(index);
                    MarkChanged();
                }
            }
        }

        /// <summary>
        /// 记录一次成功状态切换。
        /// </summary>
        /// <param name="fsm">状态机实例。</param>
        /// <param name="from">来源状态名称。</param>
        /// <param name="to">目标状态名称。</param>
        internal static void RecordTransition(IFSM fsm, string from, string to)
        {
            lock (sGate)
            {
                GetOrCreateRecord(fsm).RecordTransition(from, to);
                MarkChanged();
            }
        }

        /// <summary>
        /// 记录状态加入或移除事件。
        /// </summary>
        /// <param name="fsm">状态机实例。</param>
        /// <param name="eventName">稳定事件名称。</param>
        /// <param name="state">状态名称。</param>
        internal static void RecordStateEvent(IFSM fsm, string eventName, string state)
        {
            lock (sGate)
            {
                GetOrCreateRecord(fsm).RecordStateEvent(eventName, state);
                MarkChanged();
            }
        }

        /// <summary>
        /// 清空指定实例的转换和状态事件记录，保留稳定注册身份。
        /// </summary>
        /// <param name="fsm">状态机实例。</param>
        internal static void ClearRecords(IFSM fsm)
        {
            if (fsm == null)
            {
                return;
            }

            lock (sGate)
            {
                FsmKitDiagnosticRecord record = FindByFsm(fsm);
                if (record != null)
                {
                    record.ClearRecords();
                    MarkChanged();
                }
            }
        }

        /// <summary>
        /// 标记状态机生命周期阶段已经变化，但没有新增转换或状态事件记录。
        /// </summary>
        /// <param name="fsm">发生变化的已注册状态机。</param>
        internal static void NotifyStateChanged(IFSM fsm)
        {
            if (fsm == null)
            {
                return;
            }

            lock (sGate)
            {
                FsmKitDiagnosticRecord record = FindByFsm(fsm);
                if (record != null)
                {
                    record.NotifyStateChanged();
                    MarkChanged();
                }
            }
        }

        /// <summary>
        /// 获取按注册顺序排列的全部独立诊断快照，并剔除状态机已被回收的记录。
        /// </summary>
        /// <returns>调用方可安全枚举的快照数组。</returns>
        internal static FsmKitDiagnosticSnapshot[] GetAllSnapshots()
        {
            lock (sGate)
            {
                PruneDeadRecords();
                List<FsmKitDiagnosticSnapshot> snapshots =
                    new List<FsmKitDiagnosticSnapshot>(sRecords.Count);
                for (var index = 0; index < sRecords.Count; index++)
                {
                    FsmKitDiagnosticSnapshot snapshot = sRecords[index].CreateSnapshot();
                    if (snapshot != null)
                    {
                        snapshots.Add(snapshot);
                    }
                }

                return snapshots.ToArray();
            }
        }

        /// <summary>
        /// 获取当前全部活动实例标识，供宿主建立按实例拆分的 Shared Memory latest frame。
        /// </summary>
        /// <returns>按注册顺序排列的安全 instanceId 数组。</returns>
        internal static string[] GetInstanceIds()
        {
            lock (sGate)
            {
                PruneDeadRecords();
                string[] instanceIds = new string[sRecords.Count];
                for (var index = 0; index < sRecords.Count; index++)
                {
                    instanceIds[index] = sRecords[index].InstanceId;
                }

                return instanceIds;
            }
        }

        /// <summary>
        /// 按实例标识读取单实例诊断版本，不创建完整诊断快照。
        /// </summary>
        /// <param name="instanceId">由注册表生成的稳定实例标识。</param>
        /// <returns>活动实例的当前版本；实例已经失效或不存在时返回零。</returns>
        internal static long GetInstanceVersion(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return 0L;
            }

            lock (sGate)
            {
                FsmKitDiagnosticRecord record = FindByInstanceId(instanceId);
                return record == null ? 0L : record.Version;
            }
        }

        /// <summary>
        /// 优先按 instanceId 精确查找，否则按名称返回最后注册的兼容目标；查找前剔除已回收记录。
        /// </summary>
        /// <param name="instanceId">精确实例标识。</param>
        /// <param name="name">兼容诊断名称。</param>
        /// <returns>找到的独立快照；没有匹配项或实例已被回收时为空。</returns>
        internal static FsmKitDiagnosticSnapshot FindSnapshot(string instanceId, string name)
        {
            lock (sGate)
            {
                PruneDeadRecords();
                FsmKitDiagnosticRecord record = !string.IsNullOrEmpty(instanceId)
                    ? FindByInstanceId(instanceId)
                    : FindByName(name);
                return record?.CreateSnapshot();
            }
        }

        /// <summary>
        /// 按兼容名称注销最后注册的同名实例，避免一次调用误删其它同名 FSM。
        /// </summary>
        /// <param name="name">诊断名称。</param>
        internal static void UnregisterByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            lock (sGate)
            {
                int index = FindIndexByName(name);
                if (index >= 0)
                {
                    sRecords.RemoveAt(index);
                    MarkChanged();
                }
            }
        }

        /// <summary>
        /// 清空全部实例及历史；instanceId 计数不回退，避免旧引用误命中新实例。
        /// </summary>
        internal static void ClearAll()
        {
            lock (sGate)
            {
                if (sRecords.Count > 0)
                {
                    sRecords.Clear();
                    MarkChanged();
                }
            }
        }

        /// <summary>推进诊断版本；调用方必须持有注册表锁。</summary>
        private static void MarkChanged()
        {
            sStateVersion++;
        }

        /// <summary>取得已有记录，缺失时按状态机自身名称创建。</summary>
        private static FsmKitDiagnosticRecord GetOrCreateRecord(IFSM fsm)
        {
            EnsureFsm(fsm);
            FsmKitDiagnosticRecord record = FindByFsm(fsm);
            if (record != null)
            {
                return record;
            }

            record = CreateRecord(fsm, ResolveRegistrationName(fsm, fsm.Name));
            sRecords.Add(record);
            return record;
        }

        /// <summary>创建带新 instanceId 的记录；调用方必须持有注册表锁。</summary>
        private static FsmKitDiagnosticRecord CreateRecord(IFSM fsm, string name)
        {
            sNextInstanceId++;
            string suffix = sNextInstanceId.ToString("D8", CultureInfo.InvariantCulture);
            return new FsmKitDiagnosticRecord(INSTANCE_ID_PREFIX + suffix, name, fsm);
        }

        /// <summary>按引用查找记录，避免状态机覆写 Equals 后合并实例。</summary>
        private static FsmKitDiagnosticRecord FindByFsm(IFSM fsm)
        {
            int index = FindIndexByFsm(fsm);
            return index >= 0 ? sRecords[index] : null;
        }

        /// <summary>按状态机引用查找索引；调用方必须持有注册表锁。</summary>
        private static int FindIndexByFsm(IFSM fsm)
        {
            for (var index = 0; index < sRecords.Count; index++)
            {
                if (sRecords[index].TryGetFsm(out IFSM candidate) && ReferenceEquals(candidate, fsm))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>移除状态机已被回收的记录；调用方必须持有注册表锁。</summary>
        private static void PruneDeadRecords()
        {
            bool removed = false;
            for (var index = sRecords.Count - 1; index >= 0; index--)
            {
                if (!sRecords[index].TryGetFsm(out _))
                {
                    sRecords.RemoveAt(index);
                    removed = true;
                }
            }

            if (removed)
            {
                MarkChanged();
            }
        }

        /// <summary>按实例标识查找记录。</summary>
        private static FsmKitDiagnosticRecord FindByInstanceId(string instanceId)
        {
            int index = FindIndexByInstanceId(instanceId);
            return index >= 0 ? sRecords[index] : null;
        }

        /// <summary>按实例标识查找索引。</summary>
        private static int FindIndexByInstanceId(string instanceId)
        {
            for (var index = 0; index < sRecords.Count; index++)
            {
                if (string.Equals(sRecords[index].InstanceId, instanceId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>按名称返回最后注册记录，保持旧版同名覆盖查询体验。</summary>
        private static FsmKitDiagnosticRecord FindByName(string name)
        {
            int index = FindIndexByName(name);
            return index >= 0 ? sRecords[index] : null;
        }

        /// <summary>按名称从后向前查找索引。</summary>
        private static int FindIndexByName(string name)
        {
            for (var index = sRecords.Count - 1; index >= 0; index--)
            {
                if (string.Equals(sRecords[index].Name, name, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>解析首次注册名称，确保所有实例都可被名称查询。</summary>
        private static string ResolveRegistrationName(IFSM fsm, string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }

            return string.IsNullOrEmpty(fsm.Name) ? UNNAMED_FSM : fsm.Name;
        }

        /// <summary>拒绝空状态机实例。</summary>
        private static void EnsureFsm(IFSM fsm)
        {
            if (fsm == null)
            {
                throw new ArgumentNullException(nameof(fsm));
            }
        }

        /// <summary>拒绝空诊断名称或实例标识。</summary>
        private static void EnsureName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("FSM identity must not be empty.", nameof(name));
            }
        }
    }
}
#endif
