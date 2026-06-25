using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 命令桥处理器。
    /// 只暴露诊断和显式维护动作，不通过文件桥传输真实存档 payload，避免把项目私有存档结构写进协议层。
    /// </summary>
    public sealed class SaveKitCommandHandler : IKitCommandHandler
    {
        /// <inheritdoc />
        public string KitName => "SaveKit";

        /// <inheritdoc />
        public string[] SupportedActions => new[]
        {
            "stats",
            "list_slots",
            "get_workbench_snapshot",
            "delete_slot",
            "disable_auto_save"
        };

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return BuildStatsJson();
                case "list_slots":
                    return BuildSlotsJson();
                case "get_workbench_snapshot":
                    return BuildWorkbenchSnapshotJson();
                case "delete_slot":
                    return DeleteSlot(payloadJson);
                case "disable_auto_save":
                    SaveKit.DisableAutoSave();
                    return BuildAutoSaveJson();
                default:
                    throw new NotSupportedException("Unknown SaveKit action '" + action + "'");
            }
        }

        private static string BuildWorkbenchSnapshotJson()
        {
            var stats = BuildStatsJson();
            var slots = BuildSlotsJson();
            var autoSave = BuildAutoSaveJson();

            var sb = new StringBuilder(stats.Length + slots.Length + autoSave.Length + 56);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"slots\":");
            sb.Append(slots);
            sb.Append(",\"autoSave\":");
            sb.Append(autoSave);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildStatsJson()
        {
            var slots = SaveKit.GetAllSlots();
            var storage = SaveKit.GetStorage();
            var serializer = SaveKit.GetSerializer();
            var encryptor = SaveKit.GetEncryptor();

            var sb = new StringBuilder(256);
            sb.Append("{\"currentVersion\":");
            sb.Append(SaveKit.GetCurrentVersion());
            sb.Append(",\"maxSlots\":");
            sb.Append(SaveKit.GetMaxSlots());
            sb.Append(",\"slotCount\":");
            sb.Append(slots.Count);
            sb.Append(",\"autoSaveEnabled\":");
            sb.Append(SaveKit.IsAutoSaveEnabled ? "true" : "false");
            sb.Append(",\"storageType\":\"");
            sb.Append(JsonHelper.EscapeString(GetTypeName(storage)));
            sb.Append("\",\"serializerType\":\"");
            sb.Append(JsonHelper.EscapeString(GetTypeName(serializer)));
            sb.Append("\",\"encryptorType\":\"");
            sb.Append(JsonHelper.EscapeString(GetTypeName(encryptor)));
            sb.Append("\",\"hasEncryptor\":");
            sb.Append(encryptor != null ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildSlotsJson()
        {
            var slots = SaveKit.GetAllSlots();
            var sb = new StringBuilder(256);
            sb.Append("{\"slots\":[");
            for (var i = 0; i < slots.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendSlot(sb, slots[i]);
            }

            sb.Append("],\"count\":");
            sb.Append(slots.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildAutoSaveJson()
        {
            var enabled = SaveKit.IsAutoSaveEnabled;
            var sb = new StringBuilder(128);
            sb.Append("{\"autoSaveEnabled\":");
            sb.Append(enabled ? "true" : "false");
            sb.Append(",\"autoSaveSlotId\":");
            sb.Append(enabled ? SaveKit.GetAutoSaveSlotId() : -1);
            sb.Append(",\"autoSaveIntervalSeconds\":");
            AppendFloat(sb, enabled ? SaveKit.GetAutoSaveIntervalSeconds() : 0f);
            sb.Append(",\"autoSaveElapsedSeconds\":");
            AppendFloat(sb, enabled ? SaveKit.GetAutoSaveElapsedSeconds() : 0f);
            sb.Append('}');
            return sb.ToString();
        }

        private static string DeleteSlot(string payloadJson)
        {
            int slotId;
            if (!JsonHelper.TryExtractInt(payloadJson, "slotId", out slotId) &&
                !JsonHelper.TryExtractInt(payloadJson, "slot", out slotId))
            {
                throw new ArgumentException("Missing numeric 'slotId' in payload");
            }

            var deleted = SaveKit.Delete(slotId);
            return "{\"deleted\":" + (deleted ? "true" : "false") + ",\"slotId\":" + slotId + "}";
        }

        private static void AppendSlot(StringBuilder sb, SaveMeta meta)
        {
            sb.Append("{\"slotId\":");
            sb.Append(meta.SlotId);
            sb.Append(",\"version\":");
            sb.Append(meta.Version);
            sb.Append(",\"displayName\":\"");
            sb.Append(JsonHelper.EscapeString(meta.DisplayName ?? string.Empty));
            sb.Append("\",\"createdTimestamp\":");
            sb.Append(meta.CreatedTimestamp);
            sb.Append(",\"lastSavedTimestamp\":");
            sb.Append(meta.LastSavedTimestamp);
            sb.Append(",\"createdAtUtc\":\"");
            sb.Append(JsonHelper.EscapeString(ToUtcString(meta.CreatedTimestamp)));
            sb.Append("\",\"savedAtUtc\":\"");
            sb.Append(JsonHelper.EscapeString(ToUtcString(meta.LastSavedTimestamp)));
            sb.Append("\"}");
        }

        private static string ToUtcString(long unixSeconds)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return string.Empty;
            }
        }

        private static string GetTypeName(object instance)
        {
            return instance == null ? string.Empty : instance.GetType().Name;
        }

        private static void AppendFloat(StringBuilder sb, float value)
        {
            sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }
}
