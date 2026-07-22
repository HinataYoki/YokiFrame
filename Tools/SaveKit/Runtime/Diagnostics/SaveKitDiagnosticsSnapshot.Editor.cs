#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Threading;

namespace YokiFrame
{
    /// <summary>描述 Editor/Tools 读取的 SaveKit 有界状态，不包含任何模块 payload。</summary>
    internal sealed class SaveKitDiagnosticsSnapshot
    {
        /// <summary>使用当前后端、自动保存和容器头摘要创建只读诊断快照。</summary>
        internal SaveKitDiagnosticsSnapshot(
            bool storageConfigured,
            bool serializerConfigured,
            long stateVersion,
            string storageType,
            string serializerId,
            string encryptorId,
            bool autoSaveEnabled,
            SaveTarget autoSaveTarget,
            float autoSaveIntervalSeconds,
            float autoSaveElapsedSeconds,
            IReadOnlyList<SaveKitDiagnosticsMeta> slots,
            int slotTotal,
            bool slotsTruncated,
            IReadOnlyList<SaveKitDiagnosticsMeta> globals,
            int globalTotal,
            bool globalsTruncated,
            bool metadataAvailable,
            bool metadataReadFailed)
        {
            StorageConfigured = storageConfigured;
            SerializerConfigured = serializerConfigured;
            StateVersion = stateVersion;
            StorageType = storageType;
            SerializerId = serializerId;
            EncryptorId = encryptorId;
            AutoSaveEnabled = autoSaveEnabled;
            AutoSaveTarget = autoSaveTarget;
            AutoSaveIntervalSeconds = autoSaveIntervalSeconds;
            AutoSaveElapsedSeconds = autoSaveElapsedSeconds;
            Slots = slots;
            SlotTotal = slotTotal;
            SlotsTruncated = slotsTruncated;
            Globals = globals;
            GlobalTotal = globalTotal;
            GlobalsTruncated = globalsTruncated;
            MetadataAvailable = metadataAvailable;
            MetadataReadFailed = metadataReadFailed;
        }

        /// <summary>获取 Storage 是否已经由业务或显式注入创建。</summary>
        internal bool StorageConfigured { get; }

        /// <summary>获取 Serializer 是否已经由业务或显式注入创建。</summary>
        internal bool SerializerConfigured { get; }

        /// <summary>获取当前只读状态的单调版本，用于 FileBridge 增量写入。</summary>
        internal long StateVersion { get; }

        /// <summary>获取已配置 Storage 的类型名称；未配置时为空。</summary>
        internal string StorageType { get; }

        /// <summary>获取已配置 Serializer 的稳定标识；未配置时为空。</summary>
        internal string SerializerId { get; }

        /// <summary>获取已配置 Encryptor 的稳定标识；未配置时为空。</summary>
        internal string EncryptorId { get; }

        /// <summary>获取自动保存是否已启用。</summary>
        internal bool AutoSaveEnabled { get; }

        /// <summary>获取启用时的自动保存目标；未启用时忽略该值。</summary>
        internal SaveTarget AutoSaveTarget { get; }

        /// <summary>获取启用时的自动保存间隔秒数。</summary>
        internal float AutoSaveIntervalSeconds { get; }

        /// <summary>获取当前自动保存累计秒数。</summary>
        internal float AutoSaveElapsedSeconds { get; }

        /// <summary>获取已读取且头部有效的槽位元数据。</summary>
        internal IReadOnlyList<SaveKitDiagnosticsMeta> Slots { get; }

        /// <summary>获取 Storage 枚举出的槽位目标数量。</summary>
        internal int SlotTotal { get; }

        /// <summary>获取槽位元数据是否超过本次诊断读取预算。</summary>
        internal bool SlotsTruncated { get; }

        /// <summary>获取已读取且头部有效的 Global 元数据。</summary>
        internal IReadOnlyList<SaveKitDiagnosticsMeta> Globals { get; }

        /// <summary>获取 Storage 枚举出的 Global 目标数量。</summary>
        internal int GlobalTotal { get; }

        /// <summary>获取 Global 元数据是否超过本次诊断读取预算。</summary>
        internal bool GlobalsTruncated { get; }

        /// <summary>获取当前 Storage 是否支持只读容器头查询。</summary>
        internal bool MetadataAvailable { get; }

        /// <summary>获取本次头部枚举是否遇到 Storage 读取失败。</summary>
        internal bool MetadataReadFailed { get; }
    }

    /// <summary>描述单个存档容器的安全头部字段，不保留或解析 payload。</summary>
    internal sealed class SaveKitDiagnosticsMeta
    {
        /// <summary>从已验证的容器头复制可公开的元数据。</summary>
        internal SaveKitDiagnosticsMeta(SaveMeta meta)
        {
            Target = meta.Target;
            ContainerVersion = meta.ContainerVersion;
            CreatedTimestamp = meta.CreatedTimestamp;
            LastSavedTimestamp = meta.LastSavedTimestamp;
            DisplayName = meta.DisplayName ?? string.Empty;
            SerializerId = meta.SerializerId ?? string.Empty;
        }

        /// <summary>获取槽位或 Global 目标。</summary>
        internal SaveTarget Target { get; }

        /// <summary>获取容器格式版本。</summary>
        internal int ContainerVersion { get; }

        /// <summary>获取创建 Unix 秒时间戳。</summary>
        internal long CreatedTimestamp { get; }

        /// <summary>获取最近保存 Unix 秒时间戳。</summary>
        internal long LastSavedTimestamp { get; }

        /// <summary>获取用户可见显示名称。</summary>
        internal string DisplayName { get; }

        /// <summary>获取写入该容器的 Serializer 标识。</summary>
        internal string SerializerId { get; }
    }

    /// <summary>提供不会初始化默认后端的 SaveKit Editor/Tools 诊断入口。</summary>
    public static partial class SaveKit
    {
        private const int MAX_DIAGNOSTIC_METADATA_PER_KIND = 32;
        private static long sInteractionStateVersion;

        /// <summary>获取当前 Editor/Tools Interaction state 的单调版本。</summary>
        internal static long InteractionStateVersion
        {
            get { return Interlocked.Read(ref sInteractionStateVersion); }
        }

        /// <summary>在可观察的 SaveKit 状态变化后推进版本，不在 Player 编译。</summary>
        internal static void MarkInteractionStateChanged()
        {
            Interlocked.Increment(ref sInteractionStateVersion);
        }

        /// <summary>
        /// 复制当前已经存在的后端和容器头摘要；该方法绝不调用 EnsureBackend，
        /// 因此纯观察不会创建默认 Storage 或 Serializer。
        /// </summary>
        /// <returns>供 Editor/Tools Provider 使用的有界快照。</returns>
        internal static SaveKitDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        {
            ISaveStorage storage = sStorage;
            ISaveSerializer serializer = sSerializer;
            ISaveEncryptor encryptor = sEncryptor;
            var metadataStorage = storage as ISaveMetadataStorage;
            ReadDiagnosticMetadata(
                storage,
                metadataStorage,
                out List<SaveKitDiagnosticsMeta> slots,
                out int slotTotal,
                out bool slotsTruncated,
                out List<SaveKitDiagnosticsMeta> globals,
                out int globalTotal,
                out bool globalsTruncated,
                out bool metadataReadFailed);
            return new SaveKitDiagnosticsSnapshot(
                storage != null,
                serializer != null,
                InteractionStateVersion,
                storage == null ? string.Empty : storage.GetType().Name,
                serializer == null ? string.Empty : serializer.SerializerId ?? string.Empty,
                encryptor == null ? string.Empty : encryptor.EncryptorId ?? string.Empty,
                sAutoSaveEnabled,
                sAutoSaveTarget,
                sAutoSaveIntervalSeconds,
                sAutoSaveElapsedSeconds,
                slots,
                slotTotal,
                slotsTruncated,
                globals,
                globalTotal,
                globalsTruncated,
                metadataStorage != null,
                metadataReadFailed);
        }

        /// <summary>读取已存在 Storage 的 Slot/Global 容器头，并把后端异常降为可观察失败状态。</summary>
        private static void ReadDiagnosticMetadata(
            ISaveStorage storage,
            ISaveMetadataStorage metadataStorage,
            out List<SaveKitDiagnosticsMeta> slots,
            out int slotTotal,
            out bool slotsTruncated,
            out List<SaveKitDiagnosticsMeta> globals,
            out int globalTotal,
            out bool globalsTruncated,
            out bool metadataReadFailed)
        {
            slots = new List<SaveKitDiagnosticsMeta>();
            globals = new List<SaveKitDiagnosticsMeta>();
            slotTotal = 0;
            globalTotal = 0;
            slotsTruncated = false;
            globalsTruncated = false;
            metadataReadFailed = false;
            if (storage == null)
            {
                return;
            }

            ReadDiagnosticMetadataForKind(
                storage, metadataStorage, SaveTargetKind.Slot, slots, out slotTotal, out slotsTruncated, ref metadataReadFailed);
            ReadDiagnosticMetadataForKind(
                storage, metadataStorage, SaveTargetKind.Global, globals, out globalTotal, out globalsTruncated, ref metadataReadFailed);
        }

        /// <summary>读取单一目标域的固定上限容器头，损坏文件只标记失败而不传播到 Host。</summary>
        private static void ReadDiagnosticMetadataForKind(
            ISaveStorage storage,
            ISaveMetadataStorage metadataStorage,
            SaveTargetKind kind,
            List<SaveKitDiagnosticsMeta> output,
            out int targetTotal,
            out bool truncated,
            ref bool readFailed)
        {
            targetTotal = 0;
            truncated = false;
            try
            {
                IReadOnlyList<SaveTarget> targets = storage.GetTargets(kind);
                targetTotal = targets.Count;
                int limit = Math.Min(targets.Count, MAX_DIAGNOSTIC_METADATA_PER_KIND);
                truncated = targets.Count > limit;
                if (metadataStorage == null)
                {
                    return;
                }

                for (int index = 0; index < limit; index++)
                {
                    if (TryReadDiagnosticMeta(metadataStorage, targets[index], out SaveKitDiagnosticsMeta meta))
                    {
                        output.Add(meta);
                    }
                    else
                    {
                        readFailed = true;
                    }
                }
            }
            catch (Exception)
            {
                readFailed = true;
            }
        }

        /// <summary>读取并验证单个容器头；失败时不读取或暴露 payload。</summary>
        private static bool TryReadDiagnosticMeta(
            ISaveMetadataStorage storage,
            SaveTarget target,
            out SaveKitDiagnosticsMeta meta)
        {
            meta = null;
            try
            {
                if (!storage.TryReadMetadata(target, out SaveMeta header))
                {
                    return false;
                }

                meta = new SaveKitDiagnosticsMeta(header);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
#endif
